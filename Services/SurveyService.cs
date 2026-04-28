using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

public sealed record SurveyResult(bool Completed, int? Score, string? Feedback);

public sealed class SurveyService(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("DefaultConnection") ?? "";

    // ── Kiểm tra hôm nay đã làm khảo sát chưa ───────────
    public async Task<SurveyResult> GetTodayAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new(false, null, null);
        try
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT TOP 1 IsCompleted, Score, Feedback
                FROM DMSUserExperience
                WHERE UserId   = @userId
                  AND SurveyDate = CAST(GETDATE() AS DATE)
                """;
            cmd.Parameters.AddWithValue("@userId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                return new(
                    r.GetBoolean(0),
                    r.IsDBNull(1) ? null : (int?)r.GetByte(1),
                    r.IsDBNull(2) ? null : r.GetString(2));
        }
        catch { }
        return new(false, null, null);
    }

    // ── Tạo bản ghi khảo sát (nếu chưa có) ──────────────
    public async Task EnsureCreatedAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        try
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                IF NOT EXISTS (
                    SELECT 1 FROM DMSUserExperience
                    WHERE UserId = @userId AND SurveyDate = CAST(GETDATE() AS DATE))
                INSERT INTO DMSUserExperience (UserId) VALUES (@userId)
                """;
            cmd.Parameters.AddWithValue("@userId", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }

    // ── Lưu kết quả khảo sát ─────────────────────────────
    public async Task SaveAsync(string userId, int score, string? feedback)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        try
        {
            await using var conn = new SqlConnection(_cs);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                MERGE DMSUserExperience AS target
                USING (SELECT @userId AS UserId, CAST(GETDATE() AS DATE) AS SurveyDate) AS src
                  ON target.UserId = src.UserId AND target.SurveyDate = src.SurveyDate
                WHEN MATCHED THEN
                    UPDATE SET Score = @score, Feedback = @feedback,
                               IsCompleted = 1, CompletedAt = SYSDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, Score, Feedback, IsCompleted, CompletedAt)
                    VALUES (@userId, @score, @feedback, 1, SYSDATETIME());
                """;
            cmd.Parameters.AddWithValue("@userId",   userId);
            cmd.Parameters.AddWithValue("@score",    (byte)score);
            cmd.Parameters.AddWithValue("@feedback", (object?)feedback ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }
    }
}
