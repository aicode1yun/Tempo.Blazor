using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Undoable command that changes the Z-index of one or more nodes.</summary>
public sealed class UpdateZIndexCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly Dictionary<string, int> _before;
    private readonly Dictionary<string, int> _after;

    /// <summary>Creates a command that sets Z-index for multiple nodes.</summary>
    public UpdateZIndexCommand(DiagramDocument doc, Dictionary<string, int> before, Dictionary<string, int> after)
    {
        _doc = doc;
        _before = before;
        _after = after;
    }

    /// <inheritdoc/>
    public string Name => "Change Z-Index";

    /// <inheritdoc/>
    public void Execute() => Apply(_after);

    /// <inheritdoc/>
    public void Undo() => Apply(_before);

    private void Apply(Dictionary<string, int> values)
    {
        foreach (var node in _doc.Nodes)
        {
            if (values.TryGetValue(node.Id, out var z))
                node.ZIndex = z;
        }
    }
}
