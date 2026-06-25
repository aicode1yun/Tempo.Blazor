using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Adds a single edge to the diagram document.</summary>
public sealed class AddEdgeCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly DiagramEdge _edge;

    public AddEdgeCommand(DiagramDocument doc, DiagramEdge edge)
    {
        _doc = doc;
        _edge = edge;
    }

    public string Name => "Add edge";

    public void Execute() => _doc.Edges.Add(_edge);

    public void Undo() => _doc.Edges.RemoveAll(e => e.Id == _edge.Id);
}
