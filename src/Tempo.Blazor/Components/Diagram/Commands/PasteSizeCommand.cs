using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Applies the copied width/height from <see cref="DiagramClipboard"/> to the selected nodes (undoable).</summary>
public sealed class PasteSizeCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly string[] _nodeIds;
    private readonly List<(string Id, double OldW, double OldH, double NewW, double NewH)> _snapshots = [];

    public PasteSizeCommand(DiagramDocument doc, string[] nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds;
    }

    public string Name => "Paste size";

    public void Execute()
    {
        if (DiagramClipboard.Width is null || DiagramClipboard.Height is null) return;
        foreach (var id in _nodeIds)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            _snapshots.Add((id, node.W, node.H, DiagramClipboard.Width.Value, DiagramClipboard.Height.Value));
            node.W = DiagramClipboard.Width.Value;
            node.H = DiagramClipboard.Height.Value;
        }
    }

    public void Undo()
    {
        foreach (var (id, oldW, oldH, _, _) in _snapshots)
        {
            var node = _doc.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) continue;
            node.W = oldW;
            node.H = oldH;
        }
    }
}
