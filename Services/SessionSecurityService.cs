using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BlazorReporting.Services;

/// <summary>
/// S1 — Session Timeout 2h (ISO 27001 A.9.4.2)
/// Theo doi hoat dong cuoi, tu dong logout sau 2h khong thao tac.
/// Chi anh huong session cua chinh user do, khong anh huong user khac.
/// </summary>
public sealed class SessionSecurityService : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(2);

    private readonly AuthService _auth;
    private readonly NavigationManager _nav;
    private readonly ProtectedLocalStorage _store;
    private readonly ILogger<SessionSecurityService> _log;
    private readonly AuditLogService _audit;

    private Timer? _timer;
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _disposed;

    public SessionSecurityService(
        AuthService auth,
        NavigationManager nav,
        ProtectedLocalStorage store,
        ILogger<SessionSecurityService> log,
        AuditLogService audit)
    {
        _auth  = auth;
        _nav   = nav;
        _store = store;
        _log   = log;
        _audit = audit;
    }

    /// <summary>Goi moi khi user co thao tac (click, navigate, keypress).</summary>
    public void RecordActivity() => _lastActivity = DateTime.UtcNow;

    /// <summary>Thoi gian con lai truoc khi timeout.</summary>
    public TimeSpan TimeUntilTimeout => IdleTimeout - (DateTime.UtcNow - _lastActivity);

    /// <summary>Bat dau kiem tra idle sau khi login thanh cong.</summary>
    public void Start()
    {
        if (_timer != null) return;
        // Kiem tra moi 30 giay
        _timer = new Timer(CheckIdle, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _log.LogDebug("[S1] Session timeout monitor started for {User}", _auth.UserName);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private async void CheckIdle(object? _)
    {
        if (_disposed || !_auth.IsLoggedIn) return;

        if (DateTime.UtcNow - _lastActivity > IdleTimeout)
        {
            _log.LogInformation("[S1] Auto logout do idle 2h: {User}", _auth.UserName);
            _audit.Log(_auth.UserName, AuditLogService.SESSION_TIMEOUT,
                       detail: "Tu dong logout sau 2h idle"); // S3
            try
            {
                await _store.DeleteAsync("dmspro_auth");
            }
            catch { /* ignore storage error */ }

            _auth.Logout();
            // Navigate ve login page
            await Task.Delay(100); // dam bao Logout() hoan tat
            _nav.NavigateTo("/", forceLoad: true);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
