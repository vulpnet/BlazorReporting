using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

/// <summary>
/// Quản lý phân quyền truy cập Route theo Role.
/// Dùng lại table Feature (field Page = route path) + RoleFeature.
/// </summary>
public sealed class RoutePermissionService
{
    private readonly string _cs;

    // Danh sách tất cả routes của WEBAPP (seed vào Feature nếu chưa có)
    public static readonly IReadOnlyList<RouteDefinition> AllRoutes = new List<RouteDefinition>
    {
        // Tổng quan
        new("/dashboard",              "Dashboard",                 "Tổng quan"),
        // Báo cáo
        new("/report/visit",           "Báo cáo viếng thăm",        "Báo cáo"),
        new("/report/summary",         "Tổng hợp thăm SM",          "Báo cáo"),
        new("/report/summary-sales",   "Tổng hợp doanh số",         "Báo cáo"),
        new("/report/sales-effective", "Hiệu quả bán hàng",         "Báo cáo"),
        new("/report/sales-daily",     "Doanh số ngày MTD",         "Báo cáo"),
        new("/report/daily-plan",      "Kế hoạch ngày",             "Báo cáo"),
        new("/report/pc-sm",           "Báo cáo PCSM",              "Báo cáo"),
        new("/report/3g-offline",      "Báo cáo 3G Offline",        "Báo cáo"),
        new("/report/outlet-invalid-location", "Outlet GPS không hợp lệ", "Báo cáo"),
        new("/report/issues-full",     "Báo cáo vấn đề",            "Báo cáo"),
        new("/report/user-log",        "Nhật ký người dùng",        "Báo cáo"),
        new("/report/timekeeping",     "Chấm công",                 "Báo cáo"),
        new("/report/visit-reason",    "Lý do thăm viếng",          "Báo cáo"),
        new("/report/outlet-not-visit-mcp", "CH không thăm MCP",   "Báo cáo"),
        new("/report/user-mobility",   "Di chuyển nhân viên",       "Báo cáo"),
        new("/report/display-image",   "Hình ảnh trưng bày",        "Báo cáo"),
        new("/report/synchronous",     "Báo cáo Đồng bộ",           "Báo cáo"),
        new("/report/review-work-with","Review Work With",          "Báo cáo"),
        new("/report/work-with",       "Work With",                 "Báo cáo"),
        new("/report/reason-visit",    "Lý do viếng thăm",          "Báo cáo"),
        new("/report/visit-distance",  "Khoảng cách viếng thăm",    "Báo cáo"),
        new("/report/new-outlet",      "Outlet mới",                "Báo cáo"),
        new("/report/update-outlet",   "Outlet cập nhật",           "Báo cáo"),
        new("/report/adoption",        "Adoption",                  "Báo cáo"),
        new("/report/golden-store",    "Golden Store",              "Báo cáo"),
        new("/report/general-manager", "General Manager",           "Báo cáo"),
        new("/report/inday-dsr",       "InDay DSR",                 "Báo cáo"),
        new("/report/sales-fundamentals","Sales Fundamentals",      "Báo cáo"),
        new("/report/evaluation",      "Báo cáo Evaluation",        "Báo cáo"),
        new("/report/eval-reason",     "Lý do Evaluation",          "Báo cáo"),
        new("/report/eval-inventory",  "Tồn kho Evaluation",        "Báo cáo"),
        new("/report/review-order",    "Duyệt đơn hàng",            "Báo cáo"),
        // Phân phối
        new("/distribution",           "Phân phối",                 "Phân phối"),
        new("/distribution/budget",    "Phân bổ ngân sách",         "Phân phối"),
        // MCP
        new("/mcp/report",             "Báo cáo MCP",               "MCP"),
        new("/mcp/detail",             "Chi tiết MCP",              "MCP"),
        // Issues
        new("/issues",                 "Danh sách sự cố",           "Issues"),
        new("/issues/tasks",           "Quản lý Task",              "Issues"),
        new("/issues/digital",         "Nội dung số",               "Issues"),
        new("/issues/pda-active",      "PDA Salesman Active",       "Issues"),
        new("/issues/report-usage-app","Báo cáo sử dụng App",       "Issues"),
        // Tracking / Phân tích
        new("/tracking",               "Theo dõi NVBH",             "Tracking"),
        new("/territory-map",          "Territory Map",             "Phân tích"),
        new("/product-sales",          "Product Sales",             "Phân tích"),
        new("/sales-strategy",         "Chiến lược bán hàng",       "Phân tích"),
        // Quản trị
        new("/user",                   "Danh sách người dùng",      "Quản trị"),
        new("/user/action-log-territory","Log hành động khu vực",   "Quản trị"),
        new("/account/roles",          "Quản lý Role",              "Quản trị"),
        new("/account/role-features",  "Phân quyền Feature",        "Quản trị"),
        new("/account/change-password","Đổi mật khẩu",              "Quản trị"),
        new("/admin/security",         "Security Dashboard",        "Quản trị"),
        new("/admin/route-permission", "Phân quyền Route",          "Quản trị"),
        // Cấu hình
        new("/config/settings",        "Cấu hình hệ thống",         "Cấu hình"),
        new("/config/system",          "Tham số hệ thống",          "Cấu hình"),
        new("/config/shift",           "Ca làm việc",               "Cấu hình"),
        new("/config/schedule-submit", "Mở/Đóng lịch",              "Cấu hình"),
        new("/config/phrase",          "Quản lý cụm từ",            "Cấu hình"),
    };

