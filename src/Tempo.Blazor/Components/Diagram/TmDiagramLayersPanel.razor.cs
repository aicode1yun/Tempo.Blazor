using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>Right-side layers panel for managing diagram layers.</summary>
public partial class TmDiagramLayersPanel : ComponentBase
{
    [Parameter, EditorRequired] public DiagramDocument? Document { get; set; }
    [Parameter] public string[] SelectedIds { get; set; } = [];
    [CascadingParameter] public DiagramCommandStack? CommandStack { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? ActiveLayerId { get; set; }
    [Parameter] public EventCallback<string?> ActiveLayerIdChanged { get; set; }
    [Parameter] public EventCallback<DiagramDocument> DocumentChanged { get; set; }

    private bool _collapsed;
    private string? _editingLayerId;
    private string _editName = "";
    private ElementReference _nameInputRef;
    private string? _draggedLayerId;
    private string? _dragOverLayerId;
    private string? _defaultLayerId;
    private string? _moveTargetLayerId;

    private List<DiagramNode> SelectedNodes => SelectedIds
        .Select(id => Document?.Nodes.FirstOrDefault(n => n.Id == id))
        .Where(n => n is not null)
        .Select(n => n!)
        .ToList();

    private IEnumerable<DiagramLayer> SortedLayers =>
        Document?.Layers.OrderBy(l => l.Order).ThenBy(l => l.Name) ?? Enumerable.Empty<DiagramLayer>();

    private List<SelectOption<string>> MoveLayerOptions =>
        SortedLayers.Select(l => new SelectOption<string>(l.Id, l.Name)).ToList();

    protected override void OnParametersSet()
    {
        EnsureDefaultLayer();
        if (string.IsNullOrEmpty(ActiveLayerId) && !string.IsNullOrEmpty(_defaultLayerId))
        {
            ActiveLayerId = _defaultLayerId;
            ActiveLayerIdChanged.InvokeAsync(_defaultLayerId);
        }
    }

    private void EnsureDefaultLayer()
    {
        if (Document is null) return;
        if (Document.Layers.Count == 0)
        {
            var defaultLayer = new DiagramLayer { Name = "Default", Order = 0 };
            Document.Layers.Add(defaultLayer);
            _defaultLayerId = defaultLayer.Id;
        }
        else
        {
            _defaultLayerId = Document.Layers.OrderBy(l => l.Order).First().Id;
        }
    }

    private void ToggleCollapse() => _collapsed = !_collapsed;

    private async Task OnAddLayer()
    {
        if (Document is null || ReadOnly) return;
        var name = $"Layer {Document.Layers.Count + 1}";
        var cmd = new AddLayerCommand(Document, name);
        if (CommandStack is not null)
            CommandStack.Push(cmd);
        else
            cmd.Execute();

        await ActiveLayerIdChanged.InvokeAsync(cmd.Layer.Id);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnRemoveLayer(DiagramLayer layer)
    {
        if (Document is null || ReadOnly) return;
        var cmd = new RemoveLayerCommand(Document, layer.Id);
        if (CommandStack is not null)
            CommandStack.Push(cmd);
        else
            cmd.Execute();

        if (ActiveLayerId == layer.Id)
            await ActiveLayerIdChanged.InvokeAsync(_defaultLayerId);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnSetActive(string layerId)
    {
        if (ReadOnly) return;
        await ActiveLayerIdChanged.InvokeAsync(layerId);
    }

    private async Task OnToggleVisibility(DiagramLayer layer)
    {
        if (Document is null || ReadOnly) return;
        var cmd = new ToggleLayerVisibilityCommand(layer);
        if (CommandStack is not null)
            CommandStack.Push(cmd);
        else
            cmd.Execute();
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnToggleLock(DiagramLayer layer)
    {
        if (Document is null || ReadOnly) return;
        var cmd = new ToggleLayerLockCommand(layer);
        if (CommandStack is not null)
            CommandStack.Push(cmd);
        else
            cmd.Execute();
        await DocumentChanged.InvokeAsync(Document);
    }

    private void OnStartEdit(DiagramLayer layer)
    {
        if (ReadOnly) return;
        _editingLayerId = layer.Id;
        _editName = layer.Name;
    }

    private async Task OnNameBlur(DiagramLayer layer)
    {
        await CommitRename(layer);
    }

    private async Task OnNameKeyDown(KeyboardEventArgs e, DiagramLayer layer)
    {
        if (e.Key == "Enter")
            await CommitRename(layer);
        else if (e.Key == "Escape")
            _editingLayerId = null;
    }

    private async Task CommitRename(DiagramLayer layer)
    {
        if (_editingLayerId != layer.Id) return;
        if (!string.IsNullOrWhiteSpace(_editName) && _editName != layer.Name)
        {
            var cmd = new RenameLayerCommand(layer, _editName.Trim());
            if (CommandStack is not null)
                CommandStack.Push(cmd);
            else
                cmd.Execute();
            await DocumentChanged.InvokeAsync(Document);
        }
        _editingLayerId = null;
    }

    private void OnDragStart(DragEventArgs e, string layerId)
    {
        if (ReadOnly)
        {
            _draggedLayerId = null;
            return;
        }
        _draggedLayerId = layerId;
    }

    private void OnDragOver(DragEventArgs e)
    {
        // preventDefault handled in razor via @ondragover:preventDefault
    }

    private void OnDragEnter(DragEventArgs e, string layerId)
    {
        if (_draggedLayerId != null && _draggedLayerId != layerId)
            _dragOverLayerId = layerId;
    }

    private void OnDragLeave(DragEventArgs e)
    {
        _dragOverLayerId = null;
    }

    private async Task OnDrop(DragEventArgs e, string targetLayerId)
    {
        if (Document is null || ReadOnly || _draggedLayerId is null || _draggedLayerId == targetLayerId)
        {
            _draggedLayerId = null;
            _dragOverLayerId = null;
            return;
        }

        var layers = SortedLayers.ToList();
        var draggedIndex = layers.FindIndex(l => l.Id == _draggedLayerId);
        var targetIndex = layers.FindIndex(l => l.Id == targetLayerId);
        if (draggedIndex < 0 || targetIndex < 0)
        {
            _draggedLayerId = null;
            _dragOverLayerId = null;
            return;
        }

        var reordered = layers.ToList();
        var draggedLayer = reordered[draggedIndex];
        reordered.RemoveAt(draggedIndex);
        reordered.Insert(targetIndex, draggedLayer);

        var newOrders = reordered.Select((l, i) => (l.Id, Order: i)).ToDictionary(x => x.Id, x => x.Order);
        var cmd = new ReorderLayersCommand(Document, newOrders);
        if (CommandStack is not null)
            CommandStack.Push(cmd);
        else
            cmd.Execute();

        _draggedLayerId = null;
        _dragOverLayerId = null;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnMoveNodesToLayer(string? layerId)
    {
        if (Document is null || ReadOnly || SelectedNodes.Count == 0) return;
        var nodeIds = SelectedNodes.Select(n => n.Id).ToList();
        var cmd = new MoveNodesToLayerCommand(Document, nodeIds, layerId);
        if (CommandStack is not null)
            CommandStack.Push(cmd);
        else
            cmd.Execute();
        _moveTargetLayerId = null;
        await DocumentChanged.InvokeAsync(Document);
    }
}
