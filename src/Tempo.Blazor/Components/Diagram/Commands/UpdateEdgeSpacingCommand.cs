using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Updates the source or target spacing of an edge (undoable).</summary>
public sealed class UpdateEdgeSpacingCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string _edgeId;
    private readonly double? _oldSourceSpacing;
    private readonly double? _newSourceSpacing;
    private readonly double? _oldTargetSpacing;
    private readonly double? _newTargetSpacing;

    public UpdateEdgeSpacingCommand(
        DiagramDocument doc,
        string edgeId,
        double? oldSourceSpacing,
        double? newSourceSpacing,
        double? oldTargetSpacing,
        double? newTargetSpacing)
    {
        _doc = doc;
        _edgeId = edgeId;
        _oldSourceSpacing = oldSourceSpacing;
        _newSourceSpacing = newSourceSpacing;
        _oldTargetSpacing = oldTargetSpacing;
        _newTargetSpacing = newTargetSpacing;
    }

    public string Name => "Update edge spacing";

    public void Execute()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        if (_newSourceSpacing.HasValue) edge.SourceSpacing = _newSourceSpacing.Value;
        if (_newTargetSpacing.HasValue) edge.TargetSpacing = _newTargetSpacing.Value;
    }

    public void Undo()
    {
        var edge = _doc.Edges.FirstOrDefault(e => e.Id == _edgeId);
        if (edge is null) return;
        if (_oldSourceSpacing.HasValue) edge.SourceSpacing = _oldSourceSpacing.Value;
        else edge.SourceSpacing = null;
        if (_oldTargetSpacing.HasValue) edge.TargetSpacing = _oldTargetSpacing.Value;
        else edge.TargetSpacing = null;
    }
}
