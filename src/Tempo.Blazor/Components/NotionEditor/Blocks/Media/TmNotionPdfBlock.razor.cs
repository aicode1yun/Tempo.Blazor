using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Media;

public partial class TmNotionPdfBlock : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public IPdfBlockContent? Content  { get; set; }
    [Parameter] public bool              ReadOnly { get; set; }

    [Parameter] public EventCallback<(string? FileId, string? Url)> OnMediaSet       { get; set; }
    [Parameter] public EventCallback<string?> OnCaptionSaved    { get; set; }
    [Parameter] public EventCallback          OnDeleteRequested { get; set; }
    [Parameter] public EventCallback          OnFocused         { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference                       _canvasRef;
    private ElementReference                       _captionRef;
    private DotNetObjectReference<TmNotionPdfBlock>? _dotNetRef;
    private bool                                   _pdfInitialized;
    private bool                                   _captionInitialized;
    private bool                                   _captionDirty;
    private bool                                   _useFallback;
    private bool                                   _isLoading;
    private int                                    _currentPage  = 1;
    private int                                    _totalPages;
    private double                                 _scale        = 1.0;
    private int                                    _height       = 600;
    private string?                                _loadError;
    private IPdfBlockContent?                      _lastContent;
    private bool                                   _dialogOpen;

    private static readonly double[] _zoomSteps = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Content, _lastContent)) return;
        _lastContent        = Content;
        _pdfInitialized     = false;
        _captionInitialized = false;
        _captionDirty       = false;
        _currentPage        = 1;
        _totalPages         = 0;
        _scale              = 1.0;
        _useFallback        = false;
        _loadError          = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (string.IsNullOrEmpty(Content?.Url)) return;

        if (!_pdfInitialized)
        {
            _pdfInitialized = true;
            _isLoading      = true;
            StateHasChanged();
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                var available = await JS.InvokeAsync<bool>("tmNotionPdf.isAvailable");
                if (available)
                    await JS.InvokeVoidAsync("tmNotionPdf.init", _canvasRef, Content.Url, _dotNetRef);
                else
                    _useFallback = true;
            }
            catch
            {
                _useFallback = true;
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

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

    // ── JS invokable (called from tmNotionPdf.init) ───────────────────────────

    [JSInvokable]
    public void OnPdfLoaded(int totalPages)
    {
        _totalPages = totalPages;
        _isLoading  = false;
        _loadError  = null;
        StateHasChanged();
    }

    [JSInvokable]
    public void OnPdfLoadError(string message)
    {
        _useFallback = true;
        _isLoading   = false;
        _loadError   = Loc["TmNotionPdfBlock_LoadError"];
        StateHasChanged();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async Task GoToPreviousPageAsync()
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        await RenderCurrentPageAsync();
    }

    private async Task GoToNextPageAsync()
    {
        if (_currentPage >= _totalPages) return;
        _currentPage++;
        await RenderCurrentPageAsync();
    }

    private async Task RenderCurrentPageAsync()
    {
        try { await JS.InvokeVoidAsync("tmNotionPdf.renderPage", _canvasRef, _currentPage, _scale); }
        catch { }
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    private async Task ZoomInAsync()
    {
        var next = _zoomSteps.FirstOrDefault(z => z > _scale);
        if (next == 0) return;
        _scale = next;
        await ApplyZoomAsync();
    }

    private async Task ZoomOutAsync()
    {
        var prev = _zoomSteps.LastOrDefault(z => z < _scale);
        if (prev == 0) return;
        _scale = prev;
        await ApplyZoomAsync();
    }

    private async Task ApplyZoomAsync()
    {
        try { await JS.InvokeVoidAsync("tmNotionPdf.setScale", _canvasRef, _scale); }
        catch { }
    }

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

    public async ValueTask DisposeAsync()
    {
        if (_pdfInitialized && !_useFallback)
        {
            try { await JS.InvokeVoidAsync("tmNotionPdf.destroy", _canvasRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
