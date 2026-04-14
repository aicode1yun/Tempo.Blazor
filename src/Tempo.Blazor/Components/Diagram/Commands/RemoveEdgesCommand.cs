using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Removes one or more edges from the diagram document.</summary>
public sealed class RemoveEdgesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<DiagramEdge> _removed;

    public RemoveEdgesCommand(DiagramDocument doc, IEnumerable<string> ids)
    {
        _doc = doc;
        var idSet = ids.ToHashSet();
        _removed = doc.Edges.Where(e => idSet.Contains(e.Id)).ToList();
    }

    public string Name => _removed.Count == 1
        ? "Delete edge"
        : $"Delete {_removed.Count} edges";

    public void Execute()
    {
        var idSet = _removed.Select(e => e.Id).ToHashSet();
        _doc.Edges.RemoveAll(e => idSet.Contains(e.Id));
    }

    public void Undo()
    {
        foreach (var edge in _removed)
        {
            if (!_doc.Edges.Any(e => e.Id == edge.Id))
                _doc.Edges.Add(edge);
        }
    }
}
