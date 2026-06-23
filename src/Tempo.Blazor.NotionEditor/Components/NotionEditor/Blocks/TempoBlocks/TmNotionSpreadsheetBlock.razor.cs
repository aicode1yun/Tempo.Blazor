using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Files;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.Spreadsheet.Enums;
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
    [Parameter] public EventCallback                          OnRemoveRequested { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private bool             _creating;
    private bool             _editorOpen;
    private bool             _insertDialogOpen;
    private bool             _notFound;
    private bool             _loadingWorkbook;
    private Guid             _subscribedId = Guid.Empty;
    private bool             _changeHandlerRegistered;
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
    private static readonly Type? SpreadsheetComponentType = ResolveSpreadsheetComponentType();

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

    private Dictionary<string, object?> EmbeddedSpreadsheetParameters => new()
    {
        ["InitialWorkbook"] = _workbook,
        ["Mode"] = SpreadsheetMode.Embedded,
        ["Height"] = EmbedHeight,
        ["Width"] = "100%",
        ["Class"] = "tm-notion-spreadsheet-block__spreadsheet"
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _resizeInitialized  = false;
        _captionInitialized = false;
        _captionDirty       = false;

        _notFound = false;

        var id = Content?.SpreadsheetDocumentId ?? Guid.Empty;
        if (id != Guid.Empty && id != _loadedDocumentId)
        {
            _loadingWorkbook  = true;
            _workbook         = null;
            _loadedDocumentId = id;

            // Detect deletion via the library so a dangling reference degrades gracefully.
            if (Context?.DocumentLibraryProvider is not null)
            {
                try
                {
                    var entry = await Context.DocumentLibraryProvider.GetEntryAsync(
                        DocumentLibrary.TempoDocumentKind.Spreadsheet, id);
                    if (entry is null)
                    {
                        _notFound = true;
                        _loadingWorkbook = false;
                        return;
                    }
                }
                catch { }
            }

            if (Context?.SpreadsheetDocumentProvider is not null)
            {
                try { _workbook = await Context.SpreadsheetDocumentProvider.GetSpreadsheetDocumentAsync(id); }
                catch { }
            }
            if (_workbook is null || _workbook.Sheets.Count == 0)
                _workbook = new SpreadsheetWorkbook();
            _loadingWorkbook = false;
        }

        await EnsureSubscribedAsync();
    }

    private async Task EnsureSubscribedAsync()
    {
        var notifier = Context?.DocumentChangeNotifier;
        if (notifier is null || Content is null)
        {
            return;
        }

        var id = Content.SpreadsheetDocumentId;
        if (id == _subscribedId)
        {
            return;
        }

        if (!_changeHandlerRegistered)
        {
            notifier.Changed += OnRemoteChangedAsync;
            _changeHandlerRegistered = true;
        }

        if (_subscribedId != Guid.Empty)
        {
            await notifier.UnsubscribeAsync(DocumentLibrary.TempoDocumentKind.Spreadsheet, _subscribedId);
        }

        _subscribedId = id;
        if (id != Guid.Empty)
        {
            await notifier.SubscribeAsync(DocumentLibrary.TempoDocumentKind.Spreadsheet, id);
        }
    }

    private async Task OnRemoteChangedAsync(DocumentLibrary.TempoDocumentChange change, CancellationToken ct)
    {
        if (change.Kind != DocumentLibrary.TempoDocumentKind.Spreadsheet || change.DocumentId != _subscribedId)
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            if (change.ChangeType == DocumentLibrary.TempoDocumentChangeType.Deleted)
            {
                _notFound = true;
                StateHasChanged();
                return;
            }

            if (Context?.SpreadsheetDocumentProvider is not null)
            {
                try { _workbook = await Context.SpreadsheetDocumentProvider.GetSpreadsheetDocumentAsync(change.DocumentId); }
                catch { }
                if (_workbook is null || _workbook.Sheets.Count == 0)
                {
                    _workbook = new SpreadsheetWorkbook();
                }
                _embedKey++;
                StateHasChanged();
            }
        });
    }

    private async Task HandleInsertSelectedAsync(DocumentOpenResult result)
    {
        _insertDialogOpen = false;
        if (result.Mode == DocumentOpenMode.Copy && Context?.SpreadsheetDocumentProvider is not null)
        {
            var source = await Context.SpreadsheetDocumentProvider.GetSpreadsheetDocumentAsync(result.DocumentId);
            if (source is null)
            {
                return;
            }
            var copy = source.Clone();
            var (newId, _) = await Context.SpreadsheetDocumentProvider.CreateSpreadsheetDocumentAsync(string.Empty);
            await Context.SpreadsheetDocumentProvider.SaveSpreadsheetDocumentAsync(newId, copy);
            await OnContentSaved.InvokeAsync(new SpreadsheetBlockContent { SpreadsheetDocumentId = newId });
        }
        else
        {
            await OnContentSaved.InvokeAsync(new SpreadsheetBlockContent { SpreadsheetDocumentId = result.DocumentId });
        }
    }

    private async Task HandleRemoveAsync() => await OnRemoveRequested.InvokeAsync();

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
        var notifier = Context?.DocumentChangeNotifier;
        if (notifier is not null)
        {
            if (_changeHandlerRegistered)
            {
                notifier.Changed -= OnRemoteChangedAsync;
            }
            if (_subscribedId != Guid.Empty)
            {
                try { await notifier.UnsubscribeAsync(DocumentLibrary.TempoDocumentKind.Spreadsheet, _subscribedId); }
                catch { }
            }
        }

        if (_resizeInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyResizeHandle", _embedWrapRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }

    private static Type? ResolveSpreadsheetComponentType()
        => Type.GetType("Tempo.Blazor.Components.Spreadsheet.TmSpreadsheet, Tempo.Blazor.Spreadsheet")
           ?? AppDomain.CurrentDomain.GetAssemblies()
               .Select(assembly => assembly.GetType("Tempo.Blazor.Components.Spreadsheet.TmSpreadsheet", throwOnError: false))
               .FirstOrDefault(type => type is not null);
}