    // Routes không cần check quyền (public)
    private static readonly HashSet<string> _publicRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/account/change-password", "/account/reset-password", "/help"
    };

    public RoutePermissionService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    /// <summary>
    /// Seed tất cả routes vào Feature table nếu chưa có.
    /// Mặc định gán Role Admin vào tất cả routes (Admin = account support có quyền vào hết).
    /// </summary>
    public async Task SeedRoutesAsync()
    {
        await using var conn = Conn();

        // Lấy ID của role Admin
        var adminRoleId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT ID FROM Role WHERE RoleName = 'Admin'");

        foreach (var r in AllRoutes)
        {
            var featureId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT ID FROM Feature WHERE Page = @Page AND [Action]='Route'", new { Page = r.Path });

            if (featureId is null)
            {
                // Insert feature mới
                featureId = await conn.QueryFirstOrDefaultAsync<int>(
                    "INSERT INTO Feature (FeatureName, PhraseCode, [Group], Page, [Action]) OUTPUT INSERTED.ID VALUES (@Name, @Name, @Group, @Page, 'Route')",
                    new { Name = r.Name, Group = r.Group, Page = r.Path });

                // Gán Admin mặc định
                if (adminRoleId.HasValue && featureId.HasValue)
                {
                    var already = await conn.QueryFirstOrDefaultAsync<int>(
                        "SELECT COUNT(1) FROM RoleFeature WHERE RoleID=@RID AND FeatureID=@FID",
                        new { RID = adminRoleId.Value, FID = featureId.Value });
                    if (already == 0)
                        await conn.ExecuteAsync(
                            "INSERT INTO RoleFeature (RoleID, FeatureID) VALUES (@RID, @FID)",
                            new { RID = adminRoleId.Value, FID = featureId.Value });
                }
            }
        }
    }

    /// <summary>Lấy tất cả features loại Route</summary>
    public async Task<List<RouteFeatureVM>> GetRouteFeaturesAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<RouteFeatureVM>(
            "SELECT ID, FeatureName AS Name, [Group], Page AS Path FROM Feature WHERE [Action]='Route' ORDER BY [Group], FeatureName");
        return rows.ToList();
    }

    /// <summary>Lấy RoleID có quyền theo FeatureID</summary>
    public async Task<List<int>> GetRoleIdsByFeatureAsync(int featureId)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<int>(
            "SELECT RoleID FROM RoleFeature WHERE FeatureID = @FeatureID", new { FeatureID = featureId });
        return rows.ToList();
    }

    /// <summary>Lưu danh sách RoleID được phép cho 1 Feature</summary>
    public async Task SaveFeatureRolesAsync(int featureId, IEnumerable<int> roleIds)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("DELETE FROM RoleFeature WHERE FeatureID = @FeatureID",
            new { FeatureID = featureId }, tx);
        foreach (var rid in roleIds)
            await conn.ExecuteAsync(
                "INSERT INTO RoleFeature (RoleID, FeatureID) VALUES (@RoleID, @FeatureID)",
                new { RoleID = rid, FeatureID = featureId }, tx);
        await tx.CommitAsync();
    }

    /// <summary>
    /// Check role có quyền vào route không.
    /// Nếu route không có trong Feature(Action='Route') thì cho qua (chưa cấu hình = public).
    /// </summary>
    public async Task<bool> CanAccessAsync(string roleName, string path)
    {
        // Public routes không cần check
        var normalized = "/" + path.Trim('/');
        if (_publicRoutes.Contains(normalized)) return true;

        await using var conn = Conn();

        // Lấy FeatureID của route này
        var featureId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT ID FROM Feature WHERE Page = @Page AND [Action]='Route'",
            new { Page = normalized });

        // Chưa cấu hình → cho qua
        if (featureId is null) return true;

        // Check role có trong RoleFeature không
        var roleId = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT r.ID FROM Role r WHERE r.RoleName = @RoleName", new { RoleName = roleName });
        if (roleId is null) return false;

        var count = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM RoleFeature WHERE RoleID=@RoleID AND FeatureID=@FeatureID",
            new { RoleID = roleId, FeatureID = featureId });
        return count > 0;
    }
}

public sealed record RouteDefinition(string Path, string Name, string Group);

public sealed class RouteFeatureVM
{
    public int    ID    { get; set; }
    public string Name  { get; set; } = "";
    public string Group { get; set; } = "";
    public string Path  { get; set; } = "";
}
