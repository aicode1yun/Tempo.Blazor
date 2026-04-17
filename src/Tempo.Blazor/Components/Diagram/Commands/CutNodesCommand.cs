using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Copies selected nodes to the internal clipboard and removes them from the document.</summary>
public sealed class CutNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IEnumerable<string> _nodeIds;
    private readonly string? _clipboardJson;
    private readonly RemoveNodesCommand _removeCommand;

    public CutNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        var copyCommand = new CopyNodesCommand(doc, _nodeIds);
        _clipboardJson = copyCommand.ClipboardJson;
        _removeCommand = new RemoveNodesCommand(doc, _nodeIds);
    }

    public string Name => "Cut nodes";

    public void Execute()
    {
        CopyNodesCommand.SharedClipboardJson = _clipboardJson;
        if (_clipboardJson is not null)
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<DiagramClipboardData>(_clipboardJson, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            if (payload is not null)
            {
                DiagramClipboard.Nodes = payload.Nodes;
                DiagramClipboard.Edges = payload.Edges;
            }
        }
        _removeCommand.Execute();
    }

    public void Undo()
    {
        _removeCommand.Undo();
    }

    private sealed class DiagramClipboardData
    {
        public List<DiagramNode> Nodes { get; set; } = [];
        public List<DiagramEdge> Edges { get; set; } = [];
    }
}
