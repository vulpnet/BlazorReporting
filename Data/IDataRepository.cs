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
    /// Returns one row per (rowField, colField) — avoids full table scan in memory.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> FetchPivotGroupByAsync(
        DataSourceConfig config,
        string rowField,
        string colField,
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
}

public sealed record SalesAreaItem(
    string  Label,        // Tên khu vực / tuyến
    decimal TotalAmount,  // Tổng doanh số
    int     OrderCount,   // Số đơn hàng
    int     SmCount);     // Số SM

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
    DateTime OrderDate);

/// <summary>Điểm GPS trên lộ trình, có thể kèm thông tin KH nếu tọa độ trùng nhau.</summary>
public sealed record SalesmanRoutePoint(
    string        UserName,
    DateTime      Checktime,
    double        Lattitude,
    double        Longtitude,
    CustomerVisit? Visit = null);
