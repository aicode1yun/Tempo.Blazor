using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>The direction of a sort level.</summary>
public enum SpreadsheetSortDirection
{
    /// <summary>Smallest to largest / A→Z / oldest to newest.</summary>
    Ascending,

    /// <summary>Largest to smallest / Z→A / newest to oldest.</summary>
    Descending
}

/// <summary>What a sort level orders by.</summary>
public enum SpreadsheetSortOn
{
    /// <summary>The cell value (numbers, then text, then booleans; blanks always last).</summary>
    Value,

    /// <summary>The cell background fill colour.</summary>
    CellColor,

    /// <summary>The font (foreground) colour.</summary>
    FontColor
}

/// <summary>
/// A single level of a (potentially multi-level) sort.
/// </summary>
public sealed class SpreadsheetSortLevel
{
    /// <summary>
    /// The key index: a column index when sorting rows (the default), or a row index when
    /// <see cref="SpreadsheetSortSpec.ByRows"/> is true.
    /// </summary>
    public int KeyIndex { get; set; }

    /// <summary>The sort direction.</summary>
    public SpreadsheetSortDirection Direction { get; set; } = SpreadsheetSortDirection.Ascending;

    /// <summary>What the level sorts on.</summary>
    public SpreadsheetSortOn SortOn { get; set; } = SpreadsheetSortOn.Value;

    /// <summary>
    /// For colour sorts: the hex colour pinned to the <see cref="Direction"/> end (top for ascending,
    /// bottom for descending). Null for value sorts.
    /// </summary>
    public string? ColorKey { get; set; }

    /// <summary>Whether text comparison is case-sensitive.</summary>
    public bool CaseSensitive { get; set; }

    /// <summary>Creates a deep copy of this sort level.</summary>
    public SpreadsheetSortLevel Clone() => new()
    {
        KeyIndex = KeyIndex,
        Direction = Direction,
        SortOn = SortOn,
        ColorKey = ColorKey,
        CaseSensitive = CaseSensitive
    };
}

/// <summary>
/// A full sort specification: the range to sort, whether it has a header row, the ordered list of
/// sort levels, and the orientation.
/// </summary>
public sealed class SpreadsheetSortSpec
{
    /// <summary>The range to sort.</summary>
    public SpreadsheetRange Range { get; set; }

    /// <summary>Whether the first row (or column when <see cref="ByRows"/>) is a header and stays in place.</summary>
    public bool HasHeader { get; set; }

    /// <summary>The ordered sort levels (the first is the primary key).</summary>
    public List<SpreadsheetSortLevel> Levels { get; set; } = [];

    /// <summary>
    /// When false (default) rows are reordered by column keys. When true columns are reordered by
    /// row keys (left-to-right sort).
    /// </summary>
    public bool ByRows { get; set; }

    /// <summary>Creates a sort specification over the given range.</summary>
    public SpreadsheetSortSpec(SpreadsheetRange range)
    {
        Range = range;
    }

    /// <summary>Creates a deep copy of this sort specification.</summary>
    public SpreadsheetSortSpec Clone() => new(new SpreadsheetRange(Range.StartRow, Range.StartCol, Range.EndRow, Range.EndCol))
    {
        HasHeader = HasHeader,
        ByRows = ByRows,
        Levels = Levels.Select(l => l.Clone()).ToList()
    };
}
