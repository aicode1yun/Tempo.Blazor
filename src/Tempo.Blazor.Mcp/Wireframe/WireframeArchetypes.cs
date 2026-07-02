namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>
/// Canonical wireframe scaffold archetypes. Slot dimensions are intentionally absent here;
/// the operation engine resolves W/H from the active <c>WireframeSchemaRegistry</c>.
/// </summary>
public static class WireframeArchetypes
{
    public const double DesktopWidth = 1440;
    public const double MobileWidth = 390;
    public const double DesktopHeight = 1100;
    public const double MobileHeight = 1600;
    public const double NavbarHeight = 64;
    public const double SectionSpacing = 80;

    public sealed record SlotSpec(string Region, string Type, double X, double Y, int? WSpan = null);

    private static readonly SlotSpec[] Landing =
    [
        new("navbar", "TmMenu", 40, 24),
        new("hero", "TmText", 80, 240),
        new("hero", "TmButton", 80, 304),
        new("hero", "TmCard", 760, 220),
        new("featureGrid", "TmCard", 80, 480),
        new("featureGrid", "TmCard", 400, 480),
        new("featureGrid", "TmCard", 720, 480),
        new("footer", "TmDivider", 80, 760)
    ];

    private static readonly SlotSpec[] List =
    [
        new("navbar", "TmMenu", 40, 24),
        new("header", "TmText", 300, 64),
        new("actions", "TmButton", 1120, 64),
        new("list", "TmCard", 300, 160),
        new("list", "TmCard", 300, 372),
        new("detailRail", "TmCard", 920, 160)
    ];

    private static readonly SlotSpec[] Detail =
    [
        new("navbar", "TmMenu", 40, 24),
        new("title", "TmText", 300, 64),
        new("summary", "TmCard", 300, 144),
        new("metadata", "TmStatCard", 920, 144),
        new("content", "TmCard", 300, 360),
        new("actions", "TmButton", 300, 600)
    ];

    private static readonly SlotSpec[] Form =
    [
        new("navbar", "TmMenu", 40, 24),
        new("title", "TmText", 300, 64),
        new("form", "TmCard", 300, 144),
        new("form", "TmCard", 620, 144),
        new("actions", "TmButton", 300, 380),
        new("actions", "TmButton", 440, 380)
    ];

    private static readonly SlotSpec[] Dashboard =
    [
        new("sidebar", "TmSidebar", 40, 40),
        new("header", "TmText", 300, 64),
        new("stats", "TmStatCard", 300, 144),
        new("stats", "TmStatCard", 492, 144),
        new("stats", "TmStatCard", 684, 144),
        new("main", "TmCard", 300, 284),
        new("main", "TmCard", 620, 284)
    ];

    private static readonly SlotSpec[] Auth =
    [
        new("brand", "TmText", 520, 120),
        new("authCard", "TmCard", 520, 200),
        new("primaryAction", "TmButton", 560, 430),
        new("secondaryAction", "TmButton", 700, 430),
        new("footer", "TmDivider", 520, 540)
    ];

    public static IReadOnlyList<string> Names { get; } =
    [
        "landing",
        "list",
        "detail",
        "form",
        "dashboard",
        "auth"
    ];

    public static IReadOnlyList<SlotSpec> Slots(string archetype)
        => TrySlots(archetype, out var slots)
            ? slots
            : throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Unknown scaffold archetype.");

    internal static bool TrySlots(string? archetype, out IReadOnlyList<SlotSpec> slots)
    {
        slots = archetype?.Trim().ToLowerInvariant() switch
        {
            "landing" => Landing,
            "list" => List,
            "detail" => Detail,
            "form" => Form,
            "dashboard" => Dashboard,
            "auth" => Auth,
            _ => []
        };

        return slots.Count > 0;
    }
}
