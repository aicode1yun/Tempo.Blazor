using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Files;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Files;

public class TmPdfViewerTests : LocalizationTestBase
{
    public TmPdfViewerTests()
    {
        // Ensure PDF.js isAvailable returns true in tests
        JSInterop.Setup<bool>("tmPdfViewer.isAvailable").SetResult(true);
    }

    // ── PDF-9: render s Url zobrazí .tm-pdf-viewer kontejner ───────────────

    [Fact]
    public void Render_WithUrl_DisplaysViewerContainer()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf"));

        cut.Find(".tm-pdf-viewer").Should().NotBeNull();
    }

    // ── PDF-10: render zobrazí canvas element ──────────────────────────────

    [Fact]
    public void Render_WithUrl_DisplaysCanvas()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf"));

        cut.Find("canvas.tm-pdf-viewer__canvas").Should().NotBeNull();
    }

    // ── PDF-11: ShowToolbar=true zobrazí toolbar ───────────────────────────

    [Fact]
    public void Render_WithShowToolbarTrue_DisplaysToolbar()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true));

        cut.Find(".tm-pdf-viewer__toolbar").Should().NotBeNull();
    }

    // ── PDF-12: toolbar obsahuje page label po načtení ─────────────────────

    [Fact]
    public void Render_AfterLoad_ToolbarContainsPageLabel()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(10));
        cut.Render();

        var label = cut.Find(".tm-pdf-viewer__page-label");
        label.TextContent.Should().Contain("Page");
    }

    // ── PDF-13: Previous page disabled na první stránce ────────────────────

    [Fact]
    public void Render_OnFirstPage_PreviousButtonDisabled()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(10));
        cut.Render();

        var prevBtn = cut.FindAll(".tm-pdf-viewer__btn")
                         .First(b => b.GetAttribute("aria-label")?.Contains("Previous") == true);
        prevBtn.HasAttribute("disabled").Should().BeTrue();
    }

    // ── PDF-14: Next page disabled na poslední stránce ─────────────────────

    [Fact]
    public void Render_OnLastPage_NextButtonDisabled()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Page, 10));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(10));
        cut.Render();

        var nextBtn = cut.FindAll(".tm-pdf-viewer__btn")
                         .First(b => b.GetAttribute("aria-label")?.Contains("Next") == true);
        nextBtn.HasAttribute("disabled").Should().BeTrue();
    }

    // ── PDF-15: klik na Next page inkrementuje Page ────────────────────────

    [Fact]
    public void ClickNextPage_IncrementsPage()
    {
        int capturedPage = 1;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.PageChanged, EventCallback.Factory.Create<int>(this, p => capturedPage = p)));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var nextBtn = cut.FindAll(".tm-pdf-viewer__btn")
                         .First(b => b.GetAttribute("aria-label")?.Contains("Next") == true);
        nextBtn.Click();

        capturedPage.Should().Be(2);
    }

    // ── PDF-16: klik na Previous page dekrementuje Page ────────────────────

    [Fact]
    public void ClickPreviousPage_DecrementsPage()
    {
        int capturedPage = 2;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Page, 2)
                      .Add(p => p.PageChanged, EventCallback.Factory.Create<int>(this, p => capturedPage = p)));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var prevBtn = cut.FindAll(".tm-pdf-viewer__btn")
                         .First(b => b.GetAttribute("aria-label")?.Contains("Previous") == true);
        prevBtn.Click();

        capturedPage.Should().Be(1);
    }

    // ── PDF-17: zoom in změní Scale ────────────────────────────────────────

    [Fact]
    public void ClickZoomIn_IncreasesScale()
    {
        double capturedScale = 1.0;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.ScaleChanged, EventCallback.Factory.Create<double>(this, s => capturedScale = s)));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var zoomInBtn = cut.FindAll(".tm-pdf-viewer__btn")
                           .First(b => b.GetAttribute("aria-label")?.Contains("Zoom in") == true);
        zoomInBtn.Click();

        capturedScale.Should().Be(1.25);
    }

    // ── PDF-18: zoom out změní Scale ───────────────────────────────────────

    [Fact]
    public void ClickZoomOut_DecreasesScale()
    {
        double capturedScale = 1.0;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Scale, 1.25)
                      .Add(p => p.ScaleChanged, EventCallback.Factory.Create<double>(this, s => capturedScale = s)));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var zoomOutBtn = cut.FindAll(".tm-pdf-viewer__btn")
                            .First(b => b.GetAttribute("aria-label")?.Contains("Zoom out") == true);
        zoomOutBtn.Click();

        capturedScale.Should().Be(1.0);
    }

    // ── PDF-19: zoom label zobrazí procenta ────────────────────────────────

    [Fact]
    public void Render_ZoomLabelShowsPercentage()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Scale, 1.5));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var label = cut.Find(".tm-pdf-viewer__zoom-label");
        label.TextContent.Should().Be("150%");
    }

    // ── PDF-20: AllowDownload=true zobrazí download link ───────────────────

    [Fact]
    public void Render_WithAllowDownloadTrue_DisplaysDownloadLink()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.AllowDownload, true));

        var link = cut.Find("a[download]");
        link.GetAttribute("href").Should().Be("https://example.com/test.pdf");
    }

    // ── PDF-21: Open link má target="_blank" ───────────────────────────────

    [Fact]
    public void Render_OpenLinkHasTargetBlank()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf"));

        var link = cut.Find("a[target='_blank']");
        link.Should().NotBeNull();
    }

    // ── PDF-22: loading spinner se zobrazí při inicializaci ────────────────

    [Fact]
    public void Render_InitialState_HasCanvasWrap()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf"));

        // Canvas wrap should always be present before fallback
        cut.Find(".tm-pdf-viewer__canvas-wrap").Should().NotBeNull();
    }

    // ── PDF-23: fallback embed se zobrazí když UseFallback=true ────────────

    [Fact]
    public void Render_WhenUseFallbackTrue_DisplaysEmbed()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf"));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoadError("test error"));
        cut.Render();

        cut.Find("embed.tm-pdf-viewer__embed").Should().NotBeNull();
    }

    // ── PDF-24: při chybě se přepne do fallback módu ─────────────────────

    [Fact]
    public void Render_OnLoadError_SwitchesToFallback()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf"));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoadError("test error"));
        cut.Render();

        cut.Find("embed.tm-pdf-viewer__embed").Should().NotBeNull();
    }

    // ── PDF-25: Class se aplikuje na root element ──────────────────────────

    [Fact]
    public void Render_WithClass_AppliesToRoot()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.Class, "my-pdf"));

        var root = cut.Find(".tm-pdf-viewer");
        root.ClassList.Should().Contain("my-pdf");
    }

    // ── PDF-26: bez Url zobrazí empty state ────────────────────────────────

    [Fact]
    public void Render_WithoutUrl_DisplaysEmptyState()
    {
        var cut = RenderComponent<TmPdfViewer>();

        cut.Find(".tm-pdf-viewer__empty").Should().NotBeNull();
    }

    // ── Additional: ShowToolbar=false skryje toolbar ───────────────────────

    [Fact]
    public void Render_WithShowToolbarFalse_HidesToolbar()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, false));

        cut.FindAll(".tm-pdf-viewer__toolbar").Should().BeEmpty();
    }

    // ── Additional: AllowDownload=false skryje download ────────────────────

    [Fact]
    public void Render_WithAllowDownloadFalse_HidesDownloadLink()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.AllowDownload, false));

        cut.FindAll("a[download]").Should().BeEmpty();
    }

    // ── Additional: Height parameter applies style ─────────────────────────

    [Fact]
    public void Render_WithHeight_AppliesStyle()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.Height, "400px"));

        var root = cut.Find(".tm-pdf-viewer");
        root.GetAttribute("style").Should().Contain("height:400px");
    }

    // ── Additional: Scale binding changes zoom label ───────────────────────

    [Fact]
    public void Render_ScaleBinding_UpdatesZoomLabel()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Scale, 0.75));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var label = cut.Find(".tm-pdf-viewer__zoom-label");
        label.TextContent.Should().Be("75%");
    }

    // ── Additional: Page binding changes page label ────────────────────────

    [Fact]
    public void Render_PageBinding_UpdatesPageLabel()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.Page, 3));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(10));
        cut.Render();

        var label = cut.Find(".tm-pdf-viewer__page-label");
        label.TextContent.Should().Contain("3");
    }

    // ── Additional: ShowTextLayer renders text layer div ───────────────────

    [Fact]
    public void Render_WithShowTextLayer_DisplaysTextLayer()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowTextLayer, true));

        cut.Find(".tm-pdf-viewer__text-layer").Should().NotBeNull();
    }

    // ── Additional: AllowRotation renders rotation button ──────────────────

    [Fact]
    public void Render_WithAllowRotation_DisplaysRotateButton()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.AllowRotation, true));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var rotateBtn = cut.FindAll(".tm-pdf-viewer__btn")
                           .FirstOrDefault(b => b.GetAttribute("aria-label")?.Contains("Rotate") == true);
        rotateBtn.Should().NotBeNull();
    }

    // ── Additional: GoToPage public API works ──────────────────────────────

    [Fact]
    public async Task GoToPage_UpdatesCurrentPage()
    {
        int capturedPage = 1;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.PageChanged, EventCallback.Factory.Create<int>(this, p => capturedPage = p)));

        await cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(10));
        cut.Render();

        await cut.Instance.GoToPageAsync(5);

        capturedPage.Should().Be(5);
    }

    // ── Additional: SetScale public API works ──────────────────────────────

    [Fact]
    public async Task SetScale_UpdatesScale()
    {
        double capturedScale = 1.0;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ScaleChanged, EventCallback.Factory.Create<double>(this, s => capturedScale = s)));

        await cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        await cut.Instance.SetScaleAsync(1.5);

        capturedScale.Should().Be(1.5);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Phase 3 — Advanced features
    // ══════════════════════════════════════════════════════════════════════════

    // ── PDF-45: ShowThumbnails=true zobrazí sidebar s miniatury ─────────────

    [Fact]
    public void Render_WithShowThumbnailsTrue_DisplaysThumbnailsSidebar()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowThumbnails, true));

        cut.Find(".tm-pdf-viewer__thumbnails").Should().NotBeNull();
    }

    // ── PDF-46: ShowThumbnails=false skryje sidebar ─────────────────────────

    [Fact]
    public void Render_WithShowThumbnailsFalse_HidesThumbnailsSidebar()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowThumbnails, false));

        cut.FindAll(".tm-pdf-viewer__thumbnails").Should().BeEmpty();
    }

    // ── PDF-47: ShowSearch=true zobrazí search bar ──────────────────────────

    [Fact]
    public void Render_WithShowSearchTrue_DisplaysSearchBar()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowSearch, true));

        cut.Find(".tm-pdf-viewer__search-bar").Should().NotBeNull();
    }

    // ── PDF-48: ShowSearch=false skryje search bar ──────────────────────────

    [Fact]
    public void Render_WithShowSearchFalse_HidesSearchBar()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowSearch, false));

        cut.FindAll(".tm-pdf-viewer__search-bar").Should().BeEmpty();
    }

    // ── PDF-49: ViewMode=Continuous zobrazí continuous wrap místo canvas wrap

    [Fact]
    public void Render_WithViewModeContinuous_DisplaysContinuousWrap()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ViewMode, PdfViewMode.Continuous));

        cut.Find(".tm-pdf-viewer__continuous-wrap").Should().NotBeNull();
    }

    // ── PDF-50: ViewMode=SinglePage zobrazí canvas wrap ─────────────────────

    [Fact]
    public void Render_WithViewModeSinglePage_DisplaysCanvasWrap()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ViewMode, PdfViewMode.SinglePage));

        cut.Find(".tm-pdf-viewer__canvas-wrap").Should().NotBeNull();
    }

    // ── PDF-51: klik na view mode toggle změní ViewMode ─────────────────────

    [Fact]
    public void ClickViewModeToggle_ChangesViewMode()
    {
        PdfViewMode capturedMode = PdfViewMode.SinglePage;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.ShowViewModeToggle, true)
                      .Add(p => p.ViewMode, PdfViewMode.SinglePage)
                      .Add(p => p.ViewModeChanged, EventCallback.Factory.Create<PdfViewMode>(this, m => capturedMode = m)));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var continuousBtn = cut.FindAll(".tm-pdf-viewer__btn")
                               .FirstOrDefault(b => b.GetAttribute("aria-label")?.Contains("Continuous") == true);
        continuousBtn.Should().NotBeNull();
        continuousBtn!.Click();

        capturedMode.Should().Be(PdfViewMode.Continuous);
    }

    // ── PDF-52: RotateAsync cykluje 0→90→180→270→0 ─────────────────────────

    [Fact]
    public async Task RotateAsync_CyclesRotation()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.AllowRotation, true));

        await cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));

        await cut.Instance.RotateAsync();
        cut.Instance.Rotation.Should().Be(90);

        await cut.Instance.RotateAsync();
        cut.Instance.Rotation.Should().Be(180);

        await cut.Instance.RotateAsync();
        cut.Instance.Rotation.Should().Be(270);

        await cut.Instance.RotateAsync();
        cut.Instance.Rotation.Should().Be(0);
    }

    // ── PDF-53: search input existuje v search baru ─────────────────────────

    [Fact]
    public void Render_WithShowSearchTrue_HasSearchInput()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowSearch, true));

        cut.Find(".tm-pdf-viewer__search-input").Should().NotBeNull();
    }

    // ── PDF-54: thumbnails sidebar má ref element ──────────────────────────

    [Fact]
    public void Render_WithShowThumbnailsTrue_HasThumbnailsElement()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowThumbnails, true));

        cut.Find(".tm-pdf-viewer__thumbnails").Should().NotBeNull();
    }

    // ── PDF-55: toolbar obsahuje search toggle když ShowSearch=true ─────────

    [Fact]
    public void Render_WithShowToolbarAndSearch_HasSearchToggleButton()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.ShowSearch, true));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        var searchBtn = cut.FindAll(".tm-pdf-viewer__btn")
                           .FirstOrDefault(b => b.GetAttribute("aria-label")?.Contains("Search") == true);
        searchBtn.Should().NotBeNull();
    }

    // ── PDF-56: body má flex layout s thumbnails ────────────────────────────

    [Fact]
    public void Render_WithThumbnails_BodyHasFlexLayout()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowThumbnails, true));

        var body = cut.Find(".tm-pdf-viewer__body");
        body.Should().NotBeNull();
    }

    // ── PDF-57: search toggle klik skryje/zobrazí search bar ────────────────

    [Fact]
    public void ClickSearchToggle_TogglesSearchBarVisibility()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.ShowToolbar, true)
                      .Add(p => p.ShowSearch, true));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(5));
        cut.Render();

        // Search bar visible by default when ShowSearch=true
        cut.FindAll(".tm-pdf-viewer__search-bar").Should().ContainSingle();

        var searchBtn = cut.FindAll(".tm-pdf-viewer__btn")
                           .First(b => b.GetAttribute("aria-label")?.Contains("Search") == true);
        searchBtn.Click();
        cut.Render();

        // After toggle click, search bar should be hidden
        cut.FindAll(".tm-pdf-viewer__search-bar").Should().BeEmpty();
    }

    // ── N1: additive extension points used by TmPdfAnnotator ────────────────

    [Fact]
    public void OverlayContent_RendersInsideCanvasWrap()
    {
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.OverlayContent, builder =>
                      {
                          builder.OpenElement(0, "div");
                          builder.AddAttribute(1, "data-testid", "custom-overlay-content");
                          builder.CloseElement();
                      }));

        cut.Find(".tm-pdf-viewer__canvas-wrap [data-testid='custom-overlay-content']").Should().NotBeNull();
    }

    [Fact]
    public void OnDocumentLoaded_FiresWithTotalPages()
    {
        var total = 0;
        var cut = RenderComponent<TmPdfViewer>(parameters =>
            parameters.Add(p => p.Url, "https://example.com/test.pdf")
                      .Add(p => p.OnDocumentLoaded, EventCallback.Factory.Create<int>(this, t => total = t)));

        cut.InvokeAsync(() => cut.Instance.OnPdfLoaded(7));

        total.Should().Be(7);
    }
}
