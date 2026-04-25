using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Undoable command that renames a layer.</summary>
public sealed class RenameLayerCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly string _layerId;
    private readonly string? _oldName;
    private readonly string _newName;

    public string Name => "Rename layer";

    public RenameLayerCommand(WireframeDocument doc, string layerId, string? oldName, string newName)
    {
        _doc = doc;
        _layerId = layerId;
        _oldName = oldName;
        _newName = newName;
    }

    public void Execute()
    {
        var page = _doc.ActivePage;
        if (page is null) return;
        var layer = page.Layers.FirstOrDefault(l => l.Id == _layerId);
        if (layer is not null) layer.Name = _newName;
    }

    public void Undo()
    {
        var page = _doc.ActivePage;
        if (page is null) return;
        var layer = page.Layers.FirstOrDefault(l => l.Id == _layerId);
        if (layer is not null) layer.Name = _oldName ?? layer.Name;
    }
}
