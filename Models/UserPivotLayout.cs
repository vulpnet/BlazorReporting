namespace BlazorReporting.Models;

public sealed class UserPivotLayout
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string LayoutName { get; set; } = "Default";
    public string LayoutJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
