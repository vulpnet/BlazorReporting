namespace BlazorReporting.Data.Models;

public enum DataSourceType { StoredProcedure, View, Table }

public class DataSourceConfig
{
    public string Source { get; set; } = string.Empty;
    public DataSourceType SourceType { get; set; } = DataSourceType.StoredProcedure;

    /// <summary>Extra parameters for the SP (FromDate, ToDate, BranchID …).</summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    // Paging
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    // Sort
    public string? SortColumn { get; set; }
    public bool SortDescending { get; set; }

    // Column-level search (column → search-term)
    public Dictionary<string, string> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
