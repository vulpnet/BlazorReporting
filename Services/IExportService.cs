using BlazorReporting.Models;

namespace BlazorReporting.Services;

public interface IExportService
{
    Task<byte[]> ToExcelAsync(IReadOnlyList<Dictionary<string, object?>> data, string title);
    Task<byte[]> ToExcelPivotAsync(PivotResult pivot, PivotConfig config, string title);
    byte[] ToCsv(IReadOnlyList<Dictionary<string, object?>> data);
    byte[] ToCsvPivot(PivotResult pivot, PivotConfig config);
    byte[] ToPdf(IReadOnlyList<Dictionary<string, object?>> data, string title);
    byte[] ToPdfPivot(PivotResult pivot, PivotConfig config, string title);
}
