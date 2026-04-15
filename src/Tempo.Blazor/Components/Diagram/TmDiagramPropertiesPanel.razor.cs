using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Right-side properties panel for the selected diagram element(s).
/// Supports single node, single edge, and multi-selection with collapsible sections.
/// </summary>
public partial class TmDiagramPropertiesPanel : ComponentBase
{
    [Parameter, EditorRequired] public DiagramDocument? Document { get; set; }
    [Parameter] public string[] SelectedIds { get; set; } = [];
    [CascadingParameter] public DiagramCommandStack? CommandStack { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public EventCallback<DiagramDocument> DocumentChanged { get; set; }
    [Parameter] public EventCallback<(string EdgeId, string OldRouting, string NewRouting)> OnEdgeRoutingChanged { get; set; }
    [Inject] private DiagramStencilRegistry StencilRegistry { get; set; } = default!;

    private bool _collapsed;
    private readonly HashSet<string> _expandedSections = new() { "style", "text", "arrange" };

    private bool IsSingleNode => SelectedNodes.Count == 1 && SelectedEdges.Count == 0;
    private bool IsSingleEdge => SelectedNodes.Count == 0 && SelectedEdges.Count == 1;
    private bool IsMultiSelection => SelectedNodes.Count + SelectedEdges.Count > 1;
    private bool IsMultiNode => SelectedNodes.Count > 1;
    private bool IsMultiEdge => SelectedEdges.Count > 1;
    private bool IsMixedSelection => SelectedNodes.Count > 0 && SelectedEdges.Count > 0;

    private List<DiagramNode> SelectedNodes => SelectedIds
        .Select(id => Document?.Nodes.FirstOrDefault(n => n.Id == id))
        .Where(n => n is not null)
        .Select(n => n!)
        .ToList();

    private List<DiagramEdge> SelectedEdges => SelectedIds
        .Select(id => Document?.Edges.FirstOrDefault(e => e.Id == id))
        .Where(e => e is not null)
        .Select(e => e!)
        .ToList();

    private DiagramNode? FirstSelectedNode => SelectedNodes.FirstOrDefault();
    private DiagramEdge? FirstSelectedEdge => SelectedEdges.FirstOrDefault();
    private DiagramStencil? SelectedStencil => FirstSelectedNode is not null
        ? StencilRegistry.GetStencil(FirstSelectedNode.StencilId)
        : null;

    private IEnumerable<DiagramLayer> SortedLayers =>
        Document?.Layers.OrderBy(l => l.Order).ThenBy(l => l.Name) ?? Enumerable.Empty<DiagramLayer>();

    private bool CanGroup => SelectedNodes.Count > 1 && SelectedEdges.Count == 0;
    private bool CanUngroup => SelectedNodes.Count == 1 && !string.IsNullOrEmpty(FirstSelectedNode?.GroupId);
    private bool CanAlign => SelectedNodes.Count > 1 && SelectedEdges.Count == 0;
    private bool CanDistribute => SelectedNodes.Count > 2 && SelectedEdges.Count == 0;

    private void ToggleCollapse() => _collapsed = !_collapsed;
    private void ToggleSection(string section)
    {
        if (!_expandedSections.Add(section)) _expandedSections.Remove(section);
    }

    private bool IsSectionExpanded(string section) => _expandedSections.Contains(section);

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ── Mixed value helpers ──────────────────────────────────────────────────

    private static string? GetCommonString(IEnumerable<string?> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        var first = list[0];
        return list.All(v => v == first) ? first : null;
    }

    private static double? GetCommonDouble(IEnumerable<double?> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        var first = list[0];
        return list.All(v => v == first) ? first : null;
    }

    private static bool? GetCommonBool(IEnumerable<bool?> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        var first = list[0];
        return list.All(v => v == first) ? first : null;
    }

    private static bool? GetCommonBool(IEnumerable<bool> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return null;
        var first = list[0];
        return list.All(v => v == first) ? first : null;
    }

    private string GetNodeDataText(string dataKey) => FirstSelectedNode?.Data.GetValueOrDefault(dataKey)?.ToString() ?? "";

    private string GetNodeDataListText(string dataKey)
    {
        if (FirstSelectedNode?.Data.TryGetValue(dataKey, out var value) == true)
        {
            if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
                return string.Join("\n", je.EnumerateArray().Select(e => e.ToString()));
            if (value is IEnumerable<string> strs)
                return string.Join("\n", strs);
            var s = value?.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return "";
    }

    // ── Node data text/list handlers (single node only) ───────────────────────

    private async Task OnNodeDataTextChanged(string dataKey, string value)
    {
        if (FirstSelectedNode is null || CommandStack is null || Document is null) return;
        var oldData = DeepCopy(FirstSelectedNode.Data);
        var newData = DeepCopy(FirstSelectedNode.Data);
        newData[dataKey] = value;
        CommandStack.Push(new UpdateNodeDataCommand(Document, FirstSelectedNode.Id, oldData, newData));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeDataListChanged(string dataKey, string? text)
    {
        if (FirstSelectedNode is null || CommandStack is null || Document is null) return;
        var oldData = DeepCopy(FirstSelectedNode.Data);
        var newData = DeepCopy(FirstSelectedNode.Data);
        var lines = text?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrWhiteSpace(s))
                       .ToList() ?? [];
        newData[dataKey] = lines;
        CommandStack.Push(new UpdateNodeDataCommand(Document, FirstSelectedNode.Id, oldData, newData));
        await DocumentChanged.InvokeAsync(Document);
    }

    // ── Node Style properties (single + multi) ───────────────────────────────

    private string GetNodeFill() => GetCommonString(SelectedNodes.Select(n => n.Style.Fill)) ?? "#ffffff";
    private string GetNodeStroke() => GetCommonString(SelectedNodes.Select(n => n.Style.Stroke)) ?? "#111827";
    private double GetNodeStrokeWidth() => GetCommonDouble(SelectedNodes.Select(n => n.Style.StrokeWidth)) ?? 1.5;
    private double GetNodeOpacity() => (GetCommonDouble(SelectedNodes.Select(n => n.Style.Opacity)) ?? 1.0) * 100;
    private double GetNodeRadius() => GetCommonDouble(SelectedNodes.Select(n => n.Style.Radius)) ?? 0;
    private bool GetNodeShadow() => GetCommonBool(SelectedNodes.Select(n => n.Style.HasShadow)) ?? false;
    private bool IsMixedFill => GetCommonString(SelectedNodes.Select(n => n.Style.Fill)) is null && SelectedNodes.Count > 1;

    private async Task OnNodeFillChanged(ChangeEventArgs e)
    {
        var color = e.Value?.ToString() ?? "#ffffff";
        await ApplyNodeStyleAsync(s => s.Fill = color);
    }

    private async Task OnNodeStrokeChanged(ChangeEventArgs e)
    {
        var color = e.Value?.ToString() ?? "#111827";
        await ApplyNodeStyleAsync(s => s.Stroke = color);
    }

    private async Task OnNodeStrokeWidthChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        await ApplyNodeStyleAsync(s => s.StrokeWidth = v);
    }

    private async Task OnNodeOpacityChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        await ApplyNodeStyleAsync(s => s.Opacity = v / 100.0);
    }

