using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;

public partial class TmNotionSpreadsheetBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public ISpreadsheetBlockContent? Content     { get; set; }
    [Parameter] public bool                      ReadOnly    { get; set; }

    [Parameter] public EventCallback<SpreadsheetBlockContent> OnContentSaved { get; set; }
    [Parameter] public EventCallback                          OnFocused      { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool             _creating;
    private bool             _editorOpen;
    private bool             _loadingWorkbook;
    private SpreadsheetWorkbook? _workbook;
    private Guid             _loadedDocumentId;
    private int              _embedKey;
    private ElementReference _embedWrapRef;
    private ElementReference _captionRef;
    private DotNetObjectReference<TmNotionSpreadsheetBlock>? _dotNetRef;
    private bool             _resizeInitialized;
    private bool             _captionDirty;
    private bool             _captionInitialized;
    private ISpreadsheetBlockContent? _lastContent;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string EmbedHeight =>
        Content?.Height is int h && h > 0 ? $"{h}px" : "320px";

    private string _sizeStyle
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            if (Content?.Width is int w && w > 0)
                sb.Append($"width:{w}px;max-width:100%;");
            if (Content?.Height is int h && h > 0)
                sb.Append($"height:{h}px;");
            return sb.ToString();
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _resizeInitialized  = false;
        _captionInitialized = false;
        _captionDirty       = false;

        var id = Content?.SpreadsheetDocumentId ?? Guid.Empty;
        if (id != Guid.Empty && id != _loadedDocumentId)
        {
            _loadingWorkbook  = true;
            _workbook         = null;
            _loadedDocumentId = id;
            if (Context?.SpreadsheetDocumentProvider is not null)
            {
                try { _workbook = await Context.SpreadsheetDocumentProvider.GetSpreadsheetDocumentAsync(id); }
                catch { }
            }
            if (_workbook is null || _workbook.Sheets.Count == 0)
                _workbook = new SpreadsheetWorkbook();
            _loadingWorkbook = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ReadOnly || Content is null || Content.SpreadsheetDocumentId == Guid.Empty) return;

        if (!_resizeInitialized)
        {
            _resizeInitialized = true;
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("tmNotionEditor.initResizeHandle", _embedWrapRef, _dotNetRef); }
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
        var updated = new SpreadsheetBlockContent
        {
            SpreadsheetDocumentId = Content.SpreadsheetDocumentId,
            Width   = width,
            Height  = height,
            Caption = Content.Caption
        };
        await OnContentSaved.InvokeAsync(updated);
    }

    // ── Create / Edit ────────────────────────────────────────────────────────

    private async Task CreateSpreadsheetAsync()
    {
        if (_creating) return;
        _creating = true;
        StateHasChanged();
        try
        {
            Guid id;
            if (Context?.SpreadsheetDocumentProvider is not null)
            {
                var (newId, _) = await Context.SpreadsheetDocumentProvider.CreateSpreadsheetDocumentAsync(string.Empty);
                id = newId;
            }
            else
            {
                id = Guid.NewGuid();
            }
            var created = new SpreadsheetBlockContent { SpreadsheetDocumentId = id };
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

    private async Task HandleEditorSavedAsync(SpreadsheetWorkbook workbook)
    {
        _editorOpen = false;
        _workbook   = workbook;
        _embedKey++;
        if (Content is null) return;
        var updated = new SpreadsheetBlockContent
        {
            SpreadsheetDocumentId = Content.SpreadsheetDocumentId,
            Width   = Content.Width,
            Height  = Content.Height,
            Caption = Content.Caption
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
            var updated = new SpreadsheetBlockContent
            {
                SpreadsheetDocumentId = Content.SpreadsheetDocumentId,
                Width   = Content.Width,
                Height  = Content.Height,
                Caption = string.IsNullOrWhiteSpace(html) ? null : html
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
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyResizeHandle", _embedWrapRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
