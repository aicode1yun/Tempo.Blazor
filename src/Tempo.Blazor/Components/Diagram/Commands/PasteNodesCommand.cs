using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Commands;

/// <summary>Pastes nodes and edges from the shared clipboard payload (undoable).</summary>
public sealed class PasteNodesCommand : IDiagramCommand
{
    private readonly DiagramDocument _doc;
    private readonly DiagramCommandStack? _stack;
    private readonly IJSRuntime? _js;
    private readonly ElementReference? _containerRef;
    private readonly string? _clipboardJson;
    private readonly double _offsetX;
    private readonly double _offsetY;
    private readonly List<DiagramNode> _pastedNodes = [];
    private readonly List<DiagramEdge> _pastedEdges = [];

    /// <summary>Node IDs from the most recent paste operation.</summary>
    public static List<string> LastPastedNodeIds { get; } = [];

    public PasteNodesCommand(DiagramDocument doc, DiagramCommandStack? stack, IJSRuntime? js, ElementReference? containerRef)
    {
        _doc = doc;
        _stack = stack;
        _js = js;
        _containerRef = containerRef;
        _clipboardJson = CopyNodesCommand.SharedClipboardJson;
        _offsetX = 16;
        _offsetY = 16;
        PreparePaste();
    }

    public PasteNodesCommand(DiagramDocument doc, string clipboardJson, double offsetX, double offsetY)
    {
        _doc = doc;
        _clipboardJson = clipboardJson;
        _offsetX = offsetX;
        _offsetY = offsetY;
        PreparePaste();
    }

    public string Name => "Paste nodes";

    private void PreparePaste()
    {
        var json = _clipboardJson;
        if (string.IsNullOrWhiteSpace(json)) return;

        var payload = JsonSerializer.Deserialize<DiagramClipboardData>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (payload is null) return;

        var idMap = new Dictionary<string, string>();

        foreach (var src in payload.Nodes)
        {
            var copy = DeepCopyNode(src);
            copy.X += _offsetX;
            copy.Y += _offsetY;
            idMap[src.Id] = copy.Id;
            _pastedNodes.Add(copy);
        }

        foreach (var srcEdge in payload.Edges)
        {
            if (!idMap.TryGetValue(srcEdge.SourceNodeId, out var newSourceId)) continue;
            if (!idMap.TryGetValue(srcEdge.TargetNodeId, out var newTargetId)) continue;

            var copyEdge = DeepCopyEdge(srcEdge);
            copyEdge.SourceNodeId = newSourceId;
            copyEdge.TargetNodeId = newTargetId;
            if (copyEdge.Waypoints is not null)
            {
                foreach (var wp in copyEdge.Waypoints)
                {
                    wp.X += _offsetX;
                    wp.Y += _offsetY;
                }
            }
            _pastedEdges.Add(copyEdge);
        }
    }

    private static DiagramNode DeepCopyNode(DiagramNode src)
    {
        var json = JsonSerializer.Serialize(src);
        var copy = JsonSerializer.Deserialize<DiagramNode>(json)!;
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        foreach (var p in copy.Ports)
            p.Id = Guid.NewGuid().ToString("N")[..8];
        return copy;
    }

    private static DiagramEdge DeepCopyEdge(DiagramEdge src)
    {
        var json = JsonSerializer.Serialize(src);
        var copy = JsonSerializer.Deserialize<DiagramEdge>(json)!;
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        return copy;
    }

    public void Execute()
    {
        foreach (var n in _pastedNodes) _doc.Nodes.Add(n);
        foreach (var e in _pastedEdges) _doc.Edges.Add(e);
        LastPastedNodeIds.Clear();
        LastPastedNodeIds.AddRange(_pastedNodes.Select(n => n.Id));
    }

    public void Undo()
    {
        foreach (var n in _pastedNodes) _doc.Nodes.Remove(n);
        foreach (var e in _pastedEdges) _doc.Edges.Remove(e);
        LastPastedNodeIds.Clear();
    }

    public IReadOnlyList<DiagramNode> PastedNodes => _pastedNodes;
    public IReadOnlyList<DiagramEdge> PastedEdges => _pastedEdges;

    private sealed class DiagramClipboardData
    {
        public List<DiagramNode> Nodes { get; set; } = [];
        public List<DiagramEdge> Edges { get; set; } = [];
    }
}
