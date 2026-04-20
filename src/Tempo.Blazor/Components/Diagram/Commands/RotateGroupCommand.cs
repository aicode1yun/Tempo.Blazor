using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Rotates a group container and all its members by the same delta (undoable).</summary>
public sealed class RotateGroupCommand : IDiagramCommand
{
    private readonly DiagramDocument _document;
    private readonly Dictionary<string, double> _oldRotations;
    private readonly Dictionary<string, double> _newRotations;

    /// <summary>Creates a new group rotate command.</summary>
    public RotateGroupCommand(
        DiagramDocument document,
        Dictionary<string, double> oldRotations,
        Dictionary<string, double> newRotations)
    {
        _document = document;
        _oldRotations = new Dictionary<string, double>(oldRotations);
        _newRotations = new Dictionary<string, double>(newRotations);
    }

    /// <inheritdoc/>
    public string Name => "Rotate group";

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var node in _document.Nodes)
        {
            if (_newRotations.TryGetValue(node.Id, out var rot))
            {
                node.Rotation = rot;
            }
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var node in _document.Nodes)
        {
            if (_oldRotations.TryGetValue(node.Id, out var rot))
            {
                node.Rotation = rot;
            }
        }
    }
}
