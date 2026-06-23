using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the line style (stroke dasharray) of selected edges.</summary>
public sealed class UpdateEdgeLineStyleCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IEnumerable<string> _edgeIds;
    private readonly string? _newStrokeDasharray;
    private readonly Dictionary<string, string?> _beforeStates = [];

    public UpdateEdgeLineStyleCommand(DiagramDocument doc, IEnumerable<string> edgeIds, string? newStrokeDasharray)
    {
        _doc = doc;
        _edgeIds = edgeIds.ToList();
        _newStrokeDasharray = newStrokeDasharray;
        foreach (var id in _edgeIds)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == id);
            if (edge is not null) _beforeStates[id] = edge.Style.StrokeDasharray;
        }
    }

    public string Name => "Update edge line style";

    public void Execute()
    {
        foreach (var id in _edgeIds)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == id);
            if (edge is null) continue;
            edge.Style.StrokeDasharray = _newStrokeDasharray;
        }
    }

    public void Undo()
    {
        foreach (var kvp in _beforeStates)
        {
            var edge = _doc.Edges.FirstOrDefault(e => e.Id == kvp.Key);
            if (edge is null) continue;
            edge.Style.StrokeDasharray = kvp.Value;
        }
    }
}
