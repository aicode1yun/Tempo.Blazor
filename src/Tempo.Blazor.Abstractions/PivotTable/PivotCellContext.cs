namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Context passed to <see cref="TmPivotTable{TItem}.DataCellTemplate"/>.
/// Contains the aggregated cell value and its position in the pivot matrix.
/// </summary>
public class PivotCellContext
{
    /// <summary>The raw aggregated value (may be null).</summary>
    public object? Value { get; init; }

    /// <summary>The formatted string representation of the value.</summary>
    public string FormattedValue { get; init; } = string.Empty;

    /// <summary>Whether the cell has no data.</summary>
    public bool IsNull { get; init; }

    /// <summary>Zero-based row index of the leaf row.</summary>
    public int RowIndex { get; init; }

    /// <summary>Zero-based column index of the leaf column.</summary>
    public int ColumnIndex { get; init; }

    /// <summary>Display values of the row dimension keys for this cell.</summary>
    public IReadOnlyList<string> RowFieldValues { get; init; } = Array.Empty<string>();

    /// <summary>Display values of the column dimension keys for this cell.</summary>
    public IReadOnlyList<string> ColumnFieldValues { get; init; } = Array.Empty<string>();

    /// <summary>The zero-based index of the value field (measure) within the leaf column.</summary>
    public int ValueFieldIndex { get; init; }
}
