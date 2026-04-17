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
    };

    public static IReadOnlyDictionary<string, ArrowheadDef> Definitions => _defs;

    public static ArrowheadDef? Get(string? id)
        => id is not null && _defs.TryGetValue(id, out var def) ? def : null;

    public static bool Contains(string? id)
        => id is not null && _defs.ContainsKey(id);

    /// <summary>
    /// Generates a unique marker identifier based on arrowhead, color and size.
    /// </summary>
    public static string GetMarkerId(string arrowhead, string color, double size)
    {
        var safeColor = color.TrimStart('#').Replace(";", "");
        return $"arrow-{arrowhead}-{safeColor}-{size.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
