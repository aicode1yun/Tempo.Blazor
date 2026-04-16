using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>
/// Searches a diagram document for nodes and edges matching a free-text query.
/// </summary>
public static class DiagramSearchService
{
    /// <summary>
    /// Searches <paramref name="document"/> for nodes and edges whose
    /// <see cref="DiagramNode.Id"/>, <see cref="DiagramNode.StencilId"/>,
    /// <c>Data["label"]</c>, <c>Data["name"]</c> or any other data value contains
    /// <paramref name="query"/> (case-insensitive).
    /// </summary>
    public static List<DiagramSearchResult> Search(DiagramDocument? document, string? query)
    {
        var results = new List<DiagramSearchResult>();
        if (document is null || string.IsNullOrWhiteSpace(query))
            return results;

        var term = query.Trim();

        foreach (var node in document.Nodes)
        {
            var nodeResults = SearchNode(node, term);
            results.AddRange(nodeResults);
        }

        foreach (var edge in document.Edges)
        {
            var edgeResults = SearchEdge(edge, term);
            results.AddRange(edgeResults);
        }

        return results;
    }

    private static IEnumerable<DiagramSearchResult> SearchNode(DiagramNode node, string term)
    {
        if (Contains(node.Id, term))
        {
            yield return new DiagramSearchResult
            {
                NodeId = node.Id,
                MatchType = DiagramSearchMatchType.Id,
                MatchedText = node.Id,
            };
            yield break;
        }

        if (Contains(node.StencilId, term))
        {
            yield return new DiagramSearchResult
            {
                NodeId = node.Id,
                MatchType = DiagramSearchMatchType.StencilId,
                MatchedText = node.StencilId,
            };
            yield break;
        }

        // Label-like data keys
        foreach (var key in new[] { "label", "name", "title" })
        {
            if (node.Data.TryGetValue(key, out var value) && Contains(value?.ToString(), term))
            {
                yield return new DiagramSearchResult
                {
                    NodeId = node.Id,
                    MatchType = DiagramSearchMatchType.Label,
                    MatchedText = value?.ToString() ?? string.Empty,
                };
                yield break;
            }
        }

        // Any data value
        foreach (var kvp in node.Data)
        {
            if (Contains(kvp.Value?.ToString(), term))
            {
                yield return new DiagramSearchResult
                {
                    NodeId = node.Id,
                    MatchType = DiagramSearchMatchType.Data,
                    MatchedText = kvp.Value?.ToString() ?? string.Empty,
                };
                yield break;
            }
        }
    }

    private static IEnumerable<DiagramSearchResult> SearchEdge(DiagramEdge edge, string term)
    {
        if (Contains(edge.Id, term))
        {
            yield return new DiagramSearchResult
            {
                EdgeId = edge.Id,
                MatchType = DiagramSearchMatchType.Id,
                MatchedText = edge.Id,
            };
            yield break;
        }

        if (Contains(edge.Label, term))
        {
            yield return new DiagramSearchResult
            {
                EdgeId = edge.Id,
                MatchType = DiagramSearchMatchType.Label,
                MatchedText = edge.Label ?? string.Empty,
            };
            yield break;
        }

        if (Contains(edge.ConnectorType, term))
        {
            yield return new DiagramSearchResult
            {
                EdgeId = edge.Id,
                MatchType = DiagramSearchMatchType.Data,
                MatchedText = edge.ConnectorType ?? string.Empty,
            };
            yield break;
        }
    }

    private static bool Contains(string? text, string term)
        => text is not null && text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
