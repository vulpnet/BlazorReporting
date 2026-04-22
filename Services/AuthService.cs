namespace BlazorReporting.Services;

public sealed record AuthUser(string Username, string Password, string FullName, string Role = "User");
public sealed record AuthSession(string Username, string FullName, string Role);

public sealed class AuthService
{
    private readonly IConfiguration _config;

    /// <summary>
    /// True sau khi đã kiểm tra xong ProtectedLocalStorage.
    /// Dùng để tránh redirect sớm khi trang mới load.
    /// </summary>
    public bool IsInitialized { get; private set; }
    public bool IsLoggedIn    { get; private set; }
    public string UserName    { get; private set; } = "";
    public string FullName    { get; private set; } = "";
    public string Role        { get; private set; } = "";

    public event Action? OnAuthChanged;

    public AuthService(IConfiguration config) => _config = config;

    // Xác thực từ form login
    public bool Login(string username, string password)
    {
        var users = _config.GetSection("Auth:Users").Get<List<AuthUser>>() ?? [];
        var match = users.FirstOrDefault(u =>
            u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);

        if (match is null) return false;
        Apply(match.Username, match.FullName, match.Role, initialized: true);
        return true;
    }

    // Restore từ ProtectedLocalStorage (gọi trong OnAfterRenderAsync)
    public void Restore(AuthSession session)
        => Apply(session.Username, session.FullName, session.Role, initialized: true);

    // Đánh dấu đã kiểm tra storage nhưng không có session
    public void MarkInitialized()
    {
        IsInitialized = true;
        OnAuthChanged?.Invoke();
    }

    public void Logout()
    {
        IsLoggedIn    = false;
        IsInitialized = true;
        UserName = FullName = Role = "";
        OnAuthChanged?.Invoke();
    }

    private void Apply(string username, string fullName, string role, bool initialized)
    {
        IsLoggedIn    = true;
        IsInitialized = initialized;
        UserName      = username;
        FullName      = fullName;
        Role          = role;
        OnAuthChanged?.Invoke();
    }
}
