namespace Tempo.Blazor.Components.Spreadsheet.Enums;

/// <summary>
/// Controls the display and interaction mode of the <see cref="TmSpreadsheet"/> component.
/// </summary>
public enum SpreadsheetMode
{
    /// <summary>Full editor with toolbar, formula bar, and cell editing. Default behaviour.</summary>
    Full = 0,

    /// <summary>
    /// Compact read-only view intended for embedding inside other components (e.g. a Notion-style block).
    /// Toolbar and formula bar are hidden; sheet tabs remain functional; canvas grid is read-only.
    /// </summary>
    Embedded = 1,
}
