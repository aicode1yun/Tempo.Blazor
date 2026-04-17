using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Serialization;

namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>
/// Extension methods for <see cref="DiagramDocument"/>.
/// </summary>
public static class DiagramDocumentExtensions
{
    /// <summary>
    /// Creates a deep clone of the document with all IDs re-seeded.
    /// Edge and node references (SourceNodeId, TargetNodeId, ParentNodeId,
    /// ParentGroupId, GroupId, LayerId, port IDs) are remapped accordingly.
    /// </summary>
    public static DiagramDocument CloneWithNewIds(this DiagramDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.Serialize(source, DiagramJsonOptions.Default);
        var clone = JsonSerializer.Deserialize<DiagramDocument>(json, DiagramJsonOptions.Default);
        if (clone is null)
            throw new InvalidOperationException("Failed to clone diagram document.");

        clone.Id = Guid.NewGuid().ToString();
        clone.CreatedAt = DateTime.UtcNow;
        clone.ModifiedAt = DateTime.UtcNow;

        foreach (var page in clone.Pages)
        {
            var pageIdMap = new Dictionary<string, string>();
            page.Id = Guid.NewGuid().ToString();

            var layerIdMap = new Dictionary<string, string>();
            foreach (var layer in page.Layers)
            {
                var newId = Guid.NewGuid().ToString("N")[..8];
                layerIdMap[layer.Id] = newId;
                layer.Id = newId;
            }

            var nodeIdMap = new Dictionary<string, string>();
            var portIdMap = new Dictionary<string, string>();

            foreach (var node in page.Nodes)
            {
                var newNodeId = Guid.NewGuid().ToString("N")[..8];
                nodeIdMap[node.Id] = newNodeId;
                node.Id = newNodeId;

                foreach (var port in node.Ports)
                {
                    var newPortId = Guid.NewGuid().ToString("N")[..8];
                    portIdMap[port.Id] = newPortId;
                    port.Id = newPortId;
                }
            }

            var edgeIdMap = new Dictionary<string, string>();
            foreach (var edge in page.Edges)
            {
                var newEdgeId = Guid.NewGuid().ToString("N")[..8];
                edgeIdMap[edge.Id] = newEdgeId;
                edge.Id = newEdgeId;
            }

            // Remap references on nodes
            foreach (var node in page.Nodes)
            {
                if (!string.IsNullOrEmpty(node.ParentNodeId) && nodeIdMap.TryGetValue(node.ParentNodeId, out var newParentNodeId))
                    node.ParentNodeId = newParentNodeId;

                if (!string.IsNullOrEmpty(node.ParentGroupId) && nodeIdMap.TryGetValue(node.ParentGroupId, out var newParentGroupId))
                    node.ParentGroupId = newParentGroupId;

                if (!string.IsNullOrEmpty(node.GroupId) && nodeIdMap.TryGetValue(node.GroupId, out var newGroupId))
                    node.GroupId = newGroupId;

                if (!string.IsNullOrEmpty(node.LayerId) && layerIdMap.TryGetValue(node.LayerId, out var newLayerId))
                    node.LayerId = newLayerId;
            }

            // Remap references on edges
            foreach (var edge in page.Edges)
            {
                if (!string.IsNullOrEmpty(edge.SourceNodeId) && nodeIdMap.TryGetValue(edge.SourceNodeId, out var newSourceNodeId))
                    edge.SourceNodeId = newSourceNodeId;

                if (!string.IsNullOrEmpty(edge.TargetNodeId) && nodeIdMap.TryGetValue(edge.TargetNodeId, out var newTargetNodeId))
                    edge.TargetNodeId = newTargetNodeId;

                if (!string.IsNullOrEmpty(edge.SourcePortId) && portIdMap.TryGetValue(edge.SourcePortId, out var newSourcePortId))
                    edge.SourcePortId = newSourcePortId;

                if (!string.IsNullOrEmpty(edge.TargetPortId) && portIdMap.TryGetValue(edge.TargetPortId, out var newTargetPortId))
                    edge.TargetPortId = newTargetPortId;

                if (!string.IsNullOrEmpty(edge.SourceEdgeId) && edgeIdMap.TryGetValue(edge.SourceEdgeId, out var newSourceEdgeId))
                    edge.SourceEdgeId = newSourceEdgeId;

                if (!string.IsNullOrEmpty(edge.TargetEdgeId) && edgeIdMap.TryGetValue(edge.TargetEdgeId, out var newTargetEdgeId))
                    edge.TargetEdgeId = newTargetEdgeId;
            }
        }

        return clone;
    }
}
