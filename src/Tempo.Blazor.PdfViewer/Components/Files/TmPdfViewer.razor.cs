using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.Files;

/// <summary>View mode for the PDF viewer.</summary>
public enum PdfViewMode
{
    /// <summary>Single page mode with navigation.</summary>
    SinglePage,
    /// <summary>Continuous scroll through all pages.</summary>
    Continuous
}

/// <summary>
/// A reusable PDF viewer component powered by PDF.js v5.
/// Supports page navigation, zoom, rotation, text layer, thumbnails, search, and continuous scroll.
/// </summary>
public partial class TmPdfViewer : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>URL of the PDF document to display.</summary>
    [Parameter] public string? Url { get; set; }

    /// <summary>Current page number (1-based). Supports two-way binding.</summary>
    [Parameter] public int Page { get; set; } = 1;

    /// <summary>Callback when the page changes.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>Current zoom scale. Supports two-way binding.</summary>
    [Parameter] public double Scale { get; set; } = 1.0;

    /// <summary>Callback when the scale changes.</summary>
    [Parameter] public EventCallback<double> ScaleChanged { get; set; }

    /// <summary>Whether to show the toolbar. Default is true.</summary>
    [Parameter] public bool ShowToolbar { get; set; } = true;

    /// <summary>Whether to allow downloading the PDF. Default is true.</summary>
    [Parameter] public bool AllowDownload { get; set; } = true;

    /// <summary>Whether to allow rotation. Default is false.</summary>
    [Parameter] public bool AllowRotation { get; set; }

    /// <summary>Whether to render a selectable text layer over the canvas. Default is false.</summary>
    [Parameter] public bool ShowTextLayer { get; set; }

    /// <summary>Whether to show a thumbnails sidebar. Default is false.</summary>
    [Parameter] public bool ShowThumbnails { get; set; }

    /// <summary>Whether to show a search input. Default is false.</summary>
    [Parameter] public bool ShowSearch { get; set; }

    /// <summary>Whether to show a view mode toggle (single/continuous). Default is false.</summary>
    [Parameter] public bool ShowViewModeToggle { get; set; }

    /// <summary>Current view mode. Default is SinglePage.</summary>
    [Parameter] public PdfViewMode ViewMode { get; set; } = PdfViewMode.SinglePage;

    /// <summary>Callback when the view mode changes.</summary>
    [Parameter] public EventCallback<PdfViewMode> ViewModeChanged { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Height of the viewer (CSS value). Default is "600px".</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private ElementReference _canvasRef;
    private ElementReference _textLayerRef;
    private ElementReference _thumbnailsRef;
    private ElementReference _continuousRef;
    private DotNetObjectReference<TmPdfViewer>? _dotNetRef;
    private bool _pdfInitialized;
    private bool _useFallback;
    private bool _isLoading;
    private int _currentPage = 1;
    private int _totalPages;
    private double _scale = 1.0;
    /// <summary>Current rotation in degrees (0, 90, 180, 270).</summary>
    public int Rotation { get; private set; }
    private PdfViewMode _viewMode = PdfViewMode.SinglePage;
    private string? _loadError;
    private string? _lastUrl;

    // Search state
    private bool _searchVisible;
    private string? _searchQuery;
    private int _searchTotalMatches;
    private int _searchCurrentMatch;

    private static readonly double[] _zoomSteps = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Url != _lastUrl)
        {
            _lastUrl = Url;
            _pdfInitialized = false;
            _currentPage = Math.Max(1, Page);
            _scale = Scale is > 0 and <= 5 ? Scale : 1.0;
            Rotation = 0;
            _viewMode = ViewMode;
            _useFallback = false;
            _loadError = null;
            _totalPages = 0;
            _searchVisible = ShowSearch;
            _searchQuery = null;
            _searchTotalMatches = 0;
            _searchCurrentMatch = 0;
        }
        else
        {
            if (Page != _currentPage && Page >= 1)
            {
                _currentPage = Page;
                if (_pdfInitialized && !_useFallback)
                {
                    _ = RenderCurrentPageAsync();
                }
            }
            if (Math.Abs(Scale - _scale) > 0.001 && Scale is > 0 and <= 5)
            {
                _scale = Scale;
                if (_pdfInitialized && !_useFallback)
                {
                    _ = ApplyZoomAsync();
                }
            }
            if (ViewMode != _viewMode)
            {
                _viewMode = ViewMode;
                if (_pdfInitialized && !_useFallback)
                {
                    _ = SwitchViewModeAsync();
                }
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (string.IsNullOrEmpty(Url)) return;

        if (!_pdfInitialized)
        {
            _pdfInitialized = true;
            _isLoading = true;
            StateHasChanged();
            _dotNetRef?.Dispose();
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                var available = await JS.InvokeAsync<bool>("tmPdfViewer.isAvailable");
                if (available)
                {
                    await JS.InvokeVoidAsync("tmPdfViewer.init", _canvasRef, Url, _dotNetRef);
                }
                else
                {
                    _useFallback = true;
                }
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

        if (_totalPages > 0 && ShowThumbnails && _thumbnailsRef.Context is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("tmPdfViewer.renderThumbnails", _thumbnailsRef, Url, 0.3, _dotNetRef);
                await JS.InvokeVoidAsync("tmPdfViewer.highlightThumbnail", _thumbnailsRef, _currentPage);
            }
            catch { }
        }

        if (_totalPages > 0 && _viewMode == PdfViewMode.Continuous && _continuousRef.Context is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("tmPdfViewer.renderAllPages", _continuousRef, _canvasRef, _scale, Rotation);
            }
            catch { }
        }
    }

    // ── JS invokable ─────────────────────────────────────────────────────────

    /// <summary>Receives the total page count after the JavaScript PDF loader finishes.</summary>
    [JSInvokable]
    public void OnPdfLoaded(int totalPages)
    {
        _totalPages = totalPages;
        _isLoading = false;
        _loadError = null;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Receives a PDF load failure notification from JavaScript and switches to fallback rendering.</summary>
    [JSInvokable]
    public void OnPdfLoadError(string message)
    {
        _useFallback = true;
        _isLoading = false;
        _loadError = Loc["TmPdfViewer_LoadError"];
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Handles thumbnail page selection from the JavaScript thumbnail renderer.</summary>
    [JSInvokable]
    public async Task OnThumbnailClicked(int pageNum)
    {
        if (pageNum < 1 || pageNum > _totalPages) return;
        _currentPage = pageNum;
        await PageChanged.InvokeAsync(_currentPage);
        await RenderCurrentPageAsync();
        try { await JS.InvokeVoidAsync("tmPdfViewer.highlightThumbnail", _thumbnailsRef, _currentPage); }
        catch { }
    }

    /// <summary>Receives search result counts from the JavaScript search implementation.</summary>
    [JSInvokable]
    public void OnSearchResults(int totalMatches, int[] matchesPerPage)
    {
        _searchTotalMatches = totalMatches;
        _searchCurrentMatch = totalMatches > 0 ? 1 : 0;
        InvokeAsync(StateHasChanged);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async Task GoToPreviousPageAsync()
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        await PageChanged.InvokeAsync(_currentPage);
        await RenderCurrentPageAsync();
    }

    private async Task GoToNextPageAsync()
    {
        if (_currentPage >= _totalPages) return;
        _currentPage++;
        await PageChanged.InvokeAsync(_currentPage);
        await RenderCurrentPageAsync();
    }

    private async Task RenderCurrentPageAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("tmPdfViewer.renderPage", _canvasRef, _currentPage, _scale, Rotation);
            if (ShowTextLayer)
            {
                await JS.InvokeVoidAsync("tmPdfViewer.renderTextLayer", _canvasRef, _textLayerRef, _currentPage, _scale, Rotation);
            }
        }
        catch { }
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    private async Task ZoomInAsync()
    {
        var next = _zoomSteps.FirstOrDefault(z => z > _scale);
        if (next == 0) return;
        _scale = next;
        await ScaleChanged.InvokeAsync(_scale);
        await ApplyZoomAsync();
    }

    private async Task ZoomOutAsync()
    {
        var prev = _zoomSteps.LastOrDefault(z => z < _scale);
        if (prev == 0) return;
        _scale = prev;
        await ScaleChanged.InvokeAsync(_scale);
        await ApplyZoomAsync();
    }

    private async Task ApplyZoomAsync()
    {
        try
        {
            if (_viewMode == PdfViewMode.Continuous && _continuousRef.Context is not null)
            {
                await JS.InvokeVoidAsync("tmPdfViewer.renderAllPages", _continuousRef, _canvasRef, _scale, Rotation);
            }
            else
            {
                await JS.InvokeVoidAsync("tmPdfViewer.setScale", _canvasRef, _scale);
                if (ShowTextLayer)
                {
                    await JS.InvokeVoidAsync("tmPdfViewer.renderTextLayer", _canvasRef, _textLayerRef, _currentPage, _scale, Rotation);
                }
            }
        }
        catch { }
    }

    // ── Rotation ──────────────────────────────────────────────────────────────

    /// <summary>Rotate the document 90 degrees clockwise.</summary>
    public async Task RotateAsync()
    {
        Rotation = (Rotation + 90) % 360;
        if (_viewMode == PdfViewMode.Continuous && _continuousRef.Context is not null)
        {
            try { await JS.InvokeVoidAsync("tmPdfViewer.renderAllPages", _continuousRef, _canvasRef, _scale, Rotation); }
            catch { }
        }
        else
        {
            await RenderCurrentPageAsync();
        }
    }

    // ── View Mode ─────────────────────────────────────────────────────────────

    private async Task SetViewModeAsync(PdfViewMode mode)
    {
        _viewMode = mode;
        await ViewModeChanged.InvokeAsync(mode);
        await SwitchViewModeAsync();
    }

    private async Task SwitchViewModeAsync()
    {
        if (_viewMode == PdfViewMode.Continuous)
        {
            try { await JS.InvokeVoidAsync("tmPdfViewer.renderAllPages", _continuousRef, _canvasRef, _scale, Rotation); }
            catch { }
        }
        else
        {
            await RenderCurrentPageAsync();
        }
    }

    private async Task ToggleSearchAsync()
    {
        _searchVisible = !_searchVisible;
        await InvokeAsync(StateHasChanged);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private async Task HandleSearchKeyUpAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrEmpty(_searchQuery))
        {
            await PerformSearchAsync();
        }
    }

    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrEmpty(_searchQuery)) return;
        try
        {
            await JS.InvokeVoidAsync("tmPdfViewer.search", _canvasRef, _searchQuery, _dotNetRef);
        }
        catch { }
    }

    private async Task ClearSearchAsync()
    {
        _searchQuery = null;
        _searchTotalMatches = 0;
        _searchCurrentMatch = 0;
        await PerformSearchAsync();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Navigates to a specific page.</summary>
    public async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > _totalPages) return;
        _currentPage = page;
        await PageChanged.InvokeAsync(_currentPage);
        await RenderCurrentPageAsync();
    }

    /// <summary>Sets the zoom scale directly.</summary>
    public async Task SetScaleAsync(double scale)
    {
        if (scale is <= 0 or > 5) return;
        _scale = scale;
        await ScaleChanged.InvokeAsync(_scale);
        await ApplyZoomAsync();
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_pdfInitialized && !_useFallback)
        {
            try { await JS.InvokeVoidAsync("tmPdfViewer.destroy", _canvasRef); }
            catch { }
        }
        _dotNetRef?.Dispose();
    }
}