    private async Task OnNodeRadiusChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        await ApplyNodeStyleAsync(s => s.Radius = v);
    }

    private async Task OnNodeShadowChanged(bool value)
    {
        await ApplyNodeStyleAsync(s => s.HasShadow = value);
    }

    // ── Node Text properties (single + multi) ────────────────────────────────

    private string GetNodeFontFamily() => GetCommonString(SelectedNodes.Select(n => n.Style.FontFamily)) ?? "inherit";
    private double GetNodeFontSize() => GetCommonDouble(SelectedNodes.Select(n => n.Style.FontSize)) ?? 14;
    private string GetNodeColor() => GetCommonString(SelectedNodes.Select(n => n.Style.Color)) ?? "#111827";
    private bool GetNodeBold() => GetCommonBool(SelectedNodes.Select(n => n.Style.IsBold)) ?? false;
    private bool GetNodeItalic() => GetCommonBool(SelectedNodes.Select(n => n.Style.IsItalic)) ?? false;
    private bool GetNodeUnderline() => GetCommonBool(SelectedNodes.Select(n => n.Style.IsUnderline)) ?? false;
    private string GetNodeTextAlign() => GetCommonString(SelectedNodes.Select(n => n.Style.TextAlign)) ?? "left";
    private string GetNodeVerticalAlign() => GetCommonString(SelectedNodes.Select(n => n.Style.VerticalAlign)) ?? "middle";

    private async Task OnNodeFontFamilyChanged(string value)
    {
        await ApplyNodeStyleAsync(s => s.FontFamily = value);
    }

    private async Task OnNodeFontSizeChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        await ApplyNodeStyleAsync(s => s.FontSize = v);
    }

    private async Task OnNodeColorChanged(ChangeEventArgs e)
    {
        var color = e.Value?.ToString() ?? "#111827";
        await ApplyNodeStyleAsync(s => s.Color = color);
    }

    private async Task OnNodeBoldChanged(bool value)
    {
        await ApplyNodeStyleAsync(s => s.IsBold = value);
    }

    private async Task OnNodeItalicChanged(bool value)
    {
        await ApplyNodeStyleAsync(s => s.IsItalic = value);
    }

    private async Task OnNodeUnderlineChanged(bool value)
    {
        await ApplyNodeStyleAsync(s => s.IsUnderline = value);
    }

    private async Task OnNodeTextAlignChanged(string value)
    {
        await ApplyNodeStyleAsync(s => s.TextAlign = value);
    }

    private async Task OnNodeVerticalAlignChanged(string value)
    {
        await ApplyNodeStyleAsync(s => s.VerticalAlign = value);
    }

    private async Task ApplyNodeStyleAsync(Action<DiagramStyle> mutate)
    {
        if (Document is null || SelectedNodes.Count == 0) return;
        var ids = SelectedNodes.Select(n => n.Id).ToList();
        var beforeStyles = SelectedNodes.Select(n => CloneStyle(n.Style)).ToList();
        foreach (var n in SelectedNodes) mutate(n.Style);
        var afterStyle = CloneStyle(SelectedNodes[0].Style);
        if (CommandStack is not null)
            CommandStack.Push(new UpdateNodesStyleCommand(Document, ids, beforeStyles, afterStyle));
        await DocumentChanged.InvokeAsync(Document);
    }

    private static DiagramStyle CloneStyle(DiagramStyle source) => new()
    {
        Fill = source.Fill,
        Stroke = source.Stroke,
        StrokeWidth = source.StrokeWidth,
        StrokeDasharray = source.StrokeDasharray,
        StrokeDashPattern = source.StrokeDashPattern,
        Color = source.Color,
        FontFamily = source.FontFamily,
        FontSize = source.FontSize,
        Opacity = source.Opacity,
        Radius = source.Radius,
        TextAlign = source.TextAlign,
        VerticalAlign = source.VerticalAlign,
        IsBold = source.IsBold,
        IsItalic = source.IsItalic,
        IsUnderline = source.IsUnderline,
        HasShadow = source.HasShadow,
    };

    // ── Arrange handlers ─────────────────────────────────────────────────────

    private async Task OnNodeXChanged(string? valueStr)
    {
        if (FirstSelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        var before = new Dictionary<string, (double X, double Y)> { [FirstSelectedNode.Id] = (FirstSelectedNode.X, FirstSelectedNode.Y) };
        var after = new Dictionary<string, (double X, double Y)> { [FirstSelectedNode.Id] = (value, FirstSelectedNode.Y) };
        CommandStack.Push(new MoveNodesCommand(Document, before, after));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeYChanged(string? valueStr)
    {
        if (FirstSelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        var before = new Dictionary<string, (double X, double Y)> { [FirstSelectedNode.Id] = (FirstSelectedNode.X, FirstSelectedNode.Y) };
        var after = new Dictionary<string, (double X, double Y)> { [FirstSelectedNode.Id] = (FirstSelectedNode.X, value) };
        CommandStack.Push(new MoveNodesCommand(Document, before, after));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeWChanged(string? valueStr)
    {
        if (FirstSelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        CommandStack.Push(new ResizeNodeCommand(
            Document, FirstSelectedNode.Id,
            FirstSelectedNode.X, FirstSelectedNode.Y, FirstSelectedNode.W, FirstSelectedNode.H,
            FirstSelectedNode.X, FirstSelectedNode.Y, value, FirstSelectedNode.H));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeHChanged(string? valueStr)
    {
        if (FirstSelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        CommandStack.Push(new ResizeNodeCommand(
            Document, FirstSelectedNode.Id,
            FirstSelectedNode.X, FirstSelectedNode.Y, FirstSelectedNode.W, FirstSelectedNode.H,
            FirstSelectedNode.X, FirstSelectedNode.Y, FirstSelectedNode.W, value));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeZChanged(string? valueStr)
    {
        if (FirstSelectedNode is null || !int.TryParse(valueStr, out var value) || Document is null) return;
        FirstSelectedNode.ZIndex = value;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeLayerChanged(ChangeEventArgs e)
    {
        if (FirstSelectedNode is null || Document is null) return;
        var layerId = e.Value?.ToString();
        FirstSelectedNode.LayerId = string.IsNullOrEmpty(layerId) ? null : layerId;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnBringToFront()
    {
        if (Document is null || SelectedNodes.Count == 0) return;
        var maxZ = Document.Nodes.Count > 0 ? Document.Nodes.Max(n => n.ZIndex) : 0;
        var before = SelectedNodes.ToDictionary(n => n.Id, n => n.ZIndex);
        var after = SelectedNodes.ToDictionary(n => n.Id, _ => maxZ + 1);
        if (CommandStack is not null)
            CommandStack.Push(new UpdateZIndexCommand(Document, before, after));
        foreach (var node in SelectedNodes)
            node.ZIndex = maxZ + 1;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnSendToBack()
    {
        if (Document is null || SelectedNodes.Count == 0) return;
        var minZ = Document.Nodes.Count > 0 ? Document.Nodes.Min(n => n.ZIndex) : 0;
        var before = SelectedNodes.ToDictionary(n => n.Id, n => n.ZIndex);
        var after = SelectedNodes.ToDictionary(n => n.Id, _ => minZ - 1);
        if (CommandStack is not null)
            CommandStack.Push(new UpdateZIndexCommand(Document, before, after));
        foreach (var node in SelectedNodes)
            node.ZIndex = minZ - 1;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnGroup()
    {
        if (Document is null || SelectedNodes.Count <= 1) return;
        var ids = SelectedNodes.Select(n => n.Id).ToList();
        if (CommandStack is not null)
            CommandStack.Push(new GroupNodesCommand(Document, ids));
        else
        {
            var groupId = Guid.NewGuid().ToString("N")[..8];
            foreach (var node in SelectedNodes) node.GroupId = groupId;
        }
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnUngroup()
    {
        if (Document is null || FirstSelectedNode?.GroupId is not { } groupId) return;
        if (CommandStack is not null)
            CommandStack.Push(new UngroupNodesCommand(Document, groupId));
        else
        {
            foreach (var node in Document.Nodes.Where(n => n.GroupId == groupId)) node.GroupId = null;
        }
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnAlign(string alignment)
    {
        if (Document is null || SelectedNodes.Count <= 1) return;
        var ids = SelectedNodes.Select(n => n.Id).ToList();
        if (CommandStack is not null)
            CommandStack.Push(new AlignNodesCommand(Document, ids, alignment));
        else
        {
            var cmd = new AlignNodesCommand(Document, ids, alignment);
            cmd.Execute();
        }
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnDistribute(string axis)
    {
        if (Document is null || SelectedNodes.Count <= 2) return;
        var ids = SelectedNodes.Select(n => n.Id).ToList();
        if (CommandStack is not null)
            CommandStack.Push(new DistributeNodesCommand(Document, ids, axis));
        else
        {
            var cmd = new DistributeNodesCommand(Document, ids, axis);
            cmd.Execute();
        }
        await DocumentChanged.InvokeAsync(Document);
    }

    // ── Edge properties (single + multi) ─────────────────────────────────────

    private string GetEdgeLabel() => FirstSelectedEdge?.Label ?? "";

    private async Task OnEdgeLabelChanged(string value)
    {
        if (FirstSelectedEdge is null || Document is null) return;
        if (CommandStack is not null)
            CommandStack.Push(new UpdateEdgeLabelCommand(Document, FirstSelectedEdge.Id, FirstSelectedEdge.Label, value));
        else
            FirstSelectedEdge.Label = value;
        await DocumentChanged.InvokeAsync(Document);
    }

    private string GetEdgeRouting() => GetCommonString(SelectedEdges.Select(e => e.Routing)) ?? "straight";

    private async Task HandleEdgeRoutingChanged(string value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        if (IsSingleEdge && FirstSelectedEdge is not null)
        {
            var edge = FirstSelectedEdge;
            var oldRouting = edge.Routing;
            edge.Routing = value;
            if (value is "orthogonal" or "elbow" or "segment")
                await OnEdgeRoutingChanged.InvokeAsync((edge.Id, oldRouting, value));
            else
            {
                edge.Waypoints.Clear();
                if (CommandStack is not null)
                    CommandStack.Push(new UpdateEdgeRoutingCommand(Document, edge.Id, oldRouting, value,
                        new List<DiagramPoint>(), new List<DiagramPoint>()));
                await DocumentChanged.InvokeAsync(Document);
            }
        }
        else
        {
            foreach (var edge in SelectedEdges) edge.Routing = value;
            await DocumentChanged.InvokeAsync(Document);
        }
    }

    private string GetEdgeConnectorType() => GetCommonString(SelectedEdges.Select(e => e.ConnectorType)) ?? "association";
    private bool IsMixedConnectorType => GetCommonString(SelectedEdges.Select(e => e.ConnectorType)) is null && SelectedEdges.Count > 1;

    private async Task HandleConnectorTypeChanged(string value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        ApplyEdgeStyleChange(e => e.ConnectorType = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private string GetEdgeStartArrow() => GetCommonString(SelectedEdges.Select(e => e.StartArrow)) ?? "none";
    private string GetEdgeEndArrow() => GetCommonString(SelectedEdges.Select(e => e.EndArrow)) ?? "classic";
    private string GetEdgeStrokeColor() => GetCommonString(SelectedEdges.Select(e => e.Style.Stroke)) ?? "#111827";
    private double GetEdgeStrokeWidth() => GetCommonDouble(SelectedEdges.Select(e => e.Style.StrokeWidth)) ?? 1.5;
    private double GetEdgeJumpSize() => GetCommonDouble(SelectedEdges.Select(e => e.JumpSize)) ?? 10;
    private string GetEdgeStrokeDashPattern() => GetCommonString(SelectedEdges.Select(e => e.Style.StrokeDashPattern)) ?? "";
    private string GetEdgeJumpStyle() => GetCommonString(SelectedEdges.Select(e => e.JumpStyle)) ?? "";
    private double GetEdgeSourceSpacing() => GetCommonDouble(SelectedEdges.Select(e => e.SourceSpacing)) ?? 0;
    private double GetEdgeTargetSpacing() => GetCommonDouble(SelectedEdges.Select(e => e.TargetSpacing)) ?? 0;
    private bool GetEdgeRounded() => GetCommonBool(SelectedEdges.Select(e => e.Rounded)) ?? false;

    private async Task OnEdgeStartArrowChanged(string value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        ApplyEdgeStyleChange(e => e.StartArrow = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeEndArrowChanged(string value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        ApplyEdgeStyleChange(e => e.EndArrow = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeStrokeColorChanged(ChangeEventArgs e)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        var color = e.Value?.ToString() ?? "#111827";
        ApplyEdgeStyleChange(edge => edge.Style.Stroke = color);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeStrokeWidthChanged(ChangeEventArgs e)
    {
        if (Document is null || SelectedEdges.Count == 0 || !double.TryParse(e.Value?.ToString(), out var value)) return;
        ApplyEdgeStyleChange(edge => edge.Style.StrokeWidth = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeJumpSizeChanged(ChangeEventArgs e)
    {
        if (Document is null || SelectedEdges.Count == 0 || !double.TryParse(e.Value?.ToString(), out var value)) return;
        ApplyEdgeStyleChange(edge => edge.JumpSize = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeStrokeDashPatternChanged(string value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        ApplyEdgeStyleChange(edge => edge.Style.StrokeDashPattern = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeJumpStyleChanged(string value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        ApplyEdgeStyleChange(e => e.JumpStyle = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeSourceSpacingChanged(ChangeEventArgs e)
    {
        if (Document is null || SelectedEdges.Count == 0 || !double.TryParse(e.Value?.ToString(), out var value)) return;
        ApplyEdgeStyleChange(edge => edge.SourceSpacing = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeTargetSpacingChanged(ChangeEventArgs e)
    {
        if (Document is null || SelectedEdges.Count == 0 || !double.TryParse(e.Value?.ToString(), out var value)) return;
        ApplyEdgeStyleChange(edge => edge.TargetSpacing = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeRoundedChanged(bool value)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        ApplyEdgeStyleChange(e => e.Rounded = value);
        await DocumentChanged.InvokeAsync(Document);
    }

    private void ApplyEdgeStyleChange(Action<DiagramEdge> mutate)
    {
        if (Document is null || SelectedEdges.Count == 0) return;
        if (SelectedEdges.Count == 1)
        {
            var edge = SelectedEdges[0];
            var before = DiagramEdgeStyleSnapshot.FromEdge(edge);
            mutate(edge);
            var after = DiagramEdgeStyleSnapshot.FromEdge(edge);
            if (CommandStack is not null)
                CommandStack.Push(new UpdateEdgeStyleCommand(Document, edge.Id, before, after));
        }
        else
        {
            var ids = SelectedEdges.Select(e => e.Id).ToList();
            var beforeSnapshots = SelectedEdges.Select(e => DiagramEdgeStyleSnapshot.FromEdge(e)).ToList();
            foreach (var e in SelectedEdges) mutate(e);
            var afterSnapshot = DiagramEdgeStyleSnapshot.FromEdge(SelectedEdges[0]);
            if (CommandStack is not null)
                CommandStack.Push(new UpdateEdgesStyleCommand(Document, ids, beforeSnapshots, afterSnapshot));
        }
    }

    // ── Options ──────────────────────────────────────────────────────────────

    private IReadOnlyList<SelectOption<string>> _routingOptions =>
    [
        new SelectOption<string> { Value = "straight", Label = Loc["TmDiagramProperties_Routing_Straight"] },
        new SelectOption<string> { Value = "orthogonal", Label = Loc["TmDiagramProperties_Routing_Orthogonal"] },
        new SelectOption<string> { Value = "curved", Label = Loc["TmDiagramProperties_Routing_Curved"] },
        new SelectOption<string> { Value = "elbow", Label = Loc["TmDiagramProperties_EdgeStyle_Elbow"] },
        new SelectOption<string> { Value = "segment", Label = Loc["TmDiagramProperties_EdgeStyle_Segment"] }
    ];

    private IReadOnlyList<SelectOption<string>> _connectorTypeOptions =>
    [
        new SelectOption<string> { Value = "association", Label = Loc["TmDiagramProperties_ConnectorType_Association"] },
        new SelectOption<string> { Value = "dependency", Label = Loc["TmDiagramProperties_ConnectorType_Dependency"] },
        new SelectOption<string> { Value = "inheritance", Label = Loc["TmDiagramProperties_ConnectorType_Inheritance"] },
        new SelectOption<string> { Value = "composition", Label = Loc["TmDiagramProperties_ConnectorType_Composition"] },
        new SelectOption<string> { Value = "aggregation", Label = Loc["TmDiagramProperties_ConnectorType_Aggregation"] }
    ];

    private IReadOnlyList<SelectOption<string>> _arrowheadOptions =>
    [
        new SelectOption<string> { Value = "none", Label = Loc["TmDiagramProperties_Arrowhead_None"] },
        new SelectOption<string> { Value = "classic", Label = Loc["TmDiagramProperties_Arrowhead_Classic"] },
        new SelectOption<string> { Value = "block", Label = Loc["TmDiagramProperties_Arrowhead_Block"] },
        new SelectOption<string> { Value = "open", Label = Loc["TmDiagramProperties_Arrowhead_Open"] },
        new SelectOption<string> { Value = "oval", Label = Loc["TmDiagramProperties_Arrowhead_Oval"] },
        new SelectOption<string> { Value = "diamond", Label = Loc["TmDiagramProperties_Arrowhead_Diamond"] },
        new SelectOption<string> { Value = "async", Label = Loc["TmDiagramProperties_Arrowhead_Async"] }
    ];

    private IReadOnlyList<SelectOption<string>> _dashPatternOptions =>
    [
        new SelectOption<string> { Value = "solid", Label = Loc["TmDiagramProperties_Dash_Solid"] },
        new SelectOption<string> { Value = "dashed", Label = Loc["TmDiagramProperties_Dash_Dashed"] },
        new SelectOption<string> { Value = "dotted", Label = Loc["TmDiagramProperties_Dash_Dotted"] },
        new SelectOption<string> { Value = "dash-dot", Label = Loc["TmDiagramProperties_Dash_DashDot"] }
    ];

    private IReadOnlyList<SelectOption<string>> _jumpStyleOptions =>
    [
        new SelectOption<string> { Value = "", Label = Loc["TmDiagramProperties_JumpStyle_None"] },
        new SelectOption<string> { Value = "arc", Label = Loc["TmDiagramProperties_JumpStyle_Arc"] },
        new SelectOption<string> { Value = "gap", Label = Loc["TmDiagramProperties_JumpStyle_Gap"] },
        new SelectOption<string> { Value = "sharp", Label = Loc["TmDiagramProperties_JumpStyle_Sharp"] },
        new SelectOption<string> { Value = "line", Label = Loc["TmDiagramProperties_JumpStyle_Line"] }
    ];

    private IReadOnlyList<SelectOption<string>> _fontFamilyOptions =>
    [
        new SelectOption<string> { Value = "inherit", Label = "Default" },
        new SelectOption<string> { Value = "Arial, sans-serif", Label = "Arial" },
        new SelectOption<string> { Value = "Georgia, serif", Label = "Georgia" },
        new SelectOption<string> { Value = "'Courier New', monospace", Label = "Courier New" },
        new SelectOption<string> { Value = "'Segoe UI', sans-serif", Label = "Segoe UI" },
        new SelectOption<string> { Value = "'Times New Roman', serif", Label = "Times New Roman" }
    ];

    private IReadOnlyList<SelectOption<string>> _textAlignOptions =>
    [
        new SelectOption<string> { Value = "left", Label = Loc["TmDiagramProperties_TextAlign_Left"] },
        new SelectOption<string> { Value = "center", Label = Loc["TmDiagramProperties_TextAlign_Center"] },
        new SelectOption<string> { Value = "right", Label = Loc["TmDiagramProperties_TextAlign_Right"] }
    ];

    private IReadOnlyList<SelectOption<string>> _verticalAlignOptions =>
    [
        new SelectOption<string> { Value = "top", Label = Loc["TmDiagramProperties_VerticalAlign_Top"] },
        new SelectOption<string> { Value = "middle", Label = Loc["TmDiagramProperties_VerticalAlign_Middle"] },
        new SelectOption<string> { Value = "bottom", Label = Loc["TmDiagramProperties_VerticalAlign_Bottom"] }
    ];

    private static Dictionary<string, object> DeepCopy(Dictionary<string, object> source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? [];
    }
}
