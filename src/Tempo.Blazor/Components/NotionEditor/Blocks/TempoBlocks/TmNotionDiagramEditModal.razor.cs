using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;

public partial class TmNotionDiagramEditModal : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public Guid DiagramDocumentId { get; set; }

    [Parameter, EditorRequired]
    public IDiagramDocumentProvider Provider { get; set; } = default!;

    [Parameter]
    public IDiagramExportService? ExportService { get; set; }

    [Parameter] public EventCallback<(DiagramDocument Document, string SvgPreview)> OnSaved     { get; set; }
    [Parameter] public EventCallback                                                  OnDiscarded { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private DiagramDocument? _document;
    private bool              _loading = true;
    private bool              _saving;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        _document = await Provider.GetDiagramDocumentAsync(DiagramDocumentId);
        if (_document is null)
            _document = new DiagramDocument { Id = DiagramDocumentId.ToString() };
        _loading = false;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        if (_document is null || _saving) return;
        _saving = true;
        var svg = DiagramThumbnailSvgGenerator.Generate(_document);
        try
        {
            if (ExportService is not null)
                svg = await ExportService.ExportSvgAsync(_document, new DiagramExportOptions { Padding = 20 });
        }
        catch { }
        finally
        {
            _saving = false;
        }
        await OnSaved.InvokeAsync((_document, svg));
    }

    private async Task DiscardAsync() => await OnDiscarded.InvokeAsync();
}
