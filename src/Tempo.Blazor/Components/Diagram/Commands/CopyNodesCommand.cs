using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Captures selected nodes and their connecting edges into a JSON clipboard payload.</summary>
public sealed class CopyNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly IReadOnlyList<string> _nodeIds;

    /// <summary>Shared in-memory clipboard JSON.</summary>
    public static string? SharedClipboardJson { get; set; }

    public CopyNodesCommand(DiagramDocument doc, IEnumerable<string> nodeIds)
    {
        _doc = doc;
        _nodeIds = nodeIds.ToList();
        ClipboardJson = BuildClipboardJson();
    }

    /// <summary>Serialized JSON payload containing the copied nodes and edges.</summary>
    public string ClipboardJson { get; }

    public string Name => "Copy nodes";

    private string BuildClipboardJson()
    {
        var nodes = _doc.Nodes.Where(n => _nodeIds.Contains(n.Id)).ToList();
        var nodeIdSet = new HashSet<string>(_nodeIds);

        // Include edges that connect two copied nodes
        var edges = _doc.Edges.Where(e =>
            nodeIdSet.Contains(e.SourceNodeId) && nodeIdSet.Contains(e.TargetNodeId))
            .ToList();

        var payload = new DiagramClipboardData
        {
            Nodes = nodes,
            Edges = edges
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public void Execute()
    {
        SharedClipboardJson = ClipboardJson;
    }

    public void Undo() { /* No-op */ }

    private sealed class DiagramClipboardData
    {
        public List<DiagramNode> Nodes { get; set; } = [];
        public List<DiagramEdge> Edges { get; set; } = [];
    }
}
