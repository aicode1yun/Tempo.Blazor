namespace Tempo.Blazor.Components.Charts;

/// <summary>Represents a node in a Sankey flow diagram.</summary>
public sealed record SankeyNode
{
    /// <summary>Gets the unique identifier used by links to reference the node.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the user-visible node label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the optional CSS color used to render the node.</summary>
    public string? Color { get; init; }
}

/// <summary>Represents a directed value flow between two Sankey nodes.</summary>
public sealed record SankeyLink
{
    /// <summary>Gets the identifier of the source node.</summary>
    public required string SourceId { get; init; }

    /// <summary>Gets the identifier of the target node.</summary>
    public required string TargetId { get; init; }

    /// <summary>Gets the positive value carried by the flow.</summary>
    public required double Value { get; init; }

    /// <summary>Gets the optional CSS color used to render the flow.</summary>
    public string? Color { get; init; }
}

/// <summary>Contains the nodes and directed links rendered by a Sankey chart.</summary>
public sealed record SankeyData
{
    /// <summary>Gets the nodes in declaration order.</summary>
    public required IReadOnlyList<SankeyNode> Nodes { get; init; }

    /// <summary>Gets the links in declaration order.</summary>
    public required IReadOnlyList<SankeyLink> Links { get; init; }
}

/// <summary>Identifies why Sankey layout could not be produced.</summary>
public enum SankeyLayoutErrorKind
{
    /// <summary>The input is valid and layout succeeded.</summary>
    None,

    /// <summary>The directed graph contains a cycle.</summary>
    Cycle,

    /// <summary>The input contains malformed nodes, links, values, or layout dimensions.</summary>
    InvalidData,
}
