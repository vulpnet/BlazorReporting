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
    public string RowField { get; set; } = string.Empty;

    /// <summary>One or more column fields — composite key = values joined with "|||".</summary>
    public List<string> ColumnFields { get; set; } = [];

    /// <summary>One or more value/aggregation pairs.</summary>
    public List<PivotValueDef> ValueFields { get; set; } = [new()];

    // Display toggles
    public bool ShowRowTotals  { get; set; } = true;
    public bool ShowColTotals  { get; set; } = true;
    public bool ShowGrandTotal { get; set; } = true;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RowField) &&
        ColumnFields.Any(f => !string.IsNullOrWhiteSpace(f)) &&
        !ColumnFields.Any(f => f == RowField) &&
        ValueFields.Any(v => !string.IsNullOrWhiteSpace(v.Field));

    public IReadOnlyList<PivotValueDef> ValidValues =>
        ValueFields.Where(v => !string.IsNullOrWhiteSpace(v.Field)).ToList();

    public IReadOnlyList<string> ValidColumnFields =>
        ColumnFields.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();

    /// <summary>Composite column key separator (internal, not shown in UI).</summary>
    public const string ColKeySep = "|||";

    /// <summary>Build composite key from multiple column field values.</summary>
    public static string MakeColKey(IEnumerable<string> parts) =>
        string.Join(ColKeySep, parts);

    /// <summary>Split composite key back into individual field values.</summary>
    public static string[] SplitColKey(string compositeKey) =>
        compositeKey.Split(ColKeySep, StringSplitOptions.None);

    /// <summary>User-facing display of a composite column key.</summary>
    public static string DisplayColKey(string compositeKey) =>
        compositeKey.Replace(ColKeySep, " / ");

    public PivotConfig Clone()
    {
        var c = (PivotConfig)MemberwiseClone();
        c.ColumnFields = [.. ColumnFields];
        c.ValueFields  = ValueFields.Select(v => v.Clone()).ToList();
        return c;
    }
}
