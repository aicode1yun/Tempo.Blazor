using System.Linq;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Right-side properties panel for the selected diagram element.
/// </summary>
public partial class TmDiagramPropertiesPanel : ComponentBase
{
    /// <summary>Document being edited.</summary>
    [Parameter, EditorRequired] public DiagramDocument? Document { get; set; }

    /// <summary>IDs of currently selected elements.</summary>
    [Parameter] public string[] SelectedIds { get; set; } = [];

    /// <summary>Command stack to push mutations into.</summary>
    [CascadingParameter] public DiagramCommandStack? CommandStack { get; set; }

    /// <summary>Whether the editor is read-only.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised when a property mutation occurred.</summary>
    [Parameter] public EventCallback<DiagramDocument> DocumentChanged { get; set; }

    /// <summary>Raised when the user changes the routing type of an edge.</summary>
    [Parameter] public EventCallback<(string EdgeId, string Routing)> OnEdgeRoutingChanged { get; set; }

    private bool _collapsed;

    private IReadOnlyList<SelectOption<string>> _routingOptions =>
    [
        new SelectOption<string> { Value = "straight", Label = Loc["TmDiagramProperties_Routing_Straight"] },
        new SelectOption<string> { Value = "orthogonal", Label = Loc["TmDiagramProperties_Routing_Orthogonal"] },
        new SelectOption<string> { Value = "curved", Label = Loc["TmDiagramProperties_Routing_Curved"] }
    ];

    private IReadOnlyList<SelectOption<string>> _connectorTypeOptions =>
    [
        new SelectOption<string> { Value = "association", Label = Loc["TmDiagramProperties_ConnectorType_Association"] },
        new SelectOption<string> { Value = "dependency", Label = Loc["TmDiagramProperties_ConnectorType_Dependency"] },
        new SelectOption<string> { Value = "inheritance", Label = Loc["TmDiagramProperties_ConnectorType_Inheritance"] },
        new SelectOption<string> { Value = "composition", Label = Loc["TmDiagramProperties_ConnectorType_Composition"] },
        new SelectOption<string> { Value = "aggregation", Label = Loc["TmDiagramProperties_ConnectorType_Aggregation"] }
    ];

    private DiagramNode? SelectedNode => SelectedIds.Length == 1
        ? Document?.Nodes.FirstOrDefault(n => n.Id == SelectedIds[0])
        : null;

    private DiagramEdge? SelectedEdge => SelectedIds.Length == 1
        ? Document?.Edges.FirstOrDefault(e => e.Id == SelectedIds[0])
        : null;

    private IEnumerable<DiagramLayer> SortedLayers =>
        Document?.Layers.OrderBy(l => l.Order).ThenBy(l => l.Name) ?? Enumerable.Empty<DiagramLayer>();

    private void ToggleCollapse() => _collapsed = !_collapsed;

    private string GetNodeLabel() => SelectedNode?.Data.GetValueOrDefault("label")?.ToString() ?? "";
    private string GetEdgeLabel() => SelectedEdge?.Label ?? "";

    private async Task OnNodeLabelChanged(string value)
    {
        if (SelectedNode is null || CommandStack is null || Document is null) return;
        var oldData = DeepCopy(SelectedNode.Data);
        var newData = DeepCopy(SelectedNode.Data);
        newData["label"] = value;
        CommandStack.Push(new UpdateNodeDataCommand(Document, SelectedNode.Id, oldData, newData));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeXChanged(string? valueStr)
    {
        if (SelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        var before = new Dictionary<string, (double X, double Y)> { [SelectedNode.Id] = (SelectedNode.X, SelectedNode.Y) };
        var after = new Dictionary<string, (double X, double Y)> { [SelectedNode.Id] = (value, SelectedNode.Y) };
        CommandStack.Push(new MoveNodesCommand(Document, before, after));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeYChanged(string? valueStr)
    {
        if (SelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        var before = new Dictionary<string, (double X, double Y)> { [SelectedNode.Id] = (SelectedNode.X, SelectedNode.Y) };
        var after = new Dictionary<string, (double X, double Y)> { [SelectedNode.Id] = (SelectedNode.X, value) };
        CommandStack.Push(new MoveNodesCommand(Document, before, after));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeWChanged(string? valueStr)
    {
        if (SelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        CommandStack.Push(new ResizeNodeCommand(
            Document, SelectedNode.Id,
            SelectedNode.X, SelectedNode.Y, SelectedNode.W, SelectedNode.H,
            SelectedNode.X, SelectedNode.Y, value, SelectedNode.H));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeHChanged(string? valueStr)
    {
        if (SelectedNode is null || !double.TryParse(valueStr, out var value) || CommandStack is null || Document is null) return;
        CommandStack.Push(new ResizeNodeCommand(
            Document, SelectedNode.Id,
            SelectedNode.X, SelectedNode.Y, SelectedNode.W, SelectedNode.H,
            SelectedNode.X, SelectedNode.Y, SelectedNode.W, value));
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeZChanged(string? valueStr)
    {
        if (SelectedNode is null || !int.TryParse(valueStr, out var value) || Document is null) return;
        SelectedNode.ZIndex = value;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnNodeLayerChanged(ChangeEventArgs e)
    {
        if (SelectedNode is null || Document is null) return;
        var layerId = e.Value?.ToString();
        SelectedNode.LayerId = string.IsNullOrEmpty(layerId) ? null : layerId;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnEdgeLabelChanged(string value)
    {
        if (SelectedEdge is null || Document is null) return;
        if (CommandStack is not null)
            CommandStack.Push(new UpdateEdgeLabelCommand(Document, SelectedEdge.Id, SelectedEdge.Label, value));
        else
            SelectedEdge.Label = value;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task HandleEdgeRoutingChanged(string value)
    {
        if (SelectedEdge is null || Document is null) return;
        SelectedEdge.Routing = value;
        if (value == "orthogonal")
        {
            await OnEdgeRoutingChanged.InvokeAsync((SelectedEdge.Id, value));
        }
        else
        {
            SelectedEdge.Waypoints.Clear();
            await DocumentChanged.InvokeAsync(Document);
        }
    }

    private async Task HandleConnectorTypeChanged(string value)
    {
        if (SelectedEdge is null || Document is null) return;
        SelectedEdge.ConnectorType = value;
        await DocumentChanged.InvokeAsync(Document);
    }

    private static Dictionary<string, object> DeepCopy(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in source)
            result[kvp.Key] = kvp.Value;
        return result;
    }
}
