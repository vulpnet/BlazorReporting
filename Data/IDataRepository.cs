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
}

public sealed record SalesmanLocation(
    string   UserName,
    DateTime Checktime,
    double   Lattitude,
    double   Longtitude);
