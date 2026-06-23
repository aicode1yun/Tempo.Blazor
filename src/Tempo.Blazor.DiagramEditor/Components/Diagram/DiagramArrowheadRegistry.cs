namespace Tempo.Blazor.Components.Diagram;

/// <summary>Determines which point of the arrowhead sits at the line end.</summary>
public enum ArrowheadAnchor
{
    /// <summary>The base (left edge) sits at the shortened line end. The arrowhead is drawn before the node.</summary>
    Base,
    /// <summary>The tip (rightmost point) sits at the line end. The line goes to the node border.</summary>
    Tip,
    /// <summary>The visual centre sits at the line end. Used for symmetric symbols (cross, dash, etc.).</summary>
    Center,
}

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
        /// <summary>Reference X for SVG marker placement on single-line (connector) edges.
        /// For directed arrowheads this is usually the tip (Width) so the arrow is visible above the HTML node overlay.
        /// For symmetric arrowheads this is the centre (Width/2).</summary>
        public double RefX { get; init; } = 9;
        public double RefY { get; init; } = 5;
        /// <summary>Which point of the arrowhead sits at the line end.
        /// Base = left edge at shortened line end (arrowhead drawn before node).
        /// Tip = rightmost point at node border (line goes to node).
        /// Center = visual centre on the line axis.</summary>
        public ArrowheadAnchor Anchor { get; init; } = ArrowheadAnchor.Base;
        /// <summary>For double-line (link) edges: how much to shorten the line in px per 10 units of size.
        /// 0 = line goes to the node border; 0.9 = line is shortened by 90% of arrowhead length.</summary>
        public double LinkInset { get; init; } = 0;
        /// <summary>If true, the arrowhead should be centred on the axis (used for "link" double-line).
        /// The transform is shifted by (Width/2 - RefX) so the visual centre lands on the line axis.</summary>
        public bool IsSymmetric { get; init; }
        /// <summary>If false, the arrowhead is hidden from the dropdown when the edge shape is "link".
        /// Arrowheads that look poor on double-lines (box, doubleBlock, ER notations, etc.) are excluded.</summary>
        public bool IsSupportedForDoubleLine { get; init; } = true;
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
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 0.9,
            IsSupportedForDoubleLine = true,
        },
        ["block"] = new()
        {
            PathData = "M0,0 L0,10 L10,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 0.9,
            IsSupportedForDoubleLine = true,
        },
        ["open"] = new()
        {
            PathData = "M0,0 L10,5 L0,10",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            LinkInset = 0.9,
            IsSupportedForDoubleLine = true,
        },
        ["oval"] = new()
        {
            PathData = "M0,5 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = true,
        },
        ["diamond"] = new()
        {
            PathData = "M0,5 L5,0 L10,5 L5,10 z",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = true,
        },
        ["async"] = new()
        {
            PathData = "M0,0 L10,5 L0,10 M10,0 L10,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["dash"] = new()
        {
            PathData = "M4,0 L4,10",
            FillMode = "line",
            Width = 8,
            Height = 10,
            RefX = 4,
            RefY = 5,
            Anchor = ArrowheadAnchor.Center,
            LinkInset = 0.8,
            IsSupportedForDoubleLine = true,
        },
        ["cross"] = new()
        {
            PathData = "M0,0 L10,10 M0,10 L10,0",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Center,
            LinkInset = 1.0,
            IsSupportedForDoubleLine = true,
        },
        ["circle"] = new()
        {
            PathData = "M0,5 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = true,
        },
        ["box"] = new()
        {
            PathData = "M0,0 L10,0 L10,10 L0,10 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = false,
        },
        ["double"] = new()
        {
            PathData = "M0,2 L0,8 L7,5 z",
            ExtraPath = "M4,0 L4,10",
            FillMode = "filled",
            Width = 12,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = false,
        },
        ["crow"] = new()
        {
            PathData = "M0,0 L10,5 L0,10 M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["one"] = new()
        {
            PathData = "M2,0 L2,10 M6,0 L6,10",
            FillMode = "line",
            Width = 8,
            Height = 10,
            RefX = 8,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["many"] = new()
        {
            PathData = "M0,10 L5,0 L10,10 M5,0 L5,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["zero-one"] = new()
        {
            PathData = "M0,5 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0 M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 10,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["zero-many"] = new()
        {
            PathData = "M0,5 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0 M8,10 L11,0 L14,10 M11,0 L11,10",
            FillMode = "line",
            Width = 14,
            Height = 10,
            RefX = 14,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["classicThin"] = new()
        {
            PathData = "M0,1 L0,9 L7,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 7,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 0.7,
            IsSupportedForDoubleLine = true,
        },
        ["openThin"] = new()
        {
            PathData = "M0,1 L7,5 L0,9",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 7,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            LinkInset = 0.7,
            IsSupportedForDoubleLine = true,
        },
        ["blockThin"] = new()
        {
            PathData = "M0,1 L0,9 L7,5 z",
            FillMode = "filled",
            Width = 10,
            Height = 10,
            RefX = 7,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 0.7,
            IsSupportedForDoubleLine = true,
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
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
        ["halfCircle"] = new()
        {
            PathData = "M0,0 A5,5 0 0,1 0,10",
            FillMode = "empty",
            Width = 10,
            Height = 10,
            RefX = 5,
            RefY = 5,
            Anchor = ArrowheadAnchor.Center,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = true,
        },
        ["circlePlus"] = new()
        {
            PathData = "M0,5 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0",
            ExtraPath = "M5,2 L5,8 M2,5 L8,5",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = true,
        },
        ["baseDash"] = new()
        {
            PathData = "M-3,5 L3,5",
            FillMode = "line",
            Width = 8,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Center,
            LinkInset = 0.8,
            IsSupportedForDoubleLine = true,
        },
        ["doubleBlock"] = new()
        {
            PathData = "M0,2 L5,2 L5,8 L0,8 z M5,2 L10,2 L10,8 L5,8 z",
            FillMode = "empty",
            Width = 12,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = false,
        },
        ["doubleBlockFilled"] = new()
        {
            PathData = "M0,2 L5,2 L5,8 L0,8 z M5,2 L10,2 L10,8 L5,8 z",
            FillMode = "filled",
            Width = 12,
            Height = 10,
            RefX = 0,
            RefY = 5,
            Anchor = ArrowheadAnchor.Base,
            LinkInset = 1.3,
            IsSupportedForDoubleLine = false,
        },
        ["ERone"] = new()
        {
            PathData = "M8,0 L8,10",
            FillMode = "line",
            Width = 10,
            Height = 10,
            RefX = 8,
            RefY = 5,
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
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
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
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
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
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
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
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
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
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
            Anchor = ArrowheadAnchor.Tip,
            IsSupportedForDoubleLine = false,
        },
    };

    public static IReadOnlyDictionary<string, ArrowheadDef> Definitions => _defs;

    public static ArrowheadDef? Get(string? id)
        => id is not null && _defs.TryGetValue(id, out var def) ? def : null;

    public static bool Contains(string? id)
        => id is not null && _defs.ContainsKey(id);

    /// <summary>
    /// Returns the list of arrowhead IDs that are supported for double-line ("link") edges.
    /// </summary>
    public static IEnumerable<string> GetSupportedForDoubleLine()
        => _defs.Where(kv => kv.Value.IsSupportedForDoubleLine).Select(kv => kv.Key);

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
