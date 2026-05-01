namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Context passed to <see cref="TmPivotTable{TItem}.RowHeaderTemplate"/>.
/// Contains the row dimension header text and its position in the hierarchy.
/// </summary>
public class PivotRowHeaderContext
{
    /// <summary>The display text of this row header cell.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Display values of all row dimension keys for this row.</summary>
    public IReadOnlyList<string> RowFieldValues { get; init; } = Array.Empty<string>();

    /// <summary>The hierarchy level (0-based) of this header.</summary>
    public int Level { get; init; }

    /// <summary>Zero-based row index of the leaf row.</summary>
    public int RowIndex { get; init; }

    /// <summary>The number of rows this header spans.</summary>
    public int RowSpan { get; init; }
}
