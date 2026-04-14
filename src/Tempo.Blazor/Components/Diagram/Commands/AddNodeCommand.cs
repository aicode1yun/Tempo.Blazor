using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Adds a single node to the diagram document.</summary>
public sealed class AddNodeCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly DiagramNode _node;

    public AddNodeCommand(DiagramDocument doc, DiagramNode node)
    {
        _doc = doc;
        _node = node;
    }

    public string Name => $"Add {_node.StencilId}";

    public void Execute() => _doc.Nodes.Add(_node);

    public void Undo() => _doc.Nodes.RemoveAll(n => n.Id == _node.Id);
}
