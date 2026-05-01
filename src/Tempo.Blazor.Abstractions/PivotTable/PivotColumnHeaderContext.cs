namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Context passed to <see cref="TmPivotTable{TItem}.ColumnHeaderTemplate"/>.
/// Contains the column dimension header text and its position in the hierarchy.
/// </summary>
public class PivotColumnHeaderContext
{
    /// <summary>The display text of this column header cell.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Display values of all column dimension keys for this column.</summary>
    public IReadOnlyList<string> ColumnFieldValues { get; init; } = Array.Empty<string>();

    /// <summary>The hierarchy level (0-based) of this header.</summary>
    public int Level { get; init; }

    /// <summary>Zero-based column index of the leaf column.</summary>
    public int ColumnIndex { get; init; }

    /// <summary>The number of columns this header spans.</summary>
    public int ColSpan { get; init; }
}
