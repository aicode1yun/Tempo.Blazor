using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Toggles the lock state of a layer (undoable).</summary>
public sealed class ToggleLayerLockCommand : IDiagramCommand
{
    private readonly DiagramLayer _layer;
    private readonly bool _previousValue;

    public ToggleLayerLockCommand(DiagramLayer layer)
    {
        _layer = layer;
        _previousValue = layer.IsLocked;
    }

    public string Name => "Toggle layer lock";

    public void Execute() => _layer.IsLocked = !_previousValue;
    public void Undo() => _layer.IsLocked = _previousValue;
}
