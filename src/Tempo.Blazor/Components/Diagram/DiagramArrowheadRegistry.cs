namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Registry of SVG arrowhead definitions for diagram edges.
/// Maps arrowhead identifiers to SVG path data and default sizes.
/// </summary>
public static class DiagramArrowheadRegistry
{
    public sealed class ArrowheadDef
    {
        public string PathData { get; init; } = "";
        public string? ExtraPath { get; init; }
        public string FillMode { get; init; } = "filled"; // filled, empty, line
        public double Width { get; init; } = 10;
        public double Height { get; init; } = 10;
        public double RefX { get; init; } = 9;
        public double RefY { get; init; } = 5;
    }

    private static readonly Dictionary<string, ArrowheadDef> _defs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = new()
        {
            PathData = "",
            FillMode = "none",
            Width = 0,
            Height = 0,
            RefX = 0,
            RefY = 0,
        },
        ["classic"] = new()
        {
            PathData = "M0,0 L0,10 L9,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 9,
            RefY = 5,
        },
        ["block"] = new()
        {
            PathData = "M0,0 L0,10 L10,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["open"] = new()
        {
            PathData = "M0,0 L10,5 L0,10",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["oval"] = new()
        {
            PathData = "M0,5 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["diamond"] = new()
        {
            PathData = "M0,5 L5,0 L10,5 L5,10 z",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["async"] = new()
        {
            PathData = "M0,0 L10,5 L0,10 M10,0 L10,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["dash"] = new()
        {
            PathData = "M4,0 L4,10",
            FillMode = "line",
            Width = 8,
            Height = 10,
            RefX = 4,
            RefY = 5,
        },
        ["cross"] = new()
        {
            PathData = "M0,0 L10,10 M0,10 L10,0",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["circle"] = new()
        {
            PathData = "M0,5 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["box"] = new()
        {
            PathData = "M0,0 L10,0 L10,10 L0,10 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["double"] = new()
        {
            PathData = "M0,2 L0,8 L7,5 z",
            ExtraPath = "M4,0 L4,10",
            FillMode = "filled",
            Width = 12,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["crow"] = new()
        {
            PathData = "M0,0 L10,5 L0,10 M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["one"] = new()
        {
            PathData = "M2,0 L2,10 M6,0 L6,10",
            FillMode = "line",
            Width = 8,
            Height = 10,
            RefX = 8,
            RefY = 5,
        },
        ["many"] = new()
        {
            PathData = "M0,10 L5,0 L10,10 M5,0 L5,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["zero-one"] = new()
        {
            PathData = "M0,5 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0 M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["zero-many"] = new()
        {
            PathData = "M0,5 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0 M8,10 L11,0 L14,10 M11,0 L11,10",
            FillMode = "line",
            Width = 14,
            Height = 10,
            RefX = 14,
            RefY = 5,
        },
        ["classicThin"] = new()
        {
            PathData = "M0,1 L0,9 L7,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 7,
            RefY = 5,
        },
        ["openThin"] = new()
        {
            PathData = "M0,1 L7,5 L0,9",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 7,
            RefY = 5,
        },
        ["blockThin"] = new()
        {
            PathData = "M0,1 L0,9 L7,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 7,
            RefY = 5,
        },
        ["openAsync"] = new()
        {
            PathData = "M0,1 L8,5 L0,9",
            ExtraPath = "M8,0 L8,10",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 8,
            RefY = 5,
        },
        ["halfCircle"] = new()
        {
            PathData = "M0,0 A5,5 0 0,1 0,10",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 5,
            RefY = 5,
        },
        ["circlePlus"] = new()
        {
            PathData = "M0,5 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0",
            ExtraPath = "M5,2 L5,8 M2,5 L8,5",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["baseDash"] = new()
        {
            PathData = "M-3,5 L3,5",
            FillMode = "line",
            Width = 8,
            Height = 10,
            RefX = 0,
            RefY = 5,
        },
        ["doubleBlock"] = new()
        {
            PathData = "M0,2 L5,2 L5,8 L0,8 z M5,2 L10,2 L10,8 L5,8 z",
            FillMode = "empty",
            Width = 12,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["doubleBlockFilled"] = new()
        {
            PathData = "M0,2 L5,2 L5,8 L0,8 z M5,2 L10,2 L10,8 L5,8 z",
            FillMode = "filled",
            Width = 12,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["ERone"] = new()
        {
            PathData = "M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 8,
            RefY = 5,
        },
        ["ERmandOne"] = new()
        {
            PathData = "M6,0 L6,10",
            ExtraPath = "M10,0 L10,10",
            FillMode = "line",
            Width = 12,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["ERmany"] = new()
        {
            PathData = "M0,0 L10,5 L0,10",
            ExtraPath = "M10,0 L10,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["ERoneToMany"] = new()
        {
            PathData = "M0,0 L10,5 L0,10",
            ExtraPath = "M6,0 L6,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["ERzeroToOne"] = new()
        {
            PathData = "M0,5 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0",
            ExtraPath = "M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
        ["ERzeroToMany"] = new()
        {
            PathData = "M0,5 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0",
            ExtraPath = "M8,0 L10,5 L8,10",
            FillMode = "line",
            Width = 12,
            Height = 10,
            RefX = 10,
            RefY = 5,
        },
    };

    public static IReadOnlyDictionary<string, ArrowheadDef> Definitions => _defs;

    public static ArrowheadDef? Get(string? id)
        => id is not null && _defs.TryGetValue(id, out var def) ? def : null;

    public static bool Contains(string? id)
        => id is not null && _defs.ContainsKey(id);

    /// <summary>
    /// Returns whether the arrowhead supports fill/unfill toggle.
    /// Only arrowheads with FillMode "filled" or "empty" can toggle.
    /// </summary>
    public static bool CanToggleFill(string? arrowhead)
    {
        if (arrowhead is null) return false;
        var def = Get(arrowhead);
        return def is not null && def.FillMode is "filled" or "empty";
    }

    /// <summary>
    /// Returns the default fill state for an arrowhead based on its FillMode.
    /// </summary>
    public static bool GetDefaultFill(string? arrowhead)
    {
        var def = Get(arrowhead);
        return def?.FillMode == "filled";
    }

    /// <summary>
    /// Returns the effective fill state combining user override with arrowhead default.
    /// </summary>
    public static bool GetEffectiveFill(string? arrowhead, bool? userFill)
    {
        if (userFill.HasValue) return userFill.Value;
        return GetDefaultFill(arrowhead);
    }

    /// <summary>
    /// Generates a unique marker identifier based on arrowhead, color, size and fill state.
    /// </summary>
    public static string GetMarkerId(string arrowhead, string color, double size, bool fill)
    {
        var safeColor = color.TrimStart('#').Replace(";", "");
        return $"arrow-{arrowhead}-{safeColor}-{size.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{(fill ? "f" : "e")}";
    }

    /// <summary>
    /// Generates a unique marker identifier based on arrowhead, color and size (uses default fill).
    /// </summary>
    public static string GetMarkerId(string arrowhead, string color, double size)
        => GetMarkerId(arrowhead, color, size, GetDefaultFill(arrowhead));
}
