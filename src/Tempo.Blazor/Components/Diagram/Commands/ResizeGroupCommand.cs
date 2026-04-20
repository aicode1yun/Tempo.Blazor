using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Resizes a group container and proportionally scales all member nodes (undoable).</summary>
public sealed class ResizeGroupCommand : IDiagramCommand
{
    private readonly DiagramDocument _document;
    private readonly Dictionary<string, NodeRect> _oldRects;
    private readonly Dictionary<string, NodeRect> _newRects;

    /// <summary>Creates a new group resize command.</summary>
    public ResizeGroupCommand(
        DiagramDocument document,
        Dictionary<string, NodeRect> oldRects,
        Dictionary<string, NodeRect> newRects)
    {
        _document = document;
        _oldRects = new Dictionary<string, NodeRect>(oldRects);
        _newRects = new Dictionary<string, NodeRect>(newRects);
    }

    /// <inheritdoc/>
    public string Name => "Resize group";

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var node in _document.Nodes)
        {
            if (_newRects.TryGetValue(node.Id, out var rect))
            {
                node.X = rect.X;
                node.Y = rect.Y;
                node.W = rect.W;
                node.H = rect.H;
            }
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var node in _document.Nodes)
        {
            if (_oldRects.TryGetValue(node.Id, out var rect))
            {
                node.X = rect.X;
                node.Y = rect.Y;
                node.W = rect.W;
                node.H = rect.H;
            }
        }
    }
}

/// <summary>Simple rectangle record for resize commands.</summary>
public sealed record NodeRect(double X, double Y, double W, double H);
