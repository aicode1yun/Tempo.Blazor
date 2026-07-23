using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Tempo.Blazor.Components.Charts;

/// <summary>
/// Renders a responsive Sankey flow diagram as pure inline SVG without JavaScript dependencies.
/// </summary>
public partial class TmSankeyChart
{
    internal const double SvgWidth = 800;
    internal const double SvgHeight = 400;
    private const double LabelHorizontalPadding = 144;
    private const double CompactHorizontalPadding = 32;

    private static readonly string[] Palette =
    [
        "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6",
        "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1",
    ];

    private SankeyLayoutResult? _layout;
    private Dictionary<string, int> _nodeIndexes = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the nodes and directed value flows to render.</summary>
    [Parameter, EditorRequired]
    public SankeyData Data { get; set; } = default!;

    /// <summary>Gets or sets the CSS width of the chart container.</summary>
    [Parameter]
    public string Width { get; set; } = "100%";

    /// <summary>Gets or sets the CSS height of the chart container.</summary>
    [Parameter]
    public string Height { get; set; } = "400px";

    /// <summary>Gets or sets whether node labels are rendered.</summary>
    [Parameter]
    public bool ShowLabels { get; set; } = true;

    /// <summary>Gets or sets whether node values are appended to labels.</summary>
    [Parameter]
    public bool ShowValues { get; set; } = true;

    /// <summary>Gets or sets the node width in SVG view-box units.</summary>
    [Parameter]
    public double NodeWidth { get; set; } = 16;

    /// <summary>Gets or sets the vertical gap between nodes in the same layer.</summary>
    [Parameter]
    public double NodePadding { get; set; } = 10;

    /// <summary>Gets or sets the minimum rendered link width in SVG view-box units.</summary>
    [Parameter]
    public double MinLinkWidth { get; set; } = 1;

    /// <summary>Gets or sets the base link opacity. Values outside zero to one are clamped.</summary>
    [Parameter]
    public double LinkOpacity { get; set; } = 0.4;

    /// <summary>
    /// Gets or sets an optional value formatter. The default uses the current culture and
    /// the <c>0.##</c> numeric format.
    /// </summary>
    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    /// <summary>Gets or sets additional CSS classes applied to the chart container.</summary>
    [Parameter]
    public string? Class { get; set; }

    private string CssClass =>
        string.IsNullOrWhiteSpace(Class) ? "tm-sankey" : $"tm-sankey {Class}";

    private string WrapperStyle => $"width:{Width};height:{Height};";

    private bool IsNoData =>
        Data is null ||
        Data.Nodes is { Count: 0 } && Data.Links is { Count: 0 } ||
        Data.Nodes is { Count: > 0 } && Data.Links is { Count: 0 };

    private string LayoutErrorText =>
        _layout?.ErrorKind == SankeyLayoutErrorKind.Cycle
            ? Loc["TmSankeyChart_CycleDetected"]
            : Loc["TmSankeyChart_InvalidData"];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _layout = SankeyLayoutEngine.Layout(
            Data,
            SvgWidth,
            SvgHeight,
            NodeWidth,
            NodePadding,
            MinLinkWidth,
            horizontalPadding: ShowLabels ? LabelHorizontalPadding : CompactHorizontalPadding);

