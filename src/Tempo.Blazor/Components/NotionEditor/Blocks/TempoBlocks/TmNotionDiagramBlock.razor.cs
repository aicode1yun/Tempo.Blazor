using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;
using Tempo.Blazor.Components.Files;
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
    [Parameter] public EventCallback                      OnRemoveRequested { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private IDiagramExportService?                _exportService;
    private bool                                   _creating;
    private bool                                   _editorOpen;
    private bool                                   _insertDialogOpen;
    private bool                                   _notFound;
    private string?                                _effectivePreview;
    private Guid                                   _refreshedId = Guid.Empty;
    private Guid                                   _subscribedId = Guid.Empty;
    private bool                                   _changeHandlerRegistered;
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
        _effectivePreview   = Content?.SvgPreviewCache;
        _notFound           = false;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Content is not null
            && Content.DiagramDocumentId != Guid.Empty
            && Context.DocumentLibraryProvider is not null
            && _refreshedId != Content.DiagramDocumentId)
        {
            _refreshedId = Content.DiagramDocumentId;
            await RefreshPreviewAsync(Content.DiagramDocumentId);
        }

        await EnsureSubscribedAsync();
    }

    private async Task EnsureSubscribedAsync()
    {
        var notifier = Context.DocumentChangeNotifier;
        if (notifier is null || Content is null)
        {
            return;
        }

        var id = Content.DiagramDocumentId;
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
            await notifier.UnsubscribeAsync(DocumentLibrary.TempoDocumentKind.Diagram, _subscribedId);
        }

        _subscribedId = id;
        if (id != Guid.Empty)
        {
            await notifier.SubscribeAsync(DocumentLibrary.TempoDocumentKind.Diagram, id);
        }
    }

    private async Task OnRemoteChangedAsync(DocumentLibrary.TempoDocumentChange change, CancellationToken ct)
    {
        if (change.Kind != DocumentLibrary.TempoDocumentKind.Diagram || change.DocumentId != _subscribedId)
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            _refreshedId = Guid.Empty;
            await RefreshPreviewAsync(change.DocumentId);
        });
    }

    private async Task RefreshPreviewAsync(Guid documentId)
    {
        DocumentLibrary.DocumentLibraryEntry? entry;
        try
        {
            entry = await Context.DocumentLibraryProvider!.GetEntryAsync(
                DocumentLibrary.TempoDocumentKind.Diagram, documentId);
        }
        catch
        {
            return;
        }

        if (entry is null)
        {
            _notFound = true;
            StateHasChanged();
            return;
        }

        if (!string.IsNullOrEmpty(entry.PreviewSvg) && entry.PreviewSvg != _effectivePreview)
        {
            _effectivePreview = entry.PreviewSvg;
            StateHasChanged();
            if (!ReadOnly && Content is not null)
            {
                await OnContentSaved.InvokeAsync(new DiagramBlockContent
                {
                    DiagramDocumentId = Content.DiagramDocumentId,
                    SvgPreviewCache   = entry.PreviewSvg,
                    Width             = Content.Width,
                    Height            = Content.Height,
                    Caption           = Content.Caption
                });
            }
        }
    }

    private async Task HandleInsertSelectedAsync(DocumentOpenResult result)
    {
        _insertDialogOpen = false;
        if (result.Mode == DocumentOpenMode.Copy)
        {
            await InsertCopyAsync(result);
        }
        else
        {
            await InsertLinkAsync(result);
        }
    }

    private async Task InsertLinkAsync(DocumentOpenResult result)
    {
        string? preview = null;
        if (Context.DocumentLibraryProvider is not null)
        {
            var entry = await Context.DocumentLibraryProvider.GetEntryAsync(
                DocumentLibrary.TempoDocumentKind.Diagram, result.DocumentId);
            preview = entry?.PreviewSvg;
        }

        _refreshedId = result.DocumentId;
        _effectivePreview = preview;
        await OnContentSaved.InvokeAsync(new DiagramBlockContent
        {
            DiagramDocumentId = result.DocumentId,
            SvgPreviewCache   = preview
        });
    }

    private async Task InsertCopyAsync(DocumentOpenResult result)
    {
        if (Context.DiagramDocumentProvider is null)
        {
            await InsertLinkAsync(result);
            return;
        }

        var source = await Context.DiagramDocumentProvider.GetDiagramDocumentAsync(result.DocumentId);
        if (source is null)
        {
            return;
        }

        var copy = DiagramSerializer.Deserialize(DiagramSerializer.Serialize(source));
        var (newId, _) = await Context.DiagramDocumentProvider.CreateDiagramDocumentAsync(copy.Title);
        await Context.DiagramDocumentProvider.SaveDiagramDocumentAsync(newId, copy);

        string? preview = null;
        if (Context.DocumentLibraryProvider is not null)
        {
            var entry = await Context.DocumentLibraryProvider.GetEntryAsync(
                DocumentLibrary.TempoDocumentKind.Diagram, result.DocumentId);
            preview = entry?.PreviewSvg;
        }

        _refreshedId = newId;
        _effectivePreview = preview;
        await OnContentSaved.InvokeAsync(new DiagramBlockContent
        {
            DiagramDocumentId = newId,
            SvgPreviewCache   = preview
        });
    }

    private async Task HandleRemoveAsync() => await OnRemoveRequested.InvokeAsync();

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
        if (_creating) return;
        _creating = true;
        StateHasChanged();
        try
        {
            Guid id;
            if (Context.DiagramDocumentProvider is not null)
            {
                var (newId, _) = await Context.DiagramDocumentProvider.CreateDiagramDocumentAsync(string.Empty);
                id = newId;
            }
            else
            {
                id = Guid.NewGuid();
            }
            var created = new DiagramBlockContent { DiagramDocumentId = id };
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
        if (Content is null) return;
        if (Context.DiagramDocumentProvider is not null)
        {
            try
            {
                await Context.DiagramDocumentProvider.SaveDiagramDocumentAsync(
                    Content.DiagramDocumentId, result.Document);
            }
            catch { }
        }
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
        var notifier = Context?.DocumentChangeNotifier;
        if (notifier is not null)
        {
            if (_changeHandlerRegistered)
            {
                notifier.Changed -= OnRemoteChangedAsync;
            }
            if (_subscribedId != Guid.Empty)
            {
                try { await notifier.UnsubscribeAsync(DocumentLibrary.TempoDocumentKind.Diagram, _subscribedId); }
                catch { }
            }
        }

        if (_resizeInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyResizeHandle", _previewWrapRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
