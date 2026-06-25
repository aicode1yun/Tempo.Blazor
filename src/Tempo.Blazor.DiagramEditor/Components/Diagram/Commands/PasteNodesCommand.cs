using System.Linq;
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
    private double _offsetX;
    private double _offsetY;
    private readonly bool _useTarget;
    private readonly double _targetX;
    private readonly double _targetY;
    private readonly string? _parentGroupId;
    private readonly List<DiagramNode> _pastedNodes = [];
    private readonly List<DiagramEdge> _pastedEdges = [];

    /// <summary>Node IDs from the most recent paste operation.</summary>
    public static List<string> LastPastedNodeIds { get; } = [];

    public PasteNodesCommand(DiagramDocument doc, DiagramCommandStack? stack, IJSRuntime? js, ElementReference? containerRef, string? parentGroupId = null)
    {
        _doc = doc;
        _stack = stack;
        _js = js;
        _containerRef = containerRef;
        _parentGroupId = parentGroupId;
        _clipboardJson = CopyNodesCommand.SharedClipboardJson;
        _offsetX = 16;
        _offsetY = 16;
        PreparePaste();
    }

    public PasteNodesCommand(DiagramDocument doc, string clipboardJson, double offsetX, double offsetY, string? parentGroupId = null)
    {
        _doc = doc;
        _clipboardJson = clipboardJson;
        _offsetX = offsetX;
        _offsetY = offsetY;
        _parentGroupId = parentGroupId;
        PreparePaste();
    }

    /// <summary>Pastes from the internal static clipboard without requiring JS interop.</summary>
    public PasteNodesCommand(DiagramDocument doc, double offsetX, double offsetY, bool useInternalClipboard, string? parentGroupId = null)
    {
        _doc = doc;
        _offsetX = offsetX;
        _offsetY = offsetY;
        _parentGroupId = parentGroupId;
        if (useInternalClipboard && DiagramClipboard.HasNodes)
        {
            _clipboardJson = JsonSerializer.Serialize(new DiagramClipboardData
            {
                Nodes = DiagramClipboard.Nodes,
                Edges = DiagramClipboard.Edges
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        else
        {
            _clipboardJson = CopyNodesCommand.SharedClipboardJson;
        }
        PreparePaste();
    }

    /// <summary>Pastes at a specific canvas location ("Paste here").</summary>
    public PasteNodesCommand(DiagramDocument doc, double targetX, double targetY, bool useInternalClipboard, bool pasteHere, string? parentGroupId = null)
    {
        _doc = doc;
        _useTarget = pasteHere;
        _targetX = targetX;
        _targetY = targetY;
        _parentGroupId = parentGroupId;
        if (useInternalClipboard && DiagramClipboard.HasNodes)
        {
            _clipboardJson = JsonSerializer.Serialize(new DiagramClipboardData
            {
                Nodes = DiagramClipboard.Nodes,
                Edges = DiagramClipboard.Edges
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        else
        {
            _clipboardJson = CopyNodesCommand.SharedClipboardJson;
        }
        PreparePaste();
    }

    public string Name => "Paste nodes";

    private void PreparePaste()
    {
        var json = _clipboardJson;
        if (string.IsNullOrWhiteSpace(json)) return;

        var payload = JsonSerializer.Deserialize<DiagramClipboardData>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (payload is null) return;

        if (_useTarget && payload.Nodes.Count > 0)
        {
            var minX = payload.Nodes.Min(n => n.X);
            var maxX = payload.Nodes.Max(n => n.X + n.W);
            var minY = payload.Nodes.Min(n => n.Y);
            var maxY = payload.Nodes.Max(n => n.Y + n.H);
            _offsetX = _targetX - (minX + maxX) / 2.0;
            _offsetY = _targetY - (minY + maxY) / 2.0;
        }

        var idMap = new Dictionary<string, string>();

        foreach (var src in payload.Nodes)
        {
            var copy = DeepCopyNode(src);
            copy.X += _offsetX;
            copy.Y += _offsetY;
            if (_parentGroupId is not null)
            {
                copy.ParentGroupId = _parentGroupId;
                copy.GroupId = _parentGroupId;
            }
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
