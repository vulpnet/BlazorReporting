using BlazorReporting.Data.Models;
using BlazorReporting.Models;

namespace BlazorReporting.Data;

public sealed record SpParameter(string Name, string TypeName, bool HasDefault);

public interface IDataRepository
{
    /// <summary>Server-side paged query. Works for SP (with optional paging params),
    /// View, and Table.</summary>
    Task<PagedResult<Dictionary<string, object?>>> FetchPagedAsync(
        DataSourceConfig config, CancellationToken ct = default);

    /// <summary>Loads ALL rows — use only for SPs without server-side paging.
    /// Reports progress via callback (rows loaded so far).</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> FetchAllAsync(
        DataSourceConfig config,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>SQL GROUP BY pivot aggregation for View/Table sources.
    /// Returns one row per (rowField, compositeColKey) — avoids full table scan in memory.
    /// Supports multiple column fields; composite key is joined with PivotConfig.ColKeySep.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> FetchPivotGroupByAsync(
        DataSourceConfig config,
        string rowField,
        IReadOnlyList<string> colFields,
        IReadOnlyList<PivotValueDef> valueDefs,
        CancellationToken ct = default);

    /// <summary>Returns column names for a source.</summary>
    Task<IReadOnlyList<string>> GetColumnsAsync(
        string source, DataSourceType sourceType, CancellationToken ct = default);

    /// <summary>Returns declared input parameters for a stored procedure.</summary>
    Task<IReadOnlyList<SpParameter>> GetSpParametersAsync(
        string spName, CancellationToken ct = default);

    /// <summary>Returns detail rows matching the drill-down equality filters.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> FetchDrillDownAsync(
        DataSourceConfig config,
        Dictionary<string, object?> drillFilters,
        CancellationToken ct = default);

    /// <summary>Vị trí cuối cùng trong ngày của từng salesman.</summary>
    Task<IReadOnlyList<SalesmanLocation>> GetSalesmanLocationsAsync(
        DateTime date, CancellationToken ct = default);

    /// <summary>Toàn bộ lộ trình di chuyển của một salesman trong ngày, theo thứ tự thời gian.</summary>
    Task<IReadOnlyList<SalesmanLocation>> GetSalesmanRouteAsync(
        string userName, DateTime date, CancellationToken ct = default);

    /// <summary>
    /// Lộ trình GPS + kết hợp đơn hàng bán tại tọa độ gần nhất (≤ 200m).
    /// Các điểm trùng tọa độ KH sẽ được gán thêm thông tin KH + doanh số.
    /// </summary>
    Task<IReadOnlyList<SalesmanRoutePoint>> GetSalesmanRouteWithSalesAsync(
        string userName, DateTime date, CancellationToken ct = default);

    /// <summary>Tổng doanh số bán theo từng SM trong ngày.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetDailySalesBySmAsync(
        DateTime date, CancellationToken ct = default);

    /// <summary>Doanh số theo khu vực (tuyến, thành phố...) trong ngày.</summary>
    Task<IReadOnlyList<SalesAreaItem>> GetSalesByAreaAsync(
        DateTime date, string groupBy = "route", CancellationToken ct = default);

    /// <summary>Danh sách UserName thuộc một nhóm cụ thể (để fly-to trên map).</summary>
    Task<IReadOnlyList<string>> GetSmsByGroupAsync(
        DateTime date, string groupBy, string groupLabel, CancellationToken ct = default);

    /// <summary>
    /// Top N sản phẩm × khu vực — dùng cho biểu đồ phân tích sản phẩm.
    /// groupBy: "route" | "province" | "sm"
    /// </summary>
    Task<IReadOnlyList<ProductAreaItem>> GetProductSalesByAreaAsync(
        int year, int? quarter, int? month,
        string groupBy = "route", int topN = 10,
        CancellationToken ct = default);

    /// <summary>Dữ liệu xu hướng theo tháng cho các sản phẩm đã chọn (để dự đoán).</summary>
    Task<IReadOnlyList<ProductTrendPoint>> GetProductTrendAsync(
        IReadOnlyList<string> products, int historyMonths = 12,
        CancellationToken ct = default);

    // ── Map filter / compare ──────────────────────────────────────

    /// <summary>Danh sách các nhóm (SS/Distributor) có salesman trong ngày để filter.</summary>
    Task<IReadOnlyList<MapGroupItem>> GetMapGroupsAsync(DateTime date, CancellationToken ct = default);

    /// <summary>Cây tổ chức SM: STLvl0→STLvl1→STLvl2→SS→Distributor→Route</summary>
    Task<IReadOnlyList<SmTreeNode>> GetSmTreeAsync(DateTime date, CancellationToken ct = default);

    /// <summary>Load vị trí SM + cây tổ chức trong 1 SP call.</summary>
    Task<SmLocationResult> GetSalesmanLocationsWithTreeAsync(DateTime date, CancellationToken ct = default);

    /// <summary>Vị trí cuối ngày của salesman thuộc nhóm cụ thể.</summary>
    Task<IReadOnlyList<SalesmanLocation>> GetSalesmanLocationsByGroupAsync(
        DateTime date, string groupType, string groupId, CancellationToken ct = default);

    /// <summary>Outlier alert: SM có vị trí xa tuyến, check-in bất thường.</summary>
    Task<IReadOnlyList<OutlierAlert>> GetOutlierAlertsAsync(DateTime date, CancellationToken ct = default);

    // ── Territory Performance Map ─────────────────────────────────

    /// <summary>Toàn bộ điểm polygon của tất cả territories.</summary>
    Task<IReadOnlyList<TerritoryPolygonPoint>> GetTerritoryPolygonsAsync(CancellationToken ct = default);

    /// <summary>Danh sách outlet có tọa độ (giới hạn maxRows để tránh quá tải browser).</summary>
    Task<IReadOnlyList<TerritoryOutlet>> GetOutletsAsync(int maxRows = 5000, CancellationToken ct = default);

    /// <summary>KPI territory theo tháng: coverage, revenue từ pp_ReportSalesAssessment.</summary>
    Task<IReadOnlyList<TerritoryKpi>> GetTerritoryKpiAsync(DateTime fromDate, CancellationToken ct = default);

    /// <summary>
    /// Lịch sử doanh số theo tháng nhóm theo Tỉnh/TP × Sản phẩm.
    /// Dùng để train ML.NET chiến lược nhập/xuất hàng theo khu vực.
    /// year/quarter/month xác định mốc cuối kỳ phân tích;
    /// historyMonths lấy lùi từ mốc đó để làm dữ liệu huấn luyện.
    /// </summary>
    Task<IReadOnlyList<ProvinceSalesHistory>> GetProvinceSalesHistoryAsync(
        int year, int? quarter, int? month,
        int historyMonths = 12, int topProducts = 20,
        CancellationToken ct = default);
}

