namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Describes what was matched during a diagram search.</summary>
public enum DiagramSearchMatchType
{
    /// <summary>Matched by node or edge identifier.</summary>
    Id,

    /// <summary>Matched by stencil identifier.</summary>
    StencilId,

    /// <summary>Matched by a label or text property in Data.</summary>
    Label,

    /// <summary>Matched by any other Data value.</summary>
    Data,
}

/// <summary>A single result from a diagram search operation.</summary>
public sealed class DiagramSearchResult
{
    /// <summary>When set, the matched node identifier.</summary>
    public string? NodeId { get; set; }

    /// <summary>When set, the matched edge identifier.</summary>
    public string? EdgeId { get; set; }

    /// <summary>What kind of field produced the match.</summary>
    public DiagramSearchMatchType MatchType { get; set; }

    /// <summary>The matching text fragment (useful for highlighting).</summary>
    public string MatchedText { get; set; } = string.Empty;

    /// <summary>When searching all pages, the index of the page containing the match.</summary>
    public int? PageIndex { get; set; }

    /// <summary>
    /// For <see cref="DiagramSearchMatchType.Data"/> matches, the dictionary key that was matched.
    /// For <see cref="DiagramSearchMatchType.Label"/> on edges, this is <c>"Label"</c>.
    /// Null for <see cref="DiagramSearchMatchType.Id"/>, <see cref="DiagramSearchMatchType.StencilId"/>
    /// and <see cref="DiagramSearchMatchType.Data"/> when matched on an edge connector type.
    /// </summary>
    public string? DataKey { get; set; }
}
