using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Displays a normalized signing document page and optional overlays.</summary>
public partial class TmDocumentPageViewer
{
    private static readonly double[] ZoomSteps = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];
    private double _scale = 1.0;
    private DocumentPageZoomMode _zoomMode = DocumentPageZoomMode.Custom;

    /// <summary>Optional id assigned to the rendered page element.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Document page to render. When null, an empty state is shown.</summary>
    [Parameter] public SigningDocumentPage? Page { get; set; }

    /// <summary>Whether to show the loading skeleton instead of the page.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Error message shown instead of the page.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Optional alt text for the page image. Defaults to page label or localized page number.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Whether overlay content should receive pointer events. Defaults to true.</summary>
    [Parameter] public bool IsOverlayInteractive { get; set; } = true;

    /// <summary>Whether to prevent the browser context menu on the page element. Defaults to true.</summary>
    [Parameter] public bool PreventDefaultContextMenu { get; set; } = true;

    /// <summary>Current visual scale. Values are clamped between <see cref="MinScale"/> and <see cref="MaxScale"/>.</summary>
    [Parameter] public double Scale { get; set; } = 1.0;

    /// <summary>Callback invoked when the visual scale changes.</summary>
    [Parameter] public EventCallback<double> ScaleChanged { get; set; }

    /// <summary>Smallest allowed visual scale. Defaults to 0.5.</summary>
    [Parameter] public double MinScale { get; set; } = 0.5;

    /// <summary>Largest allowed visual scale. Defaults to 2.0.</summary>
    [Parameter] public double MaxScale { get; set; } = 2.0;

    /// <summary>Current zoom behavior. Defaults to <see cref="DocumentPageZoomMode.Custom"/>.</summary>
    [Parameter] public DocumentPageZoomMode ZoomMode { get; set; } = DocumentPageZoomMode.Custom;

    /// <summary>Callback invoked when the zoom behavior changes.</summary>
    [Parameter] public EventCallback<DocumentPageZoomMode> ZoomModeChanged { get; set; }

    /// <summary>Current page view mode for parent tools. Defaults to <see cref="DocumentPageViewMode.SinglePage"/>.</summary>
    [Parameter] public DocumentPageViewMode ViewMode { get; set; } = DocumentPageViewMode.SinglePage;

    /// <summary>Callback invoked when the page view mode changes.</summary>
    [Parameter] public EventCallback<DocumentPageViewMode> ViewModeChanged { get; set; }

    /// <summary>Whether to render the viewer toolbar. Defaults to false for layout compatibility.</summary>
    [Parameter] public bool ShowToolbar { get; set; }

    /// <summary>Whether zoom controls are visible when the toolbar is rendered. Defaults to true.</summary>
    [Parameter] public bool ShowZoomControls { get; set; } = true;

    /// <summary>Whether page navigation controls are visible when the toolbar is rendered. Defaults to false.</summary>
    [Parameter] public bool ShowPaginationControls { get; set; }

    /// <summary>Displayed current page number for pagination controls. Defaults to the page index plus one.</summary>
    [Parameter] public int? PageNumber { get; set; }

    /// <summary>Total displayed page count for pagination controls.</summary>
    [Parameter] public int? TotalPages { get; set; }

    /// <summary>Callback invoked when the previous page button is clicked.</summary>
    [Parameter] public EventCallback PreviousPageRequested { get; set; }

    /// <summary>Callback invoked when the next page button is clicked.</summary>
    [Parameter] public EventCallback NextPageRequested { get; set; }

    /// <summary>Optional template for replacing the rendered page image.</summary>
    [Parameter] public RenderFragment<SigningDocumentPage>? PageTemplate { get; set; }

    /// <summary>Overlay content rendered over the document page.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Overlay template rendered over the document page with page context.</summary>
    [Parameter] public RenderFragment<SigningDocumentPage>? OverlayTemplate { get; set; }

    /// <summary>Callback invoked when the page is clicked.</summary>
    [Parameter] public EventCallback<TmDocumentPageViewerPointerEventArgs> OnPageClick { get; set; }

    /// <summary>Callback invoked when the page context menu is requested.</summary>
    [Parameter] public EventCallback<TmDocumentPageViewerPointerEventArgs> OnPageContextMenu { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnParametersSet()
    {
        _scale = ClampScale(Scale);
        _zoomMode = ZoomMode;
    }

    private string RootClass => string.Join(" ", new[]
    {
        "tm-document-page-viewer",
        ShowToolbar ? "tm-document-page-viewer--with-toolbar" : null,
        $"tm-document-page-viewer--zoom-{_zoomMode.ToString().ToLowerInvariant()}",
        Class
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private string OverlayClass => IsOverlayInteractive
        ? "tm-document-page-viewer__overlay"
        : "tm-document-page-viewer__overlay tm-document-page-viewer__overlay--readonly";

    private string PageElementId => !string.IsNullOrWhiteSpace(Id)
        ? Id
        : $"tm-document-page-{Page?.PageIndex ?? 0}";

    private string PageStyle
    {
        get
        {
            if (Page is null || Page.Width <= 0 || Page.Height <= 0)
            {
                return string.Empty;
            }

            return string.Create(CultureInfo.InvariantCulture, $"aspect-ratio: {Page.Width} / {Page.Height}; --tm-document-page-scale: {_scale};");
        }
    }

    private string ZoomLabel => string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(_scale * 100)}%");

    private int CurrentPageNumber => PageNumber ?? (Page?.PageIndex ?? 0) + 1;

    private int CurrentTotalPages => Math.Max(1, TotalPages ?? CurrentPageNumber);

    private bool CanGoPrevious => CurrentPageNumber > 1 && PreviousPageRequested.HasDelegate;

    private bool CanGoNext => CurrentPageNumber < CurrentTotalPages && NextPageRequested.HasDelegate;

    private bool CanZoomOut => _scale > MinScale + 0.001;

    private bool CanZoomIn => _scale < MaxScale - 0.001;

    private string ImageAlt => !string.IsNullOrWhiteSpace(Alt)
        ? Alt
        : Page?.Label ?? Loc["TmDocumentPageViewer_PageAlt", (Page?.PageIndex ?? 0) + 1];

    private string PageAriaLabel => Page?.Label ?? Loc["TmDocumentPageViewer_PageAriaLabel", (Page?.PageIndex ?? 0) + 1];

    private Task HandlePageClickAsync(MouseEventArgs args)
    {
        return Page is null || !OnPageClick.HasDelegate
            ? Task.CompletedTask
            : OnPageClick.InvokeAsync(new TmDocumentPageViewerPointerEventArgs(Page, args));
    }

    private Task HandlePageContextMenuAsync(MouseEventArgs args)
    {
        return Page is null || !OnPageContextMenu.HasDelegate
            ? Task.CompletedTask
            : OnPageContextMenu.InvokeAsync(new TmDocumentPageViewerPointerEventArgs(Page, args));
    }

    private Task GoToPreviousPageAsync()
    {
        return CanGoPrevious ? PreviousPageRequested.InvokeAsync() : Task.CompletedTask;
    }

    private Task GoToNextPageAsync()
    {
        return CanGoNext ? NextPageRequested.InvokeAsync() : Task.CompletedTask;
    }

    private Task ZoomOutAsync()
    {
        var next = ZoomSteps.LastOrDefault(value => value < _scale - 0.001);
        return SetScaleAsync(next <= 0 ? MinScale : next, DocumentPageZoomMode.Custom);
    }

    private Task ZoomInAsync()
    {
        var next = ZoomSteps.FirstOrDefault(value => value > _scale + 0.001);
        return SetScaleAsync(next <= 0 ? MaxScale : next, DocumentPageZoomMode.Custom);
    }

    private Task FitWidthAsync()
    {
        return SetScaleAsync(1.0, DocumentPageZoomMode.FitWidth);
    }

    private Task FitPageAsync()
    {
        return SetScaleAsync(0.85, DocumentPageZoomMode.FitPage);
    }

    private async Task SetScaleAsync(double scale, DocumentPageZoomMode zoomMode)
    {
        _scale = ClampScale(scale);
        _zoomMode = zoomMode;
        await ScaleChanged.InvokeAsync(_scale);
        await ZoomModeChanged.InvokeAsync(_zoomMode);
    }

    private double ClampScale(double scale)
    {
        var min = Math.Max(0.1, MinScale);
        var max = Math.Max(min, MaxScale);
        return double.IsFinite(scale)
            ? Math.Min(Math.Max(scale, min), max)
            : 1.0;
    }
}
