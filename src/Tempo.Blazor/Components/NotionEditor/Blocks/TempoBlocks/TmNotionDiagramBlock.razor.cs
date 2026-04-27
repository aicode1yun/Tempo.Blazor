using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;

public partial class TmNotionDiagramBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime        JS { get; set; } = default!;
    [Inject] private IServiceProvider  SP { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IDiagramBlockContent? Content     { get; set; }
    [Parameter] public bool                  ReadOnly    { get; set; }

    [Parameter] public EventCallback<DiagramBlockContent> OnContentSaved { get; set; }
    [Parameter] public EventCallback                      OnFocused      { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private IDiagramExportService?                _exportService;
    private bool                                   _creating;
    private bool                                   _editorOpen;
    private ElementReference                       _previewWrapRef;
    private ElementReference                       _captionRef;
    private DotNetObjectReference<TmNotionDiagramBlock>? _dotNetRef;
    private bool                                   _resizeInitialized;
    private bool                                   _captionDirty;
    private bool                                   _captionInitialized;
    private IDiagramBlockContent?                  _lastContent;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _sizeStyle
    {
        get
        {
            var parts = new System.Text.StringBuilder();
            if (Content?.Width is int w && w > 0)
                parts.Append($"width:{w}px;max-width:100%;");
            if (Content?.Height is int h && h > 0)
                parts.Append($"height:{h}px;");
            return parts.ToString();
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        _exportService = SP.GetService<IDiagramExportService>();
    }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _resizeInitialized  = false;
        _captionInitialized = false;
        _captionDirty       = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly || Content is null || Content.DiagramDocumentId == Guid.Empty) return;

        if (!_resizeInitialized)
        {
            _resizeInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("tmNotionEditor.initResizeHandle", _previewWrapRef, _dotNetRef); }
            catch { }
        }

        if (!_captionInitialized)
        {
            _captionInitialized = true;
            var caption = Content.Caption ?? string.Empty;
            if (!string.IsNullOrEmpty(caption))
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _captionRef, caption); }
                catch { }
            }
        }
    }

    // ── JS callback ───────────────────────────────────────────────────────────

    [JSInvokable]
    public async Task OnResize(int width, int height)
    {
        if (Content is null) return;
        var updated = new DiagramBlockContent
        {
            DiagramDocumentId = Content.DiagramDocumentId,
            SvgPreviewCache   = Content.SvgPreviewCache,
            Width             = width,
            Height            = height,
            Caption           = Content.Caption
        };
        await OnContentSaved.InvokeAsync(updated);
    }

    // ── Create / Edit ────────────────────────────────────────────────────────

    private async Task CreateDiagramAsync()
    {
        if (Context.DiagramDocumentProvider is null || _creating) return;
        _creating = true;
        StateHasChanged();
        try
        {
            var (id, _) = await Context.DiagramDocumentProvider.CreateDiagramDocumentAsync(string.Empty);
            var created  = new DiagramBlockContent { DiagramDocumentId = id };
            await OnContentSaved.InvokeAsync(created);
            _editorOpen = true;
        }
        catch { }
        finally
        {
            _creating = false;
        }
    }

    private Task OpenEditorAsync()
    {
        _editorOpen = true;
        return Task.CompletedTask;
    }

    private async Task HandleEditorSavedAsync((DiagramDocument Document, string SvgPreview) result)
    {
        _editorOpen = false;
        if (Context.DiagramDocumentProvider is null || Content is null) return;
        await Context.DiagramDocumentProvider.SaveDiagramDocumentAsync(
            Content.DiagramDocumentId, result.Document);
        var updated = new DiagramBlockContent
        {
            DiagramDocumentId = Content.DiagramDocumentId,
            SvgPreviewCache   = result.SvgPreview,
            Width             = Content.Width,
            Height            = Content.Height,
            Caption           = Content.Caption
        };
        await OnContentSaved.InvokeAsync(updated);
    }

    private Task HandleEditorDiscardedAsync()
    {
        _editorOpen = false;
        return Task.CompletedTask;
    }

    // ── Caption ───────────────────────────────────────────────────────────────

    private async Task OnCaptionBlurAsync()
    {
        if (!_captionDirty || ReadOnly || Content is null) return;
        _captionDirty = false;
        try
        {
            var html    = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _captionRef);
            var updated = new DiagramBlockContent
            {
                DiagramDocumentId = Content.DiagramDocumentId,
                SvgPreviewCache   = Content.SvgPreviewCache,
                Width             = Content.Width,
                Height            = Content.Height,
                Caption           = string.IsNullOrWhiteSpace(html) ? null : html
            };
            await OnContentSaved.InvokeAsync(updated);
        }
        catch { }
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_resizeInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyResizeHandle", _previewWrapRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
