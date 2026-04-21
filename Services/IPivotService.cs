using BlazorReporting.Models;

namespace BlazorReporting.Services;

public interface IPivotService
{
    PivotResult Build(IReadOnlyList<Dictionary<string, object?>> data, PivotConfig config);
}
