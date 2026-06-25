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

    [Parameter]
    public IDiagramDocumentProvider? Provider { get; set; }

    [Parameter]
    public IDiagramExportService? ExportService { get; set; }

    [Parameter] public EventCallback<(DiagramDocument Document, string SvgPreview)> OnSaved     { get; set; }
    [Parameter] public EventCallback                                                  OnDiscarded { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private DiagramDocument? _document;
    private bool              _loading = true;
    private bool              _saving;
    private static readonly Type? DiagramEditorComponentType = ResolveDiagramEditorComponentType();

    private Dictionary<string, object?> DiagramEditorParameters => new()
    {
        ["Document"] = _document,
        ["DocumentChanged"] = EventCallback.Factory.Create<DiagramDocument>(this, value => _document = value),
        ["ShowToolbox"] = true,
        ["ShowPropertiesPanel"] = true,
        ["ShowLayersPanel"] = true,
        ["ShowMinimap"] = true,
        ["ShowToolbar"] = true,
        ["ShowGrid"] = true,
        ["ShowPageView"] = true,
        ["Class"] = "tm-notion-diagram-edit-modal__editor"
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        if (Provider is not null)
        {
            try { _document = await Provider.GetDiagramDocumentAsync(DiagramDocumentId); }
            catch { }
        }
        _document ??= new DiagramDocument { Id = DiagramDocumentId.ToString() };
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

    private static Type? ResolveDiagramEditorComponentType()
        => Type.GetType("Tempo.Blazor.Components.Diagram.TmDiagramEditor, Tempo.Blazor.DiagramEditor")
           ?? AppDomain.CurrentDomain.GetAssemblies()
               .Select(assembly => assembly.GetType("Tempo.Blazor.Components.Diagram.TmDiagramEditor", throwOnError: false))
               .FirstOrDefault(type => type is not null);
}
