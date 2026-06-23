using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Command that rotates a single diagram node.</summary>
public sealed class RotateNodeCommand : IDiagramCommand
{
    private readonly DiagramDocument _document;
    private readonly string _nodeId;
    private readonly double _oldRotation;
    private readonly double _newRotation;

    /// <summary>Creates a new rotate command.</summary>
    public RotateNodeCommand(DiagramDocument document, string nodeId, double oldRotation, double newRotation)
    {
        _document = document;
        _nodeId = nodeId;
        _oldRotation = oldRotation;
        _newRotation = newRotation;
    }

    /// <inheritdoc/>
    public string Name => "Rotate node";

    /// <inheritdoc/>
    public void Execute()
    {
        var node = _document.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is not null) node.Rotation = _newRotation;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        var node = _document.Nodes.FirstOrDefault(n => n.Id == _nodeId);
        if (node is not null) node.Rotation = _oldRotation;
    }
}
