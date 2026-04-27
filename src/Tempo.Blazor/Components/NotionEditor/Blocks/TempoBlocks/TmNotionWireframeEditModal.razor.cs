using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;

public partial class TmNotionWireframeEditModal : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public Guid WireframeDocumentId { get; set; }

    [Parameter]
    public IWireframeDocumentProvider? Provider { get; set; }

    [Parameter] public EventCallback<(WireframeDocument Document, string SvgPreview)> OnSaved     { get; set; }
    [Parameter] public EventCallback                                                    OnDiscarded { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private TmWireframeEditor? _editorRef;
    private WireframeDocument? _document;
    private bool                _loading = true;
    private bool                _saving;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        if (Provider is not null)
        {
            try { _document = await Provider.GetWireframeDocumentAsync(WireframeDocumentId); }
            catch { }
        }
        if (_document is null)
        {
            _document = new WireframeDocument();
            _document.EnsureActivePage();
        }
        _loading = false;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        if (_document is null || _saving) return;
        _saving = true;
        string svg = string.Empty;
        try
        {
            if (_editorRef is not null)
                svg = await _editorRef.ExportSvgAsync();
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
