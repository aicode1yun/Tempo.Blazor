using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Mcp.Diagram;

/// <summary>The outcome of validating a diagram document.</summary>
public sealed record DiagramValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Validates diagram documents before MCP write tools persist them.</summary>
public static class DiagramValidationEngine
{
    public static DiagramValidationResult Validate(
        DiagramDocument document,
        IEnumerable<IDiagramStencilProvider>? stencilProviders = null)
    {
        var errors = new List<string>();
        var knownStencils = DiagramStencilCatalog.BuildKnownStencilIds(stencilProviders);

        if (document.Pages.Count == 0)
        {
            document.EnsurePages();
        }

        if (document.ActivePageIndex < 0 || document.ActivePageIndex >= document.Pages.Count)
        {
            errors.Add($"activePageIndex: value {document.ActivePageIndex} is outside the page range.");
        }

        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        for (var pi = 0; pi < document.Pages.Count; pi++)
        {
            ValidatePage(document.Pages[pi], pi, pageIds, knownStencils, errors);
        }

        return new DiagramValidationResult(errors.Count == 0, errors);
    }

    private static void ValidatePage(
        DiagramPage page,
        int pageIndex,
        HashSet<string> pageIds,
        HashSet<string>? knownStencils,
        List<string> errors)
    {
        var path = $"pages[{pageIndex}]";
        if (string.IsNullOrWhiteSpace(page.Id))
        {
            errors.Add($"{path}.id: page id is required.");
        }
        else if (!pageIds.Add(page.Id))
        {
            errors.Add($"{path}.id: duplicate page id '{page.Id}'.");
        }

        if (page.Width <= 0)
        {
            errors.Add($"{path}.width: width must be greater than 0 (was {page.Width}).");
        }
        if (page.Height <= 0)
        {
            errors.Add($"{path}.height: height must be greater than 0 (was {page.Height}).");
        }

        var layerIds = ValidateLayers(page, pageIndex, errors);
        var nodeIds = ValidateNodes(page, pageIndex, layerIds, knownStencils, errors);
        var edgeIds = ValidateEdges(page, pageIndex, layerIds, errors);
        ValidateEdgeReferences(page, pageIndex, nodeIds, edgeIds, errors);
    }

    private static HashSet<string> ValidateLayers(DiagramPage page, int pageIndex, List<string> errors)
    {
        var layerIds = new HashSet<string>(StringComparer.Ordinal);
        for (var li = 0; li < page.Layers.Count; li++)
        {
            var layer = page.Layers[li];
            var path = $"pages[{pageIndex}].layers[{li}]";
            if (string.IsNullOrWhiteSpace(layer.Id))
            {
                errors.Add($"{path}.id: layer id is required.");
            }
            else if (!layerIds.Add(layer.Id))
            {
                errors.Add($"{path}.id: duplicate layer id '{layer.Id}'.");
            }
        }

        return layerIds;
    }

    private static HashSet<string> ValidateNodes(
        DiagramPage page,
        int pageIndex,
        HashSet<string> layerIds,
        HashSet<string>? knownStencils,
        List<string> errors)
    {
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var ni = 0; ni < page.Nodes.Count; ni++)
        {
            var node = page.Nodes[ni];
            var path = $"pages[{pageIndex}].nodes[{ni}]";
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add($"{path}.id: node id is required.");
            }
            else if (!nodeIds.Add(node.Id))
            {
                errors.Add($"{path}.id: duplicate node id '{node.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(node.StencilId))
            {
                errors.Add($"{path}.stencilId: stencil id is required.");
            }
            else if (knownStencils is not null && !knownStencils.Contains(node.StencilId))
            {
                errors.Add($"{path}.stencilId: unknown stencil id '{node.StencilId}'.");
            }

            if (node.W <= 0)
            {
                errors.Add($"{path}.w: width must be greater than 0 (was {node.W}).");
            }
            if (node.H <= 0)
            {
                errors.Add($"{path}.h: height must be greater than 0 (was {node.H}).");
            }
            if (!string.IsNullOrWhiteSpace(node.LayerId) && !layerIds.Contains(node.LayerId))
            {
                errors.Add($"{path}.layerId: references missing layer '{node.LayerId}'.");
            }
        }

        for (var ni = 0; ni < page.Nodes.Count; ni++)
        {
            var node = page.Nodes[ni];
            if (!string.IsNullOrWhiteSpace(node.ParentNodeId) && !nodeIds.Contains(node.ParentNodeId))
            {
                errors.Add($"pages[{pageIndex}].nodes[{ni}].parentNodeId: references missing node '{node.ParentNodeId}'.");
            }
        }

        return nodeIds;
    }

    private static HashSet<string> ValidateEdges(
        DiagramPage page,
        int pageIndex,
        HashSet<string> layerIds,
        List<string> errors)
    {
        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var ei = 0; ei < page.Edges.Count; ei++)
        {
            var edge = page.Edges[ei];
            var path = $"pages[{pageIndex}].edges[{ei}]";
            if (string.IsNullOrWhiteSpace(edge.Id))
            {
                errors.Add($"{path}.id: edge id is required.");
            }
            else if (!edgeIds.Add(edge.Id))
            {
                errors.Add($"{path}.id: duplicate edge id '{edge.Id}'.");
            }

            if (!edge.IsValid())
            {
                errors.Add($"{path}: edge must have both a source and a target.");
            }
            if (!string.IsNullOrWhiteSpace(edge.LayerId) && !layerIds.Contains(edge.LayerId))
            {
                errors.Add($"{path}.layerId: references missing layer '{edge.LayerId}'.");
            }
        }

        return edgeIds;
    }

    private static void ValidateEdgeReferences(
        DiagramPage page,
        int pageIndex,
        HashSet<string> nodeIds,
        HashSet<string> edgeIds,
        List<string> errors)
    {
        for (var ei = 0; ei < page.Edges.Count; ei++)
        {
            var edge = page.Edges[ei];
            var path = $"pages[{pageIndex}].edges[{ei}]";

            if (!string.IsNullOrWhiteSpace(edge.SourceNodeId) && !nodeIds.Contains(edge.SourceNodeId))
            {
                errors.Add($"{path}.sourceNodeId: references missing node '{edge.SourceNodeId}'.");
            }
            if (!string.IsNullOrWhiteSpace(edge.TargetNodeId) && !nodeIds.Contains(edge.TargetNodeId))
            {
                errors.Add($"{path}.targetNodeId: references missing node '{edge.TargetNodeId}'.");
            }
            if (!string.IsNullOrWhiteSpace(edge.SourceEdgeId) && !edgeIds.Contains(edge.SourceEdgeId))
            {
                errors.Add($"{path}.sourceEdgeId: references missing edge '{edge.SourceEdgeId}'.");
            }
            if (!string.IsNullOrWhiteSpace(edge.TargetEdgeId) && !edgeIds.Contains(edge.TargetEdgeId))
            {
                errors.Add($"{path}.targetEdgeId: references missing edge '{edge.TargetEdgeId}'.");
            }
        }
    }
}
