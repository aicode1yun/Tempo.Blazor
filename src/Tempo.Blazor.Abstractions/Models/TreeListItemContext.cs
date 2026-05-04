namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Represents a flattened tree-list row with hierarchy metadata.</summary>
/// <typeparam name="TItem">The underlying data type.</typeparam>
public sealed class TreeListItemContext<TItem>
{
    /// <summary>The original data item.</summary>
    public required TItem Item { get; init; }

    /// <summary>Zero-based depth level in the tree.</summary>
    public int Level { get; init; }

    /// <summary>Whether this row is currently expanded.</summary>
    public bool IsExpanded { get; set; }

    /// <summary>Whether this row has child rows.</summary>
    public bool HasChildren { get; init; }

    /// <summary>Whether this row should be rendered (all ancestors expanded).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Unique identifier of this item.</summary>
    public required object Id { get; init; }

    /// <summary>Identifier of the parent item, or <c>null</c> for root rows.</summary>
    public required object? ParentId { get; init; }
}
