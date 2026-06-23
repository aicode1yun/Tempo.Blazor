using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe.Commands;

/// <summary>Undoable command that reorders layers.</summary>
public sealed class ReorderLayersCommand : IWireframeCommand
{
    private readonly WireframeDocument _doc;
    private readonly Dictionary<string, int> _newOrders = new();
    private Dictionary<string, int>? _previousOrders;

    public string Name => "Reorder layers";

    /// <summary>Creates a command that applies a complete new ordering.</summary>
    public ReorderLayersCommand(WireframeDocument doc, Dictionary<string, int> newOrders)
    {
        _doc = doc;
        _newOrders = newOrders;
    }

    public void Execute()
    {
        var page = _doc.ActivePage;
        if (page is null || _newOrders.Count == 0) return;

        _previousOrders = page.Layers.ToDictionary(l => l.Id, l => l.Order);
        foreach (var layer in page.Layers)
        {
            if (_newOrders.TryGetValue(layer.Id, out var order))
                layer.Order = order;
        }
    }

    public void Undo()
    {
        var page = _doc.ActivePage;
        if (page is null || _previousOrders is null) return;

        foreach (var layer in page.Layers)
        {
            if (_previousOrders.TryGetValue(layer.Id, out var order))
                layer.Order = order;
        }
    }
}
