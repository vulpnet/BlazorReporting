using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace BlazorReporting.Services;

public sealed record NavMenuItem(
    string Title,
    string Icon,
    string Href,
    string Group,
    int GroupOrder,
    int Order,
    string? Role,
    bool OpenInNewTab,
    string? Badge);

public sealed record NavMenuGroup(string Name, int Order, IReadOnlyList<NavMenuItem> Items);

public sealed class NavMenuService
{
    private readonly Lazy<IReadOnlyList<NavMenuItem>> _all;

    public NavMenuService()
    {
        _all = new Lazy<IReadOnlyList<NavMenuItem>>(Scan, isThreadSafe: true);
    }

    public IReadOnlyList<NavMenuGroup> GetMenu(string? userRole = null)
    {
        var visible = _all.Value
            .Where(i => i.Role is null
                        || string.Equals(i.Role, userRole, StringComparison.OrdinalIgnoreCase));

        return visible
            .GroupBy(i => (i.Group, i.GroupOrder))
            .OrderBy(g => g.Key.GroupOrder).ThenBy(g => g.Key.Group)
            .Select(g => new NavMenuGroup(
                g.Key.Group,
                g.Key.GroupOrder,
                g.OrderBy(i => i.Order).ThenBy(i => i.Title).ToList()))
            .ToList();
    }

    private static IReadOnlyList<NavMenuItem> Scan()
    {
        var asm = typeof(NavMenuService).Assembly;
        var items = new List<NavMenuItem>();

        foreach (var type in asm.GetTypes())
        {
            if (!typeof(IComponent).IsAssignableFrom(type)) continue;

            var nav = type.GetCustomAttribute<NavMenuAttribute>();
            if (nav is null) continue;

            var route = type.GetCustomAttributes<RouteAttribute>().FirstOrDefault();
            if (route is null) continue;

            // Skip parameterized routes
            var href = route.Template;
            if (href.Contains('{')) continue;

            items.Add(new NavMenuItem(
                Title:        string.IsNullOrWhiteSpace(nav.Title) ? type.Name : nav.Title,
                Icon:         nav.Icon,
                Href:         href,
                Group:        nav.Group,
                GroupOrder:   nav.GroupOrder,
                Order:        nav.Order,
                Role:         nav.Role,
                OpenInNewTab: nav.OpenInNewTab,
                Badge:        nav.Badge));
        }

        return items;
    }
}
