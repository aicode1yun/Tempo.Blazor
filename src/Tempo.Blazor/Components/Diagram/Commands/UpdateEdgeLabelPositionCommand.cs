using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the label position t of an edge (undoable).</summary>
public sealed class UpdateEdgeLabelPositionCommand : IDiagramCommand
{
    private readonly DiagramEdge _edge;
    private readonly double _before;
    private readonly double _after;

    public UpdateEdgeLabelPositionCommand(DiagramEdge edge, double after)
    {
        _edge = edge;
        _before = edge.LabelPositionT;
        _after = after;
    }

    public string Name => "Move edge label";

    public void Execute() => _edge.LabelPositionT = _after;

    public void Undo() => _edge.LabelPositionT = _before;
}