        _nodeIndexes = Data?.Nodes?
            .Select((node, index) => (node, index))
            .Where(item => item.node is not null && !string.IsNullOrWhiteSpace(item.node.Id))
            .GroupBy(item => item.node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private RenderFragment ChartContent => builder =>
    {
        if (_layout is null)
        {
            return;
        }

        BuildLinks(builder, _layout.Links);
        BuildNodes(builder, _layout.Nodes);
    };

    private void BuildLinks(RenderTreeBuilder builder, IReadOnlyList<SankeyLinkLayout> links)
    {
        for (var index = 0; index < links.Count; index++)
        {
            var layout = links[index];
            var sourceLabel = NodeLabel(layout.Link.SourceId);
            var targetLabel = NodeLabel(layout.Link.TargetId);

            builder.OpenElement(0, "path");
            builder.SetKey(layout.Link);
            builder.AddAttribute(1, "class", "tm-sankey__link");
            builder.AddAttribute(2, "data-link-index", index.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(3, "data-source-id", layout.Link.SourceId);
            builder.AddAttribute(4, "data-target-id", layout.Link.TargetId);
            builder.AddAttribute(5, "d", layout.PathData);
            builder.AddAttribute(6, "fill", "none");
            builder.AddAttribute(7, "stroke", LinkColor(layout.Link));
            builder.AddAttribute(8, "stroke-width", F(layout.Width));
            builder.AddAttribute(9, "stroke-opacity", F(NormalizedLinkOpacity));

            builder.OpenElement(10, "title");
            builder.AddContent(
                11,
                $"{sourceLabel} → {targetLabel}: {FormatValue(layout.Link.Value)}");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    private void BuildNodes(RenderTreeBuilder builder, IReadOnlyList<SankeyNodeLayout> nodes)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var layout = nodes[index];
            var color = NodeColor(layout.Node.Id);

            builder.OpenElement(0, "rect");
            builder.SetKey(layout.Node);
            builder.AddAttribute(1, "class", "tm-sankey__node");
            builder.AddAttribute(2, "data-node-id", layout.Node.Id);
            builder.AddAttribute(3, "x", F(layout.X));
            builder.AddAttribute(4, "y", F(layout.Y));
            builder.AddAttribute(5, "width", F(layout.Width));
            builder.AddAttribute(6, "height", F(layout.Height));
            builder.AddAttribute(7, "rx", "2");
            builder.AddAttribute(8, "fill", color);

            builder.OpenElement(9, "title");
            builder.AddContent(10, $"{layout.Node.Label} — {FormatValue(layout.Value)}");
            builder.CloseElement();
            builder.CloseElement();

            if (ShowLabels)
            {
                BuildLabel(builder, layout);
            }
        }
    }

    private void BuildLabel(RenderTreeBuilder builder, SankeyNodeLayout layout)
    {
        var firstLayer = layout.Layer == 0;
        var x = firstLayer ? layout.X - 8 : layout.X + layout.Width + 8;
        var label = ShowValues
            ? $"{layout.Node.Label} — {FormatValue(layout.Value)}"
            : layout.Node.Label;

        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "class", "tm-sankey__label");
        builder.AddAttribute(2, "x", F(x));
        builder.AddAttribute(3, "y", F(layout.Y + (layout.Height / 2)));
        builder.AddAttribute(4, "text-anchor", firstLayer ? "end" : "start");
        builder.AddAttribute(5, "dominant-baseline", "middle");
        builder.AddContent(6, label);
        builder.CloseElement();
    }

    private string NodeColor(string nodeId)
    {
        if (_nodeIndexes.TryGetValue(nodeId, out var index) &&
            !string.IsNullOrWhiteSpace(Data.Nodes[index].Color))
        {
            return Data.Nodes[index].Color!;
        }

        return Palette[
            _nodeIndexes.TryGetValue(nodeId, out index)
                ? index % Palette.Length
                : 0];
    }

    private string LinkColor(SankeyLink link) =>
        string.IsNullOrWhiteSpace(link.Color) ? NodeColor(link.SourceId) : link.Color;

    private string NodeLabel(string nodeId) =>
        _nodeIndexes.TryGetValue(nodeId, out var index)
            ? Data.Nodes[index].Label
            : nodeId;

    private string FormatValue(double value) =>
        ValueFormatter?.Invoke(value) ??
        value.ToString("0.##", CultureInfo.CurrentCulture);

    private double NormalizedLinkOpacity =>
        double.IsFinite(LinkOpacity) ? Math.Clamp(LinkOpacity, 0, 1) : 0.4;

    private static string F(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
