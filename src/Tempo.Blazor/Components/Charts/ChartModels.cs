namespace Tempo.Blazor.Components.Charts;

/// <summary>Chart type.</summary>
public enum ChartType
{
    /// <summary>Vertical bar chart.</summary>
    Bar,
    /// <summary>Line chart.</summary>
    Line,
    /// <summary>Pie chart.</summary>
    Pie,
    /// <summary>Donut chart.</summary>
    Donut,
    /// <summary>Horizontal bar chart.</summary>
    HorizontalBar,
    /// <summary>Conversion funnel: stacked centered trapezoids with per-stage conversion.</summary>
    Funnel,
    /// <summary>Intensity matrix: dataset rows × label columns (e.g. day × hour).</summary>
    Heatmap,
    /// <summary>Squarified share tiles; hierarchical when <see cref="ChartTreeNode"/> data is supplied.</summary>
    Treemap
}

/// <summary>Data for TmChart.</summary>
public sealed record ChartData
{
    /// <summary>Category labels (X-axis for bar/line).</summary>
    public required string[] Labels { get; init; }

    /// <summary>One or more datasets.</summary>
    public required ChartDataset[] Datasets { get; init; }
}

/// <summary>A single dataset within chart data.</summary>
public sealed record ChartDataset
{
    /// <summary>Dataset label (used in legend).</summary>
    public required string Label { get; init; }

    /// <summary>Data values.</summary>
    public required double[] Values { get; init; }

    /// <summary>Stroke/border color.</summary>
    public required string Color { get; init; }

    /// <summary>Fill color (defaults to Color with opacity).</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// Optional per-value fill colors. When provided and contains a value for the index,
    /// overrides BackgroundColor/Color for individual bars or pie/donut slices.
    /// </summary>
    public IReadOnlyList<string>? BackgroundColors { get; init; }
}

/// <summary>
/// One node of a hierarchical treemap. Leaves carry <see cref="Value"/>; a parent's size
/// is the sum of its children. Top-level nodes render as groups with a header strip.
/// </summary>
public sealed record ChartTreeNode
{
    /// <summary>Display label of the node.</summary>
    public required string Label { get; init; }

    /// <summary>Leaf value. Ignored (children sum wins) when <see cref="Children"/> exist.</summary>
    public double Value { get; init; }

    /// <summary>Fill color; defaults to the chart palette by top-level index.</summary>
    public string? Color { get; init; }

    /// <summary>Child nodes, when this node is a group.</summary>
    public IReadOnlyList<ChartTreeNode>? Children { get; init; }
}

/// <summary>Identifies a clicked segment.</summary>
public sealed record ChartSegment(int DatasetIndex, int Index, string Label, double Value);

/// <summary>
/// Raised when a series (multi-dataset legend) or a single value (per-value legend) is
/// toggled through the interactive legend. <see cref="Index"/> is the legend-item index.
/// </summary>
public sealed record ChartSeriesToggle(int Index, string Label, bool Hidden);

/// <summary>Context passed to a chart tooltip — the default tooltip and any custom TooltipTemplate.</summary>
public sealed record ChartTooltipContext(
    int DatasetIndex,
    int Index,
    string Label,
    double Value,
    string DatasetLabel,
    string Color);
