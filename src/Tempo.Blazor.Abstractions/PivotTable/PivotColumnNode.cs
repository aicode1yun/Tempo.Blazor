namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Represents a node in the column dimension tree of a pivot table result.
/// </summary>
public sealed class PivotColumnNode
{
    /// <summary>The raw key value for this column node.</summary>
    public object? Key { get; init; }

    /// <summary>The display text for this column node.</summary>
    public string DisplayValue { get; init; } = string.Empty;

    /// <summary>Zero-based depth level in the column tree.</summary>
    public int Level { get; init; }

    /// <summary>Child nodes for multi-level column dimensions.</summary>
    public List<PivotColumnNode> Children { get; set; } = [];

    /// <summary>True when this node has no children.</summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>The column index in the rendered table (computed after flattening).</summary>
    public int ColIndex { get; set; }

    /// <summary>The number of leaf columns this node spans (for colspan).</summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>Column total values keyed by value field index.</summary>
    public Dictionary<int, PivotCell> Totals { get; set; } = [];
}
