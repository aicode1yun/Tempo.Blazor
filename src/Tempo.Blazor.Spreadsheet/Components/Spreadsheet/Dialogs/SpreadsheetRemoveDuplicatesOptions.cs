namespace Tempo.Blazor.Components.Spreadsheet.Dialogs;

/// <summary>
/// The user's choices from the Remove Duplicates dialog: which columns form the duplicate key,
/// whether the range has a header row, and whether text comparison is case-sensitive.
/// </summary>
public sealed class SpreadsheetRemoveDuplicatesOptions
{
    /// <summary>The absolute column indices selected as the duplicate key.</summary>
    public IReadOnlyList<int> KeyColumns { get; init; } = [];

    /// <summary>Whether the first row of the range is a header that must be preserved.</summary>
    public bool HasHeader { get; init; }

    /// <summary>Whether text comparison is case-sensitive.</summary>
    public bool CaseSensitive { get; init; }
}
