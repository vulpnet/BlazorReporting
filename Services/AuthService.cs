using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

public sealed record AuthSession(string Username, string FullName, string Role);

public sealed class AuthService
{
    private readonly string _connStr;

    public bool IsInitialized { get; private set; }
    public bool IsLoggedIn    { get; private set; }
    public string UserName    { get; private set; } = "";
    public string FullName    { get; private set; } = "";
    public string Role        { get; private set; } = "";

    public event Action? OnAuthChanged;

    public AuthService(IConfiguration config)
        => _connStr = config.GetConnectionString("DefaultConnection")!;

    // Xác thực từ DB DMS2.0 — webpages_Membership dùng SHA1 hash
    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            const string sql = """
                SELECT up.UserId, upi.FullName,
                       m.Password AS HashedPassword
                FROM   UserProfile up
                JOIN   webpages_Membership m  ON m.UserId = up.UserId
                LEFT JOIN UserProfileInfo upi ON upi.LoginID = up.UserName
                WHERE  up.UserName = @UserName
                  AND  m.IsConfirmed = 1
                """;

            await using var conn = new SqlConnection(_connStr);
            var row = await conn.QueryFirstOrDefaultAsync<UserRow>(sql, new { UserName = username.Trim() });
            if (row is null) return false;

            if (!VerifyPassword(password, row.HashedPassword)) return false;

            Apply(username.Trim(), row.FullName ?? username.Trim(), "User", initialized: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Restore từ ProtectedLocalStorage
    public void Restore(AuthSession session)
        => Apply(session.Username, session.FullName, session.Role, initialized: true);

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

    // ASP.NET Identity v1 format: [0x00][16-byte salt][32-byte PBKDF2-HMAC-SHA1(1000 iter)]
    private static bool VerifyPassword(string plainPassword, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        try
        {
            var bytes = Convert.FromBase64String(storedHash);
            if (bytes.Length != 49 || bytes[0] != 0x00) return false;

            var salt     = bytes[1..17];
            var expected = bytes[17..];
            var actual   = Pbkdf2Sha1(plainPassword, salt, 1000, 32);
            return CryptographicEquals(actual, expected);
        }
        catch { return false; }
    }

    private static byte[] Pbkdf2Sha1(string password, byte[] salt, int iterations, int outputBytes)
    {
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            password, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA1);
        return pbkdf2.GetBytes(outputBytes);
    }

    private static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private sealed class UserRow
    {
        public int    UserId        { get; init; }
        public string? FullName     { get; init; }
        public string HashedPassword { get; init; } = "";
    }
}
