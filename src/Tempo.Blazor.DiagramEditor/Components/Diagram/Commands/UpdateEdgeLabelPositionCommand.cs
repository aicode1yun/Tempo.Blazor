using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the label position (t + offset) of an edge (undoable).</summary>
public sealed class UpdateEdgeLabelPositionCommand : IDiagramCommand
{
    private readonly DiagramEdge _edge;
    private readonly double _beforeT;
    private readonly double _beforeOx;
    private readonly double _beforeOy;
    private readonly double _afterT;
    private readonly double _afterOx;
    private readonly double _afterOy;

    public UpdateEdgeLabelPositionCommand(DiagramEdge edge, double afterT, double afterOx, double afterOy)
    {
        _edge = edge;
        _beforeT = edge.LabelPositionT;
        _beforeOx = edge.LabelOffsetX;
        _beforeOy = edge.LabelOffsetY;
        _afterT = afterT;
        _afterOx = afterOx;
        _afterOy = afterOy;
    }

    public string Name => "Move edge label";

    public void Execute()
    {
        _edge.LabelPositionT = _afterT;
        _edge.LabelOffsetX = _afterOx;
        _edge.LabelOffsetY = _afterOy;
    }

    public void Undo()
    {
        _edge.LabelPositionT = _beforeT;
        _edge.LabelOffsetX = _beforeOx;
        _edge.LabelOffsetY = _beforeOy;
    }
}
