namespace Tempo.Blazor.Components.Spreadsheet.Enums;

/// <summary>
/// Specifies which renderer is used for the spreadsheet grid surface.
/// </summary>
public enum SpreadsheetRenderMode
{
    /// <summary>Use the HTML/DOM renderer. This is the default and preserves full compatibility.</summary>
    Dom = 0,

    /// <summary>Use the hybrid canvas renderer for larger sheets while keeping HTML for editing and controls.</summary>
    Canvas = 1
}
