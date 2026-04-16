using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Adds a new layer to the diagram (undoable).</summary>
public sealed class AddLayerCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly DiagramLayer _layer;

    public AddLayerCommand(DiagramDocument doc, string name)
    {
        _doc = doc;
        var maxOrder = doc.Layers.Count > 0 ? doc.Layers.Max(l => l.Order) : 0;
        _layer = new DiagramLayer
        {
            Name = name,
            Order = maxOrder + 1,
            IsVisible = true,
            IsLocked = false,
        };
    }

    public DiagramLayer Layer => _layer;
    public string Name => "Add layer";

    public void Execute() => _doc.Layers.Add(_layer);
    public void Undo() => _doc.Layers.Remove(_layer);
}
