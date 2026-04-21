namespace BlazorReporting.Models;

public sealed class PivotResult
{
    public IReadOnlyList<string>        RowKeys   { get; init; } = [];
    public IReadOnlyList<string>        ColumnKeys { get; init; } = [];
    public IReadOnlyList<PivotValueDef> ValueDefs  { get; init; } = [];

    /// <summary>Data[rowKey][colKey][valueDef.Label] = aggregated value (null = no data)</summary>
    public Dictionary<string, Dictionary<string, Dictionary<string, object?>>> Data { get; init; } = new();

    /// <summary>RowTotals[rowKey][valueDef.Label] = row sub-total</summary>
    public Dictionary<string, Dictionary<string, object?>> RowTotals { get; init; } = new();

    /// <summary>ColTotals[colKey][valueDef.Label] = column sub-total</summary>
    public Dictionary<string, Dictionary<string, object?>> ColTotals { get; init; } = new();

    /// <summary>GrandTotals[valueDef.Label] = grand total</summary>
    public Dictionary<string, object?> GrandTotals { get; init; } = new();

    public static readonly PivotResult Empty = new();
}
