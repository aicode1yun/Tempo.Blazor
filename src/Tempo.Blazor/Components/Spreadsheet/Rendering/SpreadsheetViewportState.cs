namespace Tempo.Blazor.Components.Spreadsheet.Rendering;

/// <summary>
/// Describes the current scroll and viewport metrics of a spreadsheet renderer.
/// </summary>
internal readonly record struct SpreadsheetViewportState(
    double ScrollLeft,
    double ScrollTop,
    double Width,
    double Height)
{
    /// <summary>A conservative default viewport used before browser metrics are available.</summary>
    public static SpreadsheetViewportState Default { get; } = new(0, 0, 1024, 480);
}
