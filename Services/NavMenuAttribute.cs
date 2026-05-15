namespace BlazorReporting.Services;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NavMenuAttribute : Attribute
{
    public string Title { get; init; } = "";
    public string Icon { get; init; } = "bi-circle";
    public string Group { get; init; } = "Khác";
    public int GroupOrder { get; init; } = 100;
    public int Order { get; init; } = 100;
    public string? Role { get; init; }
    public bool OpenInNewTab { get; init; }
    public string? Badge { get; init; }
}
