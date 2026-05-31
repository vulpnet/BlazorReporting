using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

/// <summary>
/// S3 — Audit Log (ISO 27001 A.12.4.1)
/// Ghi lai moi hoat dong: login/logout/xem bao cao/export/sua config.
/// Luu vao DB URCeTools, table AppAuditLog, giu 3 nam.
/// Fire-and-forget: khong block UI, khong throw exception.
/// </summary>
public sealed class AuditLogService
{
    private readonly string _cs;
    private readonly ILogger<AuditLogService> _log;
    private readonly IHttpContextAccessor _http;

    public AuditLogService(IConfiguration cfg, ILogger<AuditLogService> log, IHttpContextAccessor http)
    {
        _cs  = cfg.GetConnectionString("DefaultConnection")!;
        _log = log;
        _http = http;
    }

    // ── Event types ──────────────────────────────────────────────────
    public const string LOGIN           = "LOGIN";
    public const string LOGOUT          = "LOGOUT";
    public const string SESSION_TIMEOUT = "SESSION_TIMEOUT";
    public const string VIEW_REPORT     = "VIEW_REPORT";
    public const string EXPORT_DATA     = "EXPORT_DATA";
    public const string CONFIG_CHANGE   = "CONFIG_CHANGE";
    public const string VIEW_DASHBOARD  = "VIEW_DASHBOARD";
    public const string VIEW_MAP        = "VIEW_MAP";

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>Ghi log — fire and forget, khong block UI.</summary>
    public void Log(string username, string eventType, string? page = null,
                    string? detail = null, string? fullName = null,
                    string? role = null, bool isSuccess = true)
    {
        // Fire and forget — khong await, khong block caller
        _ = WriteAsync(username, fullName, role, eventType, page, detail, isSuccess);
    }

    /// <summary>Lay IP tu HttpContext.</summary>
    public string? GetClientIp()
    {
        try
        {
            var ctx = _http.HttpContext;
            if (ctx is null) return null;
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',')[0].Trim();
            return ctx.Connection.RemoteIpAddress?.ToString();
        }
        catch { return null; }
    }

    // ── Internal ─────────────────────────────────────────────────────

    private async Task WriteAsync(string username, string? fullName, string? role,
        string eventType, string? page, string? detail, bool isSuccess)
    {
        try
        {
            var ip        = GetClientIp();
            var userAgent = _http.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();

            await using var conn = new SqlConnection(_cs);
            await conn.ExecuteAsync(
                """
                INSERT INTO AppAuditLog
                    (UserName, FullName, Role, EventType, Page, Detail, IpAddress, UserAgent, IsSuccess)
                VALUES
                    (@UserName, @FullName, @Role, @EventType, @Page, @Detail, @IpAddress, @UserAgent, @IsSuccess)
                """,
                new
                {
                    UserName  = username,
                    FullName  = fullName,
                    Role      = role,
                    EventType = eventType,
                    Page      = page,
                    Detail    = detail?[..Math.Min(detail.Length, 500)],
                    IpAddress = ip,
                    UserAgent = userAgent?[..Math.Min(userAgent.Length, 500)],
                    IsSuccess = isSuccess
                },
                commandTimeout: 5);
        }
        catch (Exception ex)
        {
            // Khong crash app khi audit log loi
            _log.LogDebug(ex, "[S3] AuditLog write failed for {User}/{Event}", username, eventType);
        }
    }
}
