using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

public sealed record HistoryMessage(
    string Role,
    string Content,
    List<ChatNavLink> NavActions,
    DateTime CreatedAt);

public sealed class ChatHistoryService(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("DefaultConnection") ?? "";

    // ── Lưu một tin nhắn ─────────────────────────────────
    public async Task SaveAsync(string userId, string role, string content,
        IReadOnlyList<ChatNavLink>? navActions = null)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(content)) return;

        var navJson = navActions?.Count > 0
            ? JsonSerializer.Serialize(navActions) : null;

        try
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO DMSChatHistory (UserId, Role, Content, NavJson)
                VALUES (@userId, @role, @content, @navJson)
                """;
            cmd.Parameters.AddWithValue("@userId",  userId);
            cmd.Parameters.AddWithValue("@role",    role);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@navJson", (object?)navJson ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* bảng chưa tồn tại hoặc lỗi kết nối — bỏ qua */ }
    }

    // ── Tải lịch sử hôm nay ──────────────────────────────
    public async Task<List<HistoryMessage>> LoadTodayAsync(string userId)
    {
        var result = new List<HistoryMessage>();
        if (string.IsNullOrWhiteSpace(userId)) return result;

        try
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT TOP 100 Role, Content, NavJson, CreatedAt
                FROM DMSChatHistory
                WHERE UserId = @userId
                  AND ChatDate = CAST(GETDATE() AS DATE)
                ORDER BY CreatedAt ASC
                """;
            cmd.Parameters.AddWithValue("@userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var navActions = new List<ChatNavLink>();
                if (!reader.IsDBNull(2))
                {
                    try
                    {
                        navActions = JsonSerializer.Deserialize<List<ChatNavLink>>(
                            reader.GetString(2)) ?? [];
                    }
                    catch { /* invalid JSON */ }
                }
                result.Add(new HistoryMessage(
                    reader.GetString(0),
                    reader.GetString(1),
                    navActions,
                    reader.GetDateTime(3)));
            }
        }
        catch { /* bảng chưa tồn tại */ }

        return result;
    }

    // ── Xoá lịch sử hôm nay ─────────────────────────────
    public async Task ClearTodayAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        try
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM DMSChatHistory
                WHERE UserId = @userId
                  AND ChatDate = CAST(GETDATE() AS DATE)
                """;
            cmd.Parameters.AddWithValue("@userId", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
