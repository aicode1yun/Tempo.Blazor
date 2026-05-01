namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Represents a node in the row dimension tree of a pivot table result.
/// </summary>
public sealed class PivotRowNode
{
    /// <summary>The raw key value for this row node.</summary>
    public object? Key { get; init; }

    /// <summary>The display text for this row node.</summary>
    public string DisplayValue { get; init; } = string.Empty;

    /// <summary>Zero-based depth level in the row tree.</summary>
    public int Level { get; init; }

    /// <summary>Child nodes for multi-level row dimensions.</summary>
    public List<PivotRowNode> Children { get; set; } = [];

    /// <summary>True when this node has no children.</summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>The row index in the rendered table (computed after flattening).</summary>
    public int RowIndex { get; set; }

    /// <summary>The number of leaf rows this node spans (for rowspan).</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Row total values keyed by value field index.</summary>
    public Dictionary<int, PivotCell> Totals { get; set; } = [];
}
