using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Toggles the visibility of a layer (undoable).</summary>
public sealed class ToggleLayerVisibilityCommand : IDiagramCommand
{
    private readonly DiagramLayer _layer;
    private readonly bool _previousValue;

    public ToggleLayerVisibilityCommand(DiagramLayer layer)
    {
        _layer = layer;
        _previousValue = layer.IsVisible;
    }

    public string Name => "Toggle layer visibility";

    public void Execute() => _layer.IsVisible = !_previousValue;
    public void Undo() => _layer.IsVisible = _previousValue;
}
