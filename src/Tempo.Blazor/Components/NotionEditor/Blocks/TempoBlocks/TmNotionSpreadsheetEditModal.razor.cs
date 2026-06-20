using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;

public partial class TmNotionSpreadsheetEditModal : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public Guid SpreadsheetDocumentId { get; set; }

    [Parameter]
    public ISpreadsheetDocumentProvider? Provider { get; set; }

    [Parameter] public EventCallback<SpreadsheetWorkbook> OnSaved     { get; set; }
    [Parameter] public EventCallback                       OnDiscarded { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private TmSpreadsheet? _spreadsheetRef;
    private SpreadsheetWorkbook _workbook = new();
    private bool _loading = true;
    private bool _saving;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        if (Provider is not null)
        {
            try
            {
                var loaded = await Provider.GetSpreadsheetDocumentAsync(SpreadsheetDocumentId);
                if (loaded is not null)
                    _workbook = loaded;
            }
            catch { }
        }
        if (_workbook.Sheets.Count == 0)
            _workbook.AddSheet("Sheet1"); // constructor may not have run (e.g. deserialized from JSON)
        _loading = false;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        if (_saving) return;
        _saving = true;
        var workbookToSave = _spreadsheetRef?.Workbook ?? _workbook;
        try
        {
            if (Provider is not null)
            {
                try { workbookToSave = await Provider.SaveSpreadsheetDocumentAsync(SpreadsheetDocumentId, workbookToSave); }
                catch { }
            }
        }
        finally
        {
            _saving = false;
        }
        await OnSaved.InvokeAsync(workbookToSave);
    }

    private async Task DiscardAsync() => await OnDiscarded.InvokeAsync();
}
