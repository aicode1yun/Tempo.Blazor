using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Undoable command that toggles a layer's lock state.</summary>
public sealed class ToggleLayerLockCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _layerId;
    private bool _previousValue;

    public string Name => "Toggle layer lock";

    public ToggleLayerLockCommand(WireframeDocument doc, string layerId)
    {
        _doc = doc;
        _layerId = layerId;
    }

    public void Execute()
    {
        var page = _doc.ActivePage;
        if (page is null) return;
        var layer = page.Layers.FirstOrDefault(l => l.Id == _layerId);
        if (layer is null) return;
        _previousValue = layer.IsLocked;
        layer.IsLocked = !_previousValue;
    }

    public void Undo()
    {
        var page = _doc.ActivePage;
        if (page is null) return;
        var layer = page.Layers.FirstOrDefault(l => l.Id == _layerId);
        if (layer is not null) layer.IsLocked = _previousValue;
    }
}
