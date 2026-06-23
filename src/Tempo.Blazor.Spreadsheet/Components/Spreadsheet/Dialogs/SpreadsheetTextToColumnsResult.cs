using Tempo.Blazor.Components.Spreadsheet.Data;

namespace Tempo.Blazor.Components.Spreadsheet.Dialogs;

/// <summary>
/// The outcome of the Text to Columns wizard: the split options and the per-output-column target
/// formats chosen by the user.
/// </summary>
public sealed class SpreadsheetTextToColumnsResult
{
    /// <summary>The separator/split options.</summary>
    public SpreadsheetSeparatorOptions Options { get; init; } = new();

    /// <summary>The target format for each produced column (by index).</summary>
    public IReadOnlyList<SpreadsheetColumnFormat> Formats { get; init; } = [];
}
