using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Batch updates style properties of multiple edges (undoable).</summary>
public sealed class UpdateEdgesStyleCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _edgeIds;
    private readonly IReadOnlyList<DiagramEdgeStyleSnapshot> _beforeSnapshots;
    private readonly DiagramEdgeStyleSnapshot _afterSnapshot;

    public UpdateEdgesStyleCommand(DiagramDocument doc, IEnumerable<string> edgeIds, IEnumerable<DiagramEdgeStyleSnapshot> beforeSnapshots, DiagramEdgeStyleSnapshot afterSnapshot)
    {
        _doc = doc;
        _edgeIds = edgeIds.ToList();
        _beforeSnapshots = beforeSnapshots.ToList();
        _afterSnapshot = afterSnapshot;
    }

    public string Name => "Update edges style";

    public void Execute()
    {
        foreach (var id in _edgeIds)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == id);
            if (edge is null) continue;
            _afterSnapshot.ApplyTo(edge);
        }
    }

    public void Undo()
    {
        for (int i = 0; i < _edgeIds.Count; i++)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeIds[i]);
            if (edge is null || i >= _beforeSnapshots.Count) continue;
            _beforeSnapshots[i].ApplyTo(edge);
        }
    }
}
