namespace BlazorReporting.Services;

/// <summary>Link điều hướng được bot trả về trong chat response.</summary>
/// <param name="Label">Tên hiển thị trên button.</param>
/// <param name="Url">URL điều hướng (có thể kèm query params).</param>
/// <param name="SpParams">Danh sách tham số SP (tên + required/optional). Null nếu không phải SP.</param>
public sealed record ChatNavLink(
    string                     Label,
    string                     Url,
    IReadOnlyList<SpParamInfo>? SpParams = null);

/// <summary>Thông tin một tham số SP dùng trong system prompt.</summary>
public sealed record SpParamInfo(string Name, string TypeName, bool Required);
