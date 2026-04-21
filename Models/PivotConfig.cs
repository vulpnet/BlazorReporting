namespace BlazorReporting.Models;

public enum AggregationType { Sum, Count, Avg, Min, Max }

public sealed class PivotValueDef
{
    public string Field { get; set; } = string.Empty;
    public AggregationType Aggregation { get; set; } = AggregationType.Sum;
    public string Label => $"{Aggregation}({Field})";
    public PivotValueDef Clone() => (PivotValueDef)MemberwiseClone();
}

public sealed class PivotConfig
{
    public string RowField    { get; set; } = string.Empty;
    public string ColumnField { get; set; } = string.Empty;

    /// <summary>One or more value/aggregation pairs.</summary>
    public List<PivotValueDef> ValueFields { get; set; } = [new()];

    // Display toggles
    public bool ShowRowTotals  { get; set; } = true;
    public bool ShowColTotals  { get; set; } = true;
    public bool ShowGrandTotal { get; set; } = true;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RowField) &&
        !string.IsNullOrWhiteSpace(ColumnField) &&
        RowField != ColumnField &&
        ValueFields.Any(v => !string.IsNullOrWhiteSpace(v.Field));

    public IReadOnlyList<PivotValueDef> ValidValues =>
        ValueFields.Where(v => !string.IsNullOrWhiteSpace(v.Field)).ToList();

    public PivotConfig Clone()
    {
        var c = (PivotConfig)MemberwiseClone();
        c.ValueFields = ValueFields.Select(v => v.Clone()).ToList();
        return c;
    }
}
