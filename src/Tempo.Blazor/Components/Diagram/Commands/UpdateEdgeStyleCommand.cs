using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Batch updates style properties of a single edge (undoable).</summary>
public sealed class UpdateEdgeStyleCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly DiagramEdgeStyleSnapshot _before;
    private readonly DiagramEdgeStyleSnapshot _after;

    public UpdateEdgeStyleCommand(DiagramDocument doc, string edgeId, DiagramEdgeStyleSnapshot before, DiagramEdgeStyleSnapshot after)
    {
        _doc = doc;
        _edgeId = edgeId;
        _before = before;
        _after = after;
    }

    public string Name => "Update edge style";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        _after.ApplyTo(edge);
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        _before.ApplyTo(edge);
    }
}
