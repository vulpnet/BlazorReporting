using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

public sealed class UserVM
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int RoleID { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsConfirmed { get; set; }
    public string ApplicationCD { get; set; } = "";
}

public sealed record RoleVM(int ID, string RoleName, string Description, int? ParentID, string? ApplicationCD);
public sealed record FeatureVM(int ID, string FeatureName, string PhraseCode, string? Group, string? Page, string? Action, string? IconClass);
public sealed record RoleFeatureVM(int RoleID, int FeatureID);
public sealed record MenuFeatureVM(int RoleID, int FeatureID, string PhraseCode, string? Path, int Order, int ParentID, string? IconClass);

public sealed class UserManagementService
{
    private readonly string _cs;
    private readonly IConfiguration _cfg;

    public UserManagementService(IConfiguration cfg)
    {
        _cs = cfg.GetConnectionString("DefaultConnection")!;
        _cfg = cfg;
    }

    private SqlConnection Conn() => new(_cs);

    // ── Users ────────────────────────────────────────────────────────

    public async Task<List<UserVM>> GetUsersAsync(string currentUsername)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<UserVM>(
            "EXEC pp_UserManagement @UserName",
            new { UserName = currentUsername });
        return rows.ToList();
    }

    public async Task<bool> ActiveUserAsync(string username, bool active)
    {
        await using var conn = Conn();
        var userId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT UserId FROM UserProfile WHERE UserName = @UserName", new { UserName = username });
        if (userId is null) return false;

        await conn.ExecuteAsync(
            "UPDATE webpages_Membership SET IsConfirmed = @Active WHERE UserId = @UserId",
            new { Active = active, UserId = userId });
        return true;
    }

    public async Task<string?> GetUserEmailAsync(string username)
    {
        await using var conn = Conn();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Email FROM UserProfileInfo WHERE LoginID = @UserName", new { UserName = username });
    }

    public async Task<(bool Ok, string Message)> AddUserAsync(UserVM user, string callCenterPhone)
    {
        await using var conn = Conn();

        var emailExists = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM UserProfileInfo WHERE Email = @Email", new { user.Email });
        if (emailExists > 0) return (false, "Email này đã tồn tại.");

        var userExists = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM UserProfile WHERE UserName = @UserName", new { user.UserName });
        if (userExists > 0) return (false, "Tài khoản này đã tồn tại.");

        return (false, "Tính năng tạo user yêu cầu cấu hình SMTP. Vui lòng liên hệ admin.");
    }

    public async Task<(bool Ok, string Message)> UpdateUserAsync(UserVM user)
    {
        if (user.ApplicationCD.Equals("DMS", StringComparison.OrdinalIgnoreCase))
            return (false, "Bạn không được phép cập nhật user ngoài ERoute.");

        await using var conn = Conn();
        var userId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT UserId FROM UserProfile WHERE UserName = @UserName", new { user.UserName });
        if (userId is null) return (false, "Tài khoản không tồn tại.");

        await conn.ExecuteAsync(
            "UPDATE UserProfileInfo SET FullName=@FullName, Phone=@Phone, Email=@Email WHERE LoginID=@UserName",
            new { user.FullName, user.Phone, user.Email, user.UserName });

        await conn.ExecuteAsync("DELETE FROM RoleUser WHERE UserID = @UserId", new { UserId = userId });
        await conn.ExecuteAsync("INSERT INTO RoleUser (UserID, RoleID) VALUES (@UserId, @RoleID)",
            new { UserId = userId, user.RoleID });

        return (true, $"Cập nhật người dùng {user.UserName} thành công.");
    }

    // ── Roles ────────────────────────────────────────────────────────

    public async Task<List<RoleVM>> GetRolesAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<RoleVM>(
            "SELECT ID, RoleName, Description, ParentID, ApplicationCD FROM Role ORDER BY RoleName");
        return rows.ToList();
    }

    public async Task<RoleVM?> AddRoleAsync(string roleName, string description, int? parentId, string? appCd)
    {
        await using var conn = Conn();
        var id = await conn.QueryFirstAsync<int>(
            """
            INSERT INTO Role (RoleName, Description, ParentID, ApplicationCD)
            OUTPUT INSERTED.ID
            VALUES (@RoleName, @Description, @ParentID, @ApplicationCD)
            """,
            new { RoleName = roleName, Description = description, ParentID = parentId, ApplicationCD = appCd });
        return new RoleVM(id, roleName, description, parentId, appCd);
    }

    public async Task<bool> UpdateRoleAsync(RoleVM role)
    {
        await using var conn = Conn();
        var affected = await conn.ExecuteAsync(
            "UPDATE Role SET RoleName=@RoleName, Description=@Description, ParentID=@ParentID, ApplicationCD=@ApplicationCD WHERE ID=@ID",
            role);
        return affected > 0;
    }

    // ── Features ─────────────────────────────────────────────────────

    public async Task<List<FeatureVM>> GetFeaturesAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<FeatureVM>(
            "SELECT ID, FeatureName, PhraseCode, [Group], Page, [Action], IconClass FROM Feature ORDER BY FeatureName");
        return rows.ToList();
    }

    // ── RoleFeature ──────────────────────────────────────────────────

    public async Task<List<int>> GetFeatureIdsByRoleAsync(int roleId)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<int>(
            "SELECT FeatureID FROM RoleFeature WHERE RoleID = @RoleID", new { RoleID = roleId });
        return rows.ToList();
    }

    public async Task SaveRoleFeatureAsync(int roleId, IEnumerable<int> featureIds)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync("DELETE FROM RoleFeature WHERE RoleID = @RoleID",
            new { RoleID = roleId }, tx);

        foreach (var fid in featureIds)
            await conn.ExecuteAsync(
                "INSERT INTO RoleFeature (RoleID, FeatureID) VALUES (@RoleID, @FeatureID)",
                new { RoleID = roleId, FeatureID = fid }, tx);

        await tx.CommitAsync();
    }

    // ── MenuFeature ──────────────────────────────────────────────────

    public async Task<List<MenuFeatureVM>> GetMenuByRoleAsync(int roleId)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<MenuFeatureVM>(
            "SELECT RoleID, FeatureID, PhraseCode, Path, [Order], ParentID, IconClass FROM MenuFeature WHERE RoleID = @RoleID ORDER BY [Order]",
            new { RoleID = roleId });
        return rows.ToList();
    }

    public async Task SaveMenuAsync(int roleId, List<MenuFeatureVM> items)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync("DELETE FROM MenuFeature WHERE RoleID = @RoleID",
            new { RoleID = roleId }, tx);

        foreach (var item in items)
            await conn.ExecuteAsync(
                """
                INSERT INTO MenuFeature (RoleID, FeatureID, PhraseCode, Path, [Order], ParentID, IconClass)
                VALUES (@RoleID, @FeatureID, @PhraseCode, @Path, @Order, @ParentID, @IconClass)
                """, item, tx);

        await tx.CommitAsync();
    }

    // ── Password ─────────────────────────────────────────────────────

    public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword)
    {
        await using var conn = Conn();
        var row = await conn.QueryFirstOrDefaultAsync<(int UserId, string Hash)>(
            """
            SELECT up.UserId, m.Password AS Hash
            FROM UserProfile up
            JOIN webpages_Membership m ON m.UserId = up.UserId
            WHERE up.UserName = @UserName
            """, new { UserName = username });

        if (row == default) return false;
        if (!VerifyPassword(oldPassword, row.Hash)) return false;

        string newHash = HashPassword(newPassword);
        await conn.ExecuteAsync(
            "UPDATE webpages_Membership SET Password = @Hash WHERE UserId = @UserId",
            new { Hash = newHash, row.UserId });
        return true;
    }

    public async Task<bool> ResetPasswordByTokenAsync(string token, string newPassword)
    {
        await using var conn = Conn();
        var userId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT UserId FROM webpages_Membership WHERE ConfirmationToken = @Token",
            new { Token = token });
        if (userId is null) return false;

        string newHash = HashPassword(newPassword);
        await conn.ExecuteAsync(
            "UPDATE webpages_Membership SET Password = @Hash, IsConfirmed = 1, ConfirmationToken = NULL WHERE UserId = @UserId",
            new { Hash = newHash, UserId = userId });
        return true;
    }

    public async Task<string?> GetUserNameByTokenAsync(string token)
    {
        await using var conn = Conn();
        return await conn.QueryFirstOrDefaultAsync<string>(
            """
            SELECT up.UserName FROM UserProfile up
            JOIN webpages_Membership m ON m.UserId = up.UserId
            WHERE m.ConfirmationToken = @Token
            """, new { Token = token });
    }

    // ── Crypto helpers (same as AuthService) ─────────────────────────

    private static bool VerifyPassword(string plain, string stored)
    {
        try
        {
            var bytes = Convert.FromBase64String(stored);
            if (bytes.Length != 49 || bytes[0] != 0x00) return false;
            var salt = bytes[1..17];
            var expected = bytes[17..];
            var actual = Pbkdf2Sha1(plain, salt, 1000, 32);
            int diff = 0;
            for (int i = 0; i < actual.Length; i++) diff |= actual[i] ^ expected[i];
            return diff == 0;
        }
        catch { return false; }
    }

    private static string HashPassword(string plain)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = Pbkdf2Sha1(plain, salt, 1000, 32);
        var bytes = new byte[49];
        bytes[0] = 0x00;
        salt.CopyTo(bytes, 1);
        hash.CopyTo(bytes, 17);
        return Convert.ToBase64String(bytes);
    }

    private static byte[] Pbkdf2Sha1(string password, byte[] salt, int iterations, int outputBytes)
    {
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            password, salt, iterations, System.Security.Cryptography.HashAlgorithmName.SHA1);
        return pbkdf2.GetBytes(outputBytes);
    }
}
