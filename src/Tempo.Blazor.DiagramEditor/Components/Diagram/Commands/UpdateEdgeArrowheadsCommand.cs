using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the start and/or end arrowhead of selected edges.</summary>
public sealed class UpdateEdgeArrowheadsCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IEnumerable<string> _edgeIds;
    private readonly string? _newStartArrow;
    private readonly string? _newEndArrow;
    private readonly Dictionary<string, (string? StartArrow, string? EndArrow)> _beforeStates = [];

    public UpdateEdgeArrowheadsCommand(DiagramDocument doc, IEnumerable<string> edgeIds, string? newStartArrow = null, string? newEndArrow = null)
    {
        _doc = doc;
        _edgeIds = edgeIds.ToList();
        _newStartArrow = newStartArrow;
        _newEndArrow = newEndArrow;
        foreach (var id in _edgeIds)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == id);
            if (edge is not null) _beforeStates[id] = (edge.StartArrow, edge.EndArrow);
        }
    }

    public string Name => "Update edge arrowheads";

    public void Execute()
    {
        foreach (var id in _edgeIds)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == id);
            if (edge is null) continue;
            if (_newStartArrow is not null) edge.StartArrow = _newStartArrow;
            if (_newEndArrow is not null) edge.EndArrow = _newEndArrow;
        }
    }

    public void Undo()
    {
        foreach (var kvp in _beforeStates)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == kvp.Key);
            if (edge is null) continue;
            edge.StartArrow = kvp.Value.StartArrow ?? edge.StartArrow;
            edge.EndArrow = kvp.Value.EndArrow ?? edge.EndArrow;
        }
    }
}
