using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the label of a single edge.</summary>
public sealed class UpdateEdgeLabelCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly string? _oldLabel;
    private readonly string? _newLabel;

    public UpdateEdgeLabelCommand(DiagramDocument doc, string edgeId, string? oldLabel, string? newLabel)
    {
        _doc = doc;
        _edgeId = edgeId;
        _oldLabel = oldLabel;
        _newLabel = newLabel;
    }

    public string Name => "Update edge label";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Label = _newLabel;
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        edge.Label = _oldLabel;
    }
}
