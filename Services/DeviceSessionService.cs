using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

/// <summary>
/// S4 — Multi-device control: max 3 thiet bi dong thoi (ISO 27001 A.9.4.2)
/// Login thu 4 -> kick session cu nhat.
/// Session ID = Blazor circuit ID hoac GUID moi tao.
/// Khong anh huong logic auth hien co.
/// </summary>
public sealed class DeviceSessionService
{
    private const int MaxDevices = 3;

    private readonly string _cs;
    private readonly ILogger<DeviceSessionService> _log;
    private readonly AuditLogService _audit;

    public DeviceSessionService(IConfiguration cfg, ILogger<DeviceSessionService> log, AuditLogService audit)
    {
        _cs    = cfg.GetConnectionString("DefaultConnection")!;
        _log   = log;
        _audit = audit;
    }

    /// <summary>
    /// Dang ky session moi khi login.
    /// Neu da du 3 thiet bi, xoa session cu nhat va ghi audit.
    /// Tra ve SessionID moi.
    /// </summary>
    public async Task<string> RegisterSessionAsync(string username, string? deviceInfo, string? ip)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        await using var conn = new SqlConnection(_cs);

        // Dem session dang active
        var activeSessions = (await conn.QueryAsync<(long Id, string SessionId, DateTime Login)>(
            """
            SELECT ID, SessionID, LoginTime
            FROM AppUserSession
            WHERE UserName=@User AND IsActive=1
            ORDER BY LoginTime ASC
            """,
            new { User = username })).ToList();

        // Kick session cu nhat neu qua gioi han
        if (activeSessions.Count >= MaxDevices)
        {
            var toKick = activeSessions.Take(activeSessions.Count - MaxDevices + 1).ToList();
            foreach (var s in toKick)
            {
                await conn.ExecuteAsync(
                    "UPDATE AppUserSession SET IsActive=0 WHERE ID=@Id",
                    new { Id = s.Id });
                _log.LogInformation("[S4] Kicked session {Sid} cua {User} (max {Max} devices)",
                    s.SessionId[..8], username, MaxDevices);
                _audit.Log(username, "SESSION_KICKED",
                    detail: $"Kick session {s.SessionId[..8]} vi dang nhap thiet bi thu {MaxDevices+1}");
            }
        }

        // Them session moi
        await conn.ExecuteAsync(
            """
            INSERT INTO AppUserSession (UserName, SessionID, DeviceInfo, IpAddress)
            VALUES (@UserName, @SessionID, @DeviceInfo, @IpAddress)
            """,
            new { UserName=username, SessionID=sessionId, DeviceInfo=deviceInfo, IpAddress=ip });

        return sessionId;
    }

    /// <summary>Cap nhat thoi gian hoat dong cuoi.</summary>
    public async Task UpdateLastActiveAsync(string sessionId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.ExecuteAsync(
            "UPDATE AppUserSession SET LastActive=SYSUTCDATETIME() WHERE SessionID=@Id AND IsActive=1",
            new { Id = sessionId },
            commandTimeout: 3);
    }

    /// <summary>Huy session khi logout hoac timeout.</summary>
    public async Task RevokeSessionAsync(string sessionId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.ExecuteAsync(
            "UPDATE AppUserSession SET IsActive=0 WHERE SessionID=@Id",
            new { Id = sessionId });
    }

    /// <summary>Lay danh sach session active cua user (dung cho Security Dashboard).</summary>
    public async Task<List<ActiveSessionInfo>> GetActiveSessionsAsync(string username)
    {
        await using var conn = new SqlConnection(_cs);
        var rows = await conn.QueryAsync<ActiveSessionInfo>(
            """
            SELECT SessionID, DeviceInfo, IpAddress, LoginTime, LastActive
            FROM AppUserSession
            WHERE UserName=@User AND IsActive=1
            ORDER BY LoginTime DESC
            """,
            new { User = username });
        return rows.ToList();
    }

    /// <summary>Admin kick session bat ky.</summary>
    public async Task AdminRevokeAsync(string sessionId, string adminUser)
    {
        await using var conn = new SqlConnection(_cs);
        var row = await conn.QueryFirstOrDefaultAsync<(string UserName, string SessionID)>(
            "SELECT UserName, SessionID FROM AppUserSession WHERE SessionID=@Id",
            new { Id = sessionId });
        if (row == default) return;

        await conn.ExecuteAsync(
            "UPDATE AppUserSession SET IsActive=0 WHERE SessionID=@Id",
            new { Id = sessionId });
        _audit.Log(adminUser, "ADMIN_KICK_SESSION",
            detail: $"Admin kick session {sessionId[..8]} cua user {row.UserName}");
    }
}

public sealed class ActiveSessionInfo
{
    public string   SessionID  { get; set; } = "";
    public string?  DeviceInfo { get; set; }
    public string?  IpAddress  { get; set; }
    public DateTime LoginTime  { get; set; }
    public DateTime LastActive { get; set; }
}
