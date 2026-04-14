using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Diagram.Commands;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Mini layer panel for the diagram editor.
/// Shows layers with visibility / lock toggles, supports renaming, adding and deleting layers,
/// and provides Bring to Front / Send to Back actions for the current selection.
/// </summary>
public partial class TmDiagramLayersPanel : ComponentBase
{
    /// <summary>Document being edited.</summary>
    [Parameter] public DiagramDocument? Document { get; set; }

    /// <summary>Raised after every mutation.</summary>
    [Parameter] public EventCallback<DiagramDocument> DocumentChanged { get; set; }

    /// <summary>IDs of currently selected elements.</summary>
    [Parameter] public string[] SelectedIds { get; set; } = [];

    /// <summary>Whether the editor is read-only.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Additional CSS class on the panel wrapper.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Currently active layer for new nodes.</summary>
    [Parameter] public string? ActiveLayerId { get; set; }

    /// <summary>Raised when the active layer changes.</summary>
    [Parameter] public EventCallback<string?> ActiveLayerIdChanged { get; set; }

    /// <summary>Command stack cascaded from the parent editor.</summary>
    [CascadingParameter] public DiagramCommandStack? CommandStack { get; set; }

    private string? _editingLayerId;
    private string _editingLayerName = "";
    private readonly string _selectId = "tm-dl-select-" + Guid.NewGuid().ToString("N")[..8];

    private IEnumerable<DiagramLayer> SortedLayers =>
        Document?.Layers.OrderBy(l => l.Order).ThenBy(l => l.Name) ?? Enumerable.Empty<DiagramLayer>();

    private bool HasSelection => SelectedIds.Length > 0;

    private string? SelectedLayerId => SelectedIds.Length > 0
        ? Document?.Nodes.FirstOrDefault(n => n.Id == SelectedIds[0])?.LayerId
        : null;

    private async Task SetActiveLayer(string? layerId)
    {
        ActiveLayerId = layerId;
        await ActiveLayerIdChanged.InvokeAsync(layerId);
    }

    private async Task AddLayer()
    {
        if (Document is null || ReadOnly) return;

        var maxOrder = Document.Layers.Count > 0 ? Document.Layers.Max(l => l.Order) : 0;
        var layer = new DiagramLayer
        {
            Name = $"{Loc["TmDiagramLayers_Layer"]} {Document.Layers.Count + 1}",
            Order = maxOrder + 1
        };

        Document.Layers.Add(layer);
        await SetActiveLayer(layer.Id);
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task ToggleVisibility(DiagramLayer layer)
    {
        if (Document is null) return;
        layer.IsVisible = !layer.IsVisible;
        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task ToggleLock(DiagramLayer layer)
    {
        if (Document is null || ReadOnly) return;
        layer.IsLocked = !layer.IsLocked;
        await DocumentChanged.InvokeAsync(Document);
    }

    private void StartRename(DiagramLayer layer)
    {
        if (ReadOnly) return;
        _editingLayerId = layer.Id;
        _editingLayerName = layer.Name;
    }

    private async Task FinishRename(DiagramLayer layer)
    {
        if (_editingLayerId == layer.Id && !string.IsNullOrWhiteSpace(_editingLayerName))
        {
            layer.Name = _editingLayerName.Trim();
            await DocumentChanged.InvokeAsync(Document);
        }
        _editingLayerId = null;
    }

    private async Task OnLayerNameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && _editingLayerId is not null)
        {
            var layer = Document?.Layers.FirstOrDefault(l => l.Id == _editingLayerId);
            if (layer is not null) await FinishRename(layer);
        }
        else if (e.Key == "Escape")
        {
            _editingLayerId = null;
        }
    }

    private async Task DeleteLayer(DiagramLayer layer)
    {
        if (Document is null || ReadOnly) return;

        foreach (var node in Document.Nodes.Where(n => n.LayerId == layer.Id))
        {
            node.LayerId = null;
        }

        Document.Layers.Remove(layer);
        if (ActiveLayerId == layer.Id)
            await SetActiveLayer(null);

        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task OnAssignLayerChanged(ChangeEventArgs e)
    {
        if (Document is null || ReadOnly) return;

        var layerId = e.Value?.ToString();
        if (string.IsNullOrEmpty(layerId)) layerId = null;

        foreach (var node in Document.Nodes.Where(n => SelectedIds.Contains(n.Id)))
        {
            node.LayerId = layerId;
        }

        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task BringToFront()
    {
        if (Document is null || ReadOnly || SelectedIds.Length == 0) return;

        var maxZ = Document.Nodes.Count > 0 ? Document.Nodes.Max(n => n.ZIndex) : 0;
        var before = SelectedIds.ToDictionary(
            id => id,
            id => Document.Nodes.First(n => n.Id == id).ZIndex);
        var after = SelectedIds.ToDictionary(id => id, _ => maxZ + 1);

        if (CommandStack is not null)
            CommandStack.Push(new UpdateZIndexCommand(Document, before, after));
        else
            foreach (var node in Document.Nodes.Where(n => SelectedIds.Contains(n.Id)))
                node.ZIndex = maxZ + 1;

        await DocumentChanged.InvokeAsync(Document);
    }

    private async Task SendToBack()
    {
        if (Document is null || ReadOnly || SelectedIds.Length == 0) return;

        var minZ = Document.Nodes.Count > 0 ? Document.Nodes.Min(n => n.ZIndex) : 0;
        var before = SelectedIds.ToDictionary(
            id => id,
            id => Document.Nodes.First(n => n.Id == id).ZIndex);
        var after = SelectedIds.ToDictionary(id => id, _ => minZ - 1);

        if (CommandStack is not null)
            CommandStack.Push(new UpdateZIndexCommand(Document, before, after));
        else
            foreach (var node in Document.Nodes.Where(n => SelectedIds.Contains(n.Id)))
                node.ZIndex = minZ - 1;

        await DocumentChanged.InvokeAsync(Document);
    }
}
