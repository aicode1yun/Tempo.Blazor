namespace Tempo.Blazor.Components.Spreadsheet.Enums;

/// <summary>Determines which textual representation of a cell the search engine inspects.</summary>
public enum SpreadsheetSearchIn
{
    /// <summary>Search the displayed (formatted) cell values.</summary>
    Values,

    /// <summary>Search cell formulas and the re-editable text of non-formula cells.</summary>
    Formulas
}
