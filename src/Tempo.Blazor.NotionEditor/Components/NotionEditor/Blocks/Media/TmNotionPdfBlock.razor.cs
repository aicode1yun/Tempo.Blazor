using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Media;

public partial class TmNotionPdfBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascading ─────────────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext? Context { get; set; }

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IPdfBlockContent? Content  { get; set; }
    [Parameter] public bool              ReadOnly { get; set; }

    [Parameter] public EventCallback<(string? FileId, string? Url)> OnMediaSet       { get; set; }
    [Parameter] public EventCallback<string?>                         OnCaptionSaved    { get; set; }
    [Parameter] public EventCallback                                OnDeleteRequested { get; set; }
    [Parameter] public EventCallback                                OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                         _blockRef;
    private ElementReference                         _captionRef;
    private DotNetObjectReference<TmNotionPdfBlock>? _dotNetRef;
    private bool                                     _dropZoneInitialized;
    private bool                                     _captionInitialized;
    private bool                                     _captionDirty;
    private int                                      _currentPage = 1;
    private double                                   _scale        = 1.0;
    private int                                      _height       = 600;
    private IPdfBlockContent?                        _lastContent;
    private bool                                     _dialogOpen;
    private bool                                     _isDragging;
    private static readonly Type?                    PdfViewerComponentType = ResolvePdfViewerComponentType();

    private Dictionary<string, object?> PdfViewerParameters => new()
    {
        ["Url"] = Content?.Url,
        ["Page"] = _currentPage,
        ["PageChanged"] = EventCallback.Factory.Create<int>(this, value => _currentPage = value),
        ["Scale"] = _scale,
        ["ScaleChanged"] = EventCallback.Factory.Create<double>(this, value => _scale = value),
        ["ShowToolbar"] = true,
        ["AllowDownload"] = true,
        ["Height"] = "100%"
    };

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _captionInitialized = false;
        _captionDirty       = false;
        _currentPage        = 1;
        _scale              = 1.0;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ReadOnly)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);

            if (!_dropZoneInitialized && Context?.FileProvider != null)
            {
                _dropZoneInitialized = true;
                try { await JS.InvokeVoidAsync("tmNotionEditor.initBlockDropZone", _blockRef, _dotNetRef); }
                catch { }
            }
        }

        if (string.IsNullOrEmpty(Content?.Url)) return;

        if (!_captionInitialized)
        {
            _captionInitialized = true;
            if (!string.IsNullOrEmpty(Content?.Caption))
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.setHtml", _captionRef, Content.Caption); }
                catch { }
            }
        }
    }

    // ── JS callbacks ──────────────────────────────────────────────────────────

    [JSInvokable]
    public async Task OnFileDropped(string dataUrl, string mimeType, string fileName)
    {
        if (ReadOnly || Context?.FileProvider == null) return;

        var base64 = dataUrl[(dataUrl.IndexOf(',') + 1)..];
        var bytes  = Convert.FromBase64String(base64);
        using var stream = new MemoryStream(bytes);

        var media = await Context.FileProvider.UploadNotionFileAsync(stream, fileName, mimeType);
        await OnMediaSet.InvokeAsync((media.AssetId, media.Url));
        await InvokeAsync(StateHasChanged);
    }

    // ── Drag and drop ────────────────────────────────────────────────────────

    private void HandleDragEnter() { if (Context?.FileProvider == null) return; _isDragging = true; }
    private void HandleDragLeave() { _isDragging = false; }

    // ── Caption ───────────────────────────────────────────────────────────────

    private async Task OnCaptionBlurAsync()
    {
        if (!_captionDirty || ReadOnly) return;
        _captionDirty = false;
        try
        {
            var html = await JS.InvokeAsync<string>("tmNotionEditor.getHtml", _captionRef);
            await OnCaptionSaved.InvokeAsync(string.IsNullOrWhiteSpace(html) ? null : html);
        }
        catch { }
    }

    // ── Upload dialog ─────────────────────────────────────────────────────────

    private Task OpenDialogAsync()  { _dialogOpen = true;  return Task.CompletedTask; }
    private Task CloseDialogAsync() { _dialogOpen = false; return Task.CompletedTask; }

    private async Task HandleMediaSetAsync((string? FileId, string? Url) media)
    {
        _dialogOpen = false;
        await OnMediaSet.InvokeAsync(media);
    }

    // ── Focus ─────────────────────────────────────────────────────────────────

    private async Task HandleFocusAsync() => await OnFocused.InvokeAsync();

    // ── Dispose ───────────────────────────────────────────────────────────────

    private static Type? ResolvePdfViewerComponentType()
        => Type.GetType("Tempo.Blazor.Components.Files.TmPdfViewer, Tempo.Blazor.PdfViewer")
           ?? AppDomain.CurrentDomain.GetAssemblies()
               .Select(assembly => assembly.GetType("Tempo.Blazor.Components.Files.TmPdfViewer", throwOnError: false))
               .FirstOrDefault(type => type is not null);

    public async ValueTask DisposeAsync()
    {
        if (_dropZoneInitialized)
        {
            try { await JS.InvokeVoidAsync("tmNotionEditor.destroyBlockDropZone", _blockRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
