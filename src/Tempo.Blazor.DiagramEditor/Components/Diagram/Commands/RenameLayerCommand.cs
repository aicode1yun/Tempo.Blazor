using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Renames a layer (undoable).</summary>
public sealed class RenameLayerCommand : IDiagramCommand
{
    private readonly DiagramLayer _layer;
    private readonly string _oldName;
    private readonly string _newName;

    public RenameLayerCommand(DiagramLayer layer, string newName)
    {
        _layer = layer;
        _oldName = layer.Name;
        _newName = newName;
    }

    public string Name => "Rename layer";

    public void Execute() => _layer.Name = _newName;
    public void Undo() => _layer.Name = _oldName;
}
