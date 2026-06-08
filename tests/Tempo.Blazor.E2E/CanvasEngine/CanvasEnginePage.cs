using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E.CanvasEngine;

/// <summary>Page object for the clean-room canvas document editor harness.</summary>
public sealed class CanvasEnginePage
{
    private readonly TestContext _testContext;

    /// <summary>Creates a page object for a mounted canvas document engine.</summary>
    public CanvasEnginePage(IPage page, string outputDirectory, TestContext testContext, string seedId, CanvasEngineViewport viewport)
    {
        Page = page;
        OutputDirectory = outputDirectory;
        _testContext = testContext;
        SeedId = seedId;
        Viewport = viewport;
    }

    /// <summary>Browser page.</summary>
    public IPage Page { get; }

    /// <summary>Directory where screenshots and manifests are written.</summary>
    public string OutputDirectory { get; }

    /// <summary>Seed document identifier requested by the test.</summary>
    public string SeedId { get; }

    /// <summary>Viewport under test.</summary>
    public CanvasEngineViewport Viewport { get; }

    /// <summary>Canvas engine host element.</summary>
    public ILocator Host => Page.GetByTestId("document-canvas-engine-host");

    /// <summary>Canvas engine root element.</summary>
    public ILocator Editor => Page.GetByTestId("document-canvas-engine-root");

    /// <summary>Visible page surface.</summary>
    public ILocator PageSurface => Page.GetByTestId("document-canvas-page").First;

    /// <summary>Page background canvas layer.</summary>
    public ILocator Canvas => Page.Locator("[data-canvas-layer='page-background']").First;

    /// <summary>Selection and caret canvas layer.</summary>
    public ILocator OverlayCanvas => Page.Locator("[data-canvas-layer='selection-caret']").First;

    /// <summary>Toolbar root, once the Blazor host introduces one.</summary>
    public ILocator Toolbar => Page.GetByTestId("document-canvas-toolbar");

    /// <summary>Accessibility mirror root.</summary>
    public ILocator A11yMirror => Page.GetByTestId("document-canvas-a11y-mirror");

    /// <summary>Hidden keyboard and IME bridge.</summary>
    public ILocator HiddenInput => Page.GetByTestId("document-canvas-hidden-input");

    /// <summary>Waits until the harness and engine both report readiness.</summary>
    public async Task WaitUntilReadyAsync()
    {
        await Host.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 30_000 });
        await Editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await Page.WaitForFunctionAsync(
            "() => window.__canvasDocumentEngineHarness && window.__canvasDocumentEngineHarness.ready === true",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
    }

    /// <summary>Focuses the hidden input bridge.</summary>
    public Task FocusHiddenInputAsync()
        => HiddenInput.FocusAsync();

    /// <summary>Captures a full-page screenshot.</summary>
    public async Task<string> CaptureFullAsync(string fileName)
    {
        var path = BuildPath(fileName);
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true,
            Type = ScreenshotType.Png
        });
        _testContext.AddResultFile(path);
        return path;
    }

    /// <summary>Captures the editor root screenshot.</summary>
    public async Task<string> CaptureEditorAsync(string fileName)
    {
        var path = BuildPath(fileName);
        await Editor.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = path,
            Type = ScreenshotType.Png
        });
        _testContext.AddResultFile(path);
        return path;
    }

    /// <summary>Captures a deterministic page screenshot clip.</summary>
    public async Task<string> CaptureCanvasCropAsync(string fileName, CanvasScreenshotClip rect)
    {
        var path = BuildPath(fileName);
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            Type = ScreenshotType.Png,
            Clip = new() { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height }
        });
        _testContext.AddResultFile(path);
        return path;
    }

    /// <summary>Captures an individual control or surface by selector.</summary>
    public async Task<string> CaptureControlAsync(string fileName, string selector)
    {
        var path = BuildPath(fileName);
        await Page.Locator(selector).First.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = path,
            Type = ScreenshotType.Png
        });
        _testContext.AddResultFile(path);
        return path;
    }

    /// <summary>Returns the current page-surface clipping rectangle.</summary>
    public async Task<CanvasScreenshotClip> GetPageSurfaceClipAsync()
    {
        var box = await PageSurface.BoundingBoxAsync();
        Assert.IsNotNull(box, "Canvas page surface must expose a clipping rectangle.");
        return new CanvasScreenshotClip
        {
            X = (float)Math.Max(0, box.X),
            Y = (float)Math.Max(0, box.Y),
            Width = (float)Math.Max(1, box.Width),
            Height = (float)Math.Max(1, box.Height)
        };
    }

    /// <summary>Creates a manifest initialized with this page's viewport and seed.</summary>
    public CanvasVisualReviewManifest CreateManifest(string testClass, string testName)
        => new()
        {
            TestClass = testClass,
            TestName = testName,
            Viewport = Viewport.Name,
            ViewportWidth = Viewport.Width,
            ViewportHeight = Viewport.Height,
            SeedId = SeedId
        };

    /// <summary>Writes and attaches the manifest.</summary>
    public async Task<string> WriteManifestAsync(CanvasVisualReviewManifest manifest)
    {
        var path = BuildPath("manifest.json");
        await manifest.WriteAsync(path);
        _testContext.AddResultFile(path);
        return path;
    }

    private string BuildPath(string fileName)
    {
        Directory.CreateDirectory(OutputDirectory);
        return Path.Combine(OutputDirectory, fileName);
    }
}

/// <summary>Viewport-space screenshot clip rectangle.</summary>
public sealed class CanvasScreenshotClip
{
    /// <summary>Left coordinate in CSS pixels.</summary>
    public float X { get; set; }

    /// <summary>Top coordinate in CSS pixels.</summary>
    public float Y { get; set; }

    /// <summary>Clip width in CSS pixels.</summary>
    public float Width { get; set; }

    /// <summary>Clip height in CSS pixels.</summary>
    public float Height { get; set; }
}