public sealed record ProvinceSalesHistory(
    string  ProvinceCode,
    string  InventoryCD,
    string  InventoryName,
    int     Year,
    int     Month,
    long    TotalQty,
    decimal TotalAmount,
    int     OrderCount);

public sealed record SalesAreaItem(
    string  Label,        // Tên khu vực / tuyến
    decimal TotalAmount,  // Tổng doanh số
    int     OrderCount,   // Số đơn hàng
    int     SmCount);     // Số SM

public sealed record ProductAreaItem(
    string  InventoryCD,    // Mã sản phẩm
    string  InventoryName,  // Tên sản phẩm
    string  AreaLabel,      // Tên khu vực / tuyến / tỉnh
    long    TotalQty,       // Tổng số lượng bán
    decimal TotalAmount,    // Tổng doanh số (qty × đơn giá)
    int     OrderCount);    // Số đơn hàng

public sealed record ProductTrendPoint(
    string  InventoryCD,
    string  InventoryName,
    int     Year,
    int     Month,
    long    TotalQty,
    decimal TotalAmount);

public sealed record SalesmanLocation(
    string   UserName,
    DateTime Checktime,
    double   Lattitude,
    double   Longtitude);

/// <summary>Đơn hàng tại một điểm khách hàng.</summary>
public sealed record CustomerVisit(
    string   CustomerCD,
    string   LocationName,
    string   RouteCode,
    decimal  OrderAmount,
    DateTime OrderDate,
    DateTime? EndTime = null,
    string   Address  = "");

/// <summary>Điểm GPS trên lộ trình, có thể kèm thông tin KH nếu tọa độ trùng nhau.</summary>
public sealed record SalesmanRoutePoint(
    string        UserName,
    DateTime      Checktime,
    double        Lattitude,
    double        Longtitude,
    CustomerVisit? Visit = null);

// ── Map filter / compare models ───────────────────────────────────

public sealed record MapGroupItem(string GroupType, string GroupId, string GroupName, int SmCount);

/// <summary>Node trong cây lọc SM. FilterType: stlvl0|stlvl1|stlvl2|ss|distributor|route</summary>
public sealed record SmTreeNode(
    string FilterType,
    string Id,
    string Name,
    string ParentId,
    int    SmCount);

/// <summary>Kết quả gộp: vị trí SM + cây tổ chức + lookup map</summary>
public sealed class SmLocationResult
{
    public IReadOnlyList<SalesmanLocation> Locations { get; init; } = [];
    public IReadOnlyList<SmTreeNode>       Tree      { get; init; } = [];
    /// <summary>SM → (stlvl0, stlvl1, stlvl2, ssId, distId, routeId)</summary>
    public IReadOnlyDictionary<string, SmGroupKeys> SmGroups { get; init; }
        = new Dictionary<string, SmGroupKeys>(StringComparer.OrdinalIgnoreCase);
}

public sealed record SmGroupKeys(
    string Stlvl0, string Stlvl1, string Stlvl2,
    string SsId, string DistId, string RouteId);

public sealed record OutlierAlert(
    string   UserName,
    string   AlertType,   // "early" | "late" | "far" | "idle"
    string   Description,
    double   Lat,
    double   Lng,
    DateTime Checktime);

// ── Territory Performance Map models ──────────────────────────────

public sealed record TerritoryPolygonPoint(
    string Territory,
    int    RenderOrder,
    double Lat,
    double Lng);

public sealed record TerritoryOutlet(
    string  OutletName,
    string  Address,
    string  Route,
    double  Lat,
    double  Lng);

public sealed record TerritoryKpi(
    string  TerritoryCode,
    string  TerritoryName,
    string  RegionName,
    string  AreaName,
    decimal Revenue,
    int     TotalOutlet,
    int     VisitedOutlet,
    decimal CoverageRate);   // 0–100
