using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Dialogs;

/// <summary>
/// Name Manager dialog that lists all named ranges in the workbook, allows filtering,
/// and raises events for New / Edit / Delete.
/// </summary>
public partial class TmSpreadsheetNameManagerDialog
{
    private string _filter = string.Empty;
    private SpreadsheetNamedRange? _selectedRange;

    /// <summary>The workbook whose named ranges are being managed.</summary>
    [Parameter, EditorRequired] public SpreadsheetWorkbook Workbook { get; set; } = null!;

    /// <summary>Raised when the user wants to create a new named range.</summary>
    [Parameter] public EventCallback OnNew { get; set; }

    /// <summary>Raised when the user wants to edit the selected named range.</summary>
    [Parameter] public EventCallback<SpreadsheetNamedRange> OnEdit { get; set; }

    /// <summary>Raised when the user wants to delete the selected named range.</summary>
    [Parameter] public EventCallback<SpreadsheetNamedRange> OnDelete { get; set; }

    /// <summary>Raised when the dialog is dismissed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private IReadOnlyList<SpreadsheetNamedRange> FilteredRanges =>
        string.IsNullOrWhiteSpace(_filter)
            ? Workbook.NamedRanges
            : Workbook.NamedRanges
                .Where(r => r.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

    private void SelectRange(SpreadsheetNamedRange range)
    {
        _selectedRange = range;
    }

    private string GetRowClass(SpreadsheetNamedRange range)
    {
        return _selectedRange == range ? "tm-spreadsheet-name-manager__row--selected" : string.Empty;
    }

    private string? GetPreviewValue(SpreadsheetNamedRange range)
    {
        try
        {
            if (range.Scope == NamedRangeScope.Sheet && range.SheetIndex.HasValue)
            {
                var sheet = Workbook.Sheets[range.SheetIndex.Value];
                var cellRef = range.RefersTo.Replace("$", "").ToUpperInvariant();
                if (sheet.Cells.TryGetValue(cellRef, out var cell))
                    return cell.Value?.ToString();
            }
            else
            {
                var active = Workbook.ActiveSheet;
                if (active is not null)
                {
                    var cellRef = range.RefersTo.Replace("$", "").ToUpperInvariant();
                    if (active.Cells.TryGetValue(cellRef, out var cell))
                        return cell.Value?.ToString();
                }
            }
        }
        catch { /* ignore preview errors */ }

        return null;
    }

    private void OnNewClick() => OnNew.InvokeAsync();

    private void OnEditClick()
    {
        if (_selectedRange is not null)
            OnEdit.InvokeAsync(_selectedRange);
    }

    private void OnDeleteClick()
    {
        if (_selectedRange is not null)
            OnDelete.InvokeAsync(_selectedRange);
    }

    private void OnCloseClick() => OnClose.InvokeAsync();

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await OnClose.InvokeAsync();
    }
}
