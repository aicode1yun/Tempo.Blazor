using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Mcp.Diagram;

public sealed record DiagramBriefNode(string Id, string StencilId, string? Label, double X, double Y, double W, double H);

public sealed record DiagramBriefEdge(
    string Id,
    string? SourceNodeId,
    string? SourceStencilId,
    string? TargetNodeId,
    string? TargetStencilId,
    string? Label,
    string Routing);

public sealed record DiagramBriefStencilUsage(string StencilId, int Count);

public sealed record DiagramBriefLayer(string Id, string Name, bool IsVisible, bool IsLocked, int Order);

public sealed record DiagramBriefPage(
    string Id,
    string Name,
    double Width,
    double Height,
    IReadOnlyList<DiagramBriefLayer> Layers,
    IReadOnlyList<DiagramBriefNode> Nodes,
    IReadOnlyList<DiagramBriefEdge> Edges,
    IReadOnlyList<DiagramBriefStencilUsage> StencilsUsed);

public sealed record DiagramBrief(
    string Id,
    string Title,
    int ActivePageIndex,
    IReadOnlyList<DiagramBriefPage> Pages,
    IReadOnlyList<DiagramBriefStencilUsage> StencilsUsed);

/// <summary>Builds a deterministic implementation brief from a diagram document.</summary>
public static class DiagramImplementationBrief
{
    public static DiagramBrief Build(DiagramDocument document)
    {
        document.EnsurePages();
        var pages = document.Pages.Select(BuildPage).ToList();
        var stencilsUsed = document.Pages
            .SelectMany(p => p.Nodes)
            .Where(n => !string.IsNullOrWhiteSpace(n.StencilId))
            .GroupBy(n => n.StencilId, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DiagramBriefStencilUsage(g.Key, g.Count()))
            .ToList();

        return new DiagramBrief(document.Id, document.Title, document.ActivePageIndex, pages, stencilsUsed);
    }

    private static DiagramBriefPage BuildPage(DiagramPage page)
    {
        var stencilByNodeId = page.Nodes.ToDictionary(n => n.Id, n => n.StencilId, StringComparer.Ordinal);
        var nodes = page.Nodes
            .OrderBy(n => n.ZIndex)
            .ThenBy(n => n.Y)
            .ThenBy(n => n.X)
            .Select(n => new DiagramBriefNode(n.Id, n.StencilId, ExtractLabel(n), n.X, n.Y, n.W, n.H))
            .ToList();

        var edges = page.Edges
            .OrderBy(e => e.ZIndex)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .Select(e => new DiagramBriefEdge(
                e.Id,
                e.SourceNodeId,
                e.SourceNodeId is null ? null : stencilByNodeId.GetValueOrDefault(e.SourceNodeId),
                e.TargetNodeId,
                e.TargetNodeId is null ? null : stencilByNodeId.GetValueOrDefault(e.TargetNodeId),
                e.Label,
                e.Routing))
            .ToList();

        var layers = page.Layers
            .OrderBy(l => l.Order)
            .ThenBy(l => l.Name, StringComparer.Ordinal)
            .Select(l => new DiagramBriefLayer(l.Id, l.Name, l.IsVisible, l.IsLocked, l.Order))
            .ToList();

        var stencilsUsed = page.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.StencilId))
            .GroupBy(n => n.StencilId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DiagramBriefStencilUsage(g.Key, g.Count()))
            .ToList();

        return new DiagramBriefPage(page.Id, page.Name, page.Width, page.Height, layers, nodes, edges, stencilsUsed);
    }

    private static string? ExtractLabel(DiagramNode node)
    {
        foreach (var key in new[] { "label", "name", "title", "text" })
        {
            if (node.Data.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }
}
