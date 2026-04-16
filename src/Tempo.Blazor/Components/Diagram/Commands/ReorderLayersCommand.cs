using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Reorders layers by their Order property (undoable).</summary>
public sealed class ReorderLayersCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly Dictionary<string, int> _previousOrders;
    private readonly Dictionary<string, int> _newOrders;

    public ReorderLayersCommand(DiagramDocument doc, Dictionary<string, int> newOrders)
    {
        _doc = doc;
        _newOrders = newOrders;
        _previousOrders = doc.Layers.ToDictionary(l => l.Id, l => l.Order);
    }

    public string Name => "Reorder layers";

    public void Execute()
    {
        foreach (var layer in _doc.Layers)
            if (_newOrders.TryGetValue(layer.Id, out var order))
                layer.Order = order;
    }

    public void Undo()
    {
        foreach (var layer in _doc.Layers)
            if (_previousOrders.TryGetValue(layer.Id, out var order))
                layer.Order = order;
    }
}
