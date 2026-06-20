namespace Tempo.Blazor.Components.Spreadsheet.Data;

/// <summary>
/// A single search match within a workbook.
/// </summary>
/// <param name="SheetIndex">Zero-based index of the sheet containing the match.</param>
/// <param name="SheetName">Name of the sheet containing the match.</param>
/// <param name="CellRef">A1 reference of the matched cell.</param>
/// <param name="MatchStart">Zero-based start offset of the match within the searchable cell text.</param>
/// <param name="MatchLength">Length of the matched substring.</param>
public readonly record struct SpreadsheetSearchHit(
    int SheetIndex,
    string SheetName,
    string CellRef,
    int MatchStart,
    int MatchLength);
