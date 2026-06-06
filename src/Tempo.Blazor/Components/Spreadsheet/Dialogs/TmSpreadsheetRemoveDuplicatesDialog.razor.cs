using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Dialogs;

/// <summary>
/// The Remove Duplicates dialog. Lists every column in the range with a checkbox, offers a
/// "my data has headers" toggle (which switches the column labels between header text and the column
/// letter), select-all / deselect-all helpers and a case-sensitivity option. Applying yields a
/// <see cref="SpreadsheetRemoveDuplicatesOptions"/>. All text is localized.
/// </summary>
public partial class TmSpreadsheetRemoveDuplicatesDialog
{
    private readonly HashSet<int> _selected = [];
    private bool _hasHeader = true;
    private bool _caseSensitive;

    /// <summary>The range being deduplicated.</summary>
    [Parameter, EditorRequired] public SpreadsheetRange Range { get; set; } = null!;

    /// <summary>
    /// Header text per absolute column index, used to label columns when <c>my data has headers</c>
    /// is on. Columns without an entry fall back to their column letter.
    /// </summary>
    [Parameter] public IReadOnlyDictionary<int, string>? HeaderLabels { get; set; }

    /// <summary>Raised when the user applies the dialog.</summary>
    [Parameter] public EventCallback<SpreadsheetRemoveDuplicatesOptions> OnApply { get; set; }

    /// <summary>Raised when the dialog is dismissed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private IReadOnlyList<int> Columns { get; set; } = [];

    protected override void OnParametersSet()
    {
        Columns = Enumerable.Range(Range.StartCol, Range.ColumnCount).ToList();

        // Default to all columns selected the first time the dialog is shown.
        if (_selected.Count == 0)
            foreach (var c in Columns)
                _selected.Add(c);
    }

    private string ColumnLabel(int col)
    {
        if (_hasHeader && HeaderLabels is not null && HeaderLabels.TryGetValue(col, out var label) && !string.IsNullOrWhiteSpace(label))
            return label;

        return string.Format(Loc["TmSpreadsheet_Dedup_ColumnLabel"], SpreadsheetRange.ColumnIndexToLetters(col));
    }

    private bool IsSelected(int col) => _selected.Contains(col);

    private void ToggleColumn(int col, bool selected)
    {
        if (selected)
            _selected.Add(col);
        else
            _selected.Remove(col);
    }

    private void SelectAll()
    {
        foreach (var c in Columns)
            _selected.Add(c);
    }

    private void DeselectAll() => _selected.Clear();

    private Task Apply()
    {
        var options = new SpreadsheetRemoveDuplicatesOptions
        {
            KeyColumns = _selected.OrderBy(c => c).ToList(),
            HasHeader = _hasHeader,
            CaseSensitive = _caseSensitive
        };

        return OnApply.InvokeAsync(options);
    }

    private Task Close() => OnClose.InvokeAsync();

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await Close();
    }
}
