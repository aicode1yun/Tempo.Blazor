using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Phase 0 baseline evidence for the canvas-based document editor direction.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[DoNotParallelize]
public sealed class DocumentEditorCanvasEngineBaselineE2ETests : DocumentEditorE2ETestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [TestMethod]
    public async Task Baseline_CurrentCoreEngineBeforeRedesign_CapturesDesktopScreenshots()
    {
        var page = await OpenBaselineDocumentEditorAsync("/document-editor?documentId=onlyoffice-parity-2026-05-24", 1440, 1000);
        var output = CreateOutputDirectory("desktop-1440x1000");
        var fullPath = Path.Combine(output, "00-current-core-full.png");
        var editorPath = Path.Combine(output, "01-current-core-editor.png");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = fullPath,
            FullPage = true,
            Type = ScreenshotType.Png
        });
        await page.GetByTestId("document-editor-demo").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = editorPath,
            Type = ScreenshotType.Png
        });

        var probe = await CaptureProbeAsync(page);
        Assert.AreEqual("CoreEnginePreview", probe.RenderEngine, "The before-redesign baseline must capture the current core engine.");
        Assert.IsTrue(probe.HasCoreHost, "The baseline route must render the current core engine host.");
        Assert.IsFalse(probe.HasCanvasHost, "Phase 0 must not pretend the canvas engine host already exists.");
        Assert.IsTrue(new FileInfo(fullPath).Length > 10_000, "The full-page baseline screenshot must be a real non-empty PNG.");
        Assert.IsTrue(new FileInfo(editorPath).Length > 5_000, "The editor baseline screenshot must be a real non-empty PNG.");

        var manifestPath = Path.Combine(output, "manifest.json");
        await WriteManifestAsync(manifestPath, new
        {
            testName = nameof(Baseline_CurrentCoreEngineBeforeRedesign_CapturesDesktopScreenshots),
            viewport = "desktop-1440x1000",
            seedDocumentId = "onlyoffice-parity-2026-05-24",
            userActions = new[]
            {
                "Open /document-editor with the deterministic ONLYOFFICE parity document.",
                "Wait for the current core engine host to be present.",
                "Capture the full page and editor surface as before-redesign evidence."
            },
            expectedVisibleChanges = "No document mutation; this is the visual baseline for the current core engine.",
            expectedModelChanges = "None.",
            screenshotPaths = new[] { fullPath, editorPath },
            canvasNonBlankMetrics = "Not applicable in phase 0 because the canvas host is intentionally absent.",
            overlapChecks = "Deferred to the canvas screenshot gate in phase 2; this baseline records current visual debt.",
            uxUiReviewerNotes = "Manual review of the saved screenshot: current core engine shows visible text overlap around wrapped images and table/object areas, compressed document density beside the side panel, and no canvas-owned page/overlay separation. This is an honest before-redesign baseline.",
            probe
        });
        TestContext.AddResultFile(fullPath);
        TestContext.AddResultFile(editorPath);
        TestContext.AddResultFile(manifestPath);
    }

    [TestMethod]
    public async Task CanvasEngineRouteFlag_CurrentlyMissing_BaselineIsExplicit()
    {
        var page = await OpenBaselineDocumentEditorAsync("/document-editor?tmDocumentEditorEngine=canvas", 1280, 800);
        var probe = await CaptureProbeAsync(page);

        Assert.IsFalse(probe.HasCanvasHost, "Phase 0 records the honest red state: the canvas engine host is not routable yet.");
        Assert.AreNotEqual("CanvasEnginePreview", probe.RenderEngine, "The demo route must not report a canvas engine before it exists.");
        Assert.AreNotEqual("CanvasEnginePreview", probe.RequestedRenderEngine, "The public component flag is intentionally not wired to canvas in phase 0.");

        var output = CreateOutputDirectory("canvas-flag-red");
        var manifestPath = Path.Combine(output, "manifest.json");
        await WriteManifestAsync(manifestPath, new
        {
            testName = nameof(CanvasEngineRouteFlag_CurrentlyMissing_BaselineIsExplicit),
            viewport = "notebook-1280x800",
            seedDocumentId = "contract-demo",
            userActions = new[]
            {
                "Open /document-editor?tmDocumentEditorEngine=canvas.",
                "Inspect the active document editor host and root render-engine attributes."
            },
            expectedVisibleChanges = "No canvas host appears yet; phase 3 will change this.",
            expectedModelChanges = "None.",
            screenshotPaths = Array.Empty<string>(),
            canvasNonBlankMetrics = "Red baseline: no canvas element is owned by a canvas document engine yet.",
            overlapChecks = "Not applicable until the canvas host exists.",
            uxUiReviewerNotes = "This is the explicit phase 0 red gate, not a product implementation.",
            probe
        });
        TestContext.AddResultFile(manifestPath);
    }

    [Ignore("Phase 0 RED gate: enable when implementing the CanvasEnginePreview route/flag in phase 3.")]
    [TestMethod]
    public async Task CanvasEngineRouteFlag_RedGate_RendersCanvasHostWhenEnabled()
    {
        var page = await OpenBaselineDocumentEditorAsync("/document-editor?tmDocumentEditorEngine=canvas", 1280, 800);

        await page.WaitForSelectorAsync("[data-testid='document-canvas-engine-host']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10_000
        });
    }

    private async Task<IPage> OpenBaselineDocumentEditorAsync(string pathAndQuery, int width, int height)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}{pathAndQuery}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        });
        await page.WaitForSelectorAsync("[data-testid='document-editor-demo']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 60_000
        });
        await page.WaitForFunctionAsync(
            """
            () => {
                const editor = document.querySelector('[data-testid="document-editor-demo"]');
                if (!editor) return false;
                const core = document.querySelector('[data-testid="document-core-engine-host"]');
                const wysiwyg = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const canvas = document.querySelector('[data-testid="document-canvas-engine-host"]');
                return !!(canvas || wysiwyg || core);
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 60_000 });
        await page.WaitForTimeoutAsync(500);
        return page;
    }

    private static Task<CanvasBaselineProbe> CaptureProbeAsync(IPage page)
        => page.EvaluateAsync<CanvasBaselineProbe>(
            """
            () => {
                const editor = document.querySelector('[data-testid="document-editor-demo"]');
                const root = editor?.closest('[data-render-engine]') || editor;
                const core = document.querySelector('[data-testid="document-core-engine-host"]');
                const wysiwyg = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const canvas = document.querySelector('[data-testid="document-canvas-engine-host"]');
                const activeHost = canvas || core || wysiwyg;
                const text = (activeHost?.innerText || activeHost?.textContent || '').replace(/\s+/g, ' ').trim();
                const rect = activeHost?.getBoundingClientRect();
                return {
                    url: location.href,
                    renderEngine: root?.getAttribute('data-render-engine') || '',
                    requestedRenderEngine: root?.getAttribute('data-render-engine-requested') || '',
                    hasCoreHost: !!core,
                    hasWysiwygHost: !!wysiwyg,
                    hasCanvasHost: !!canvas,
                    canvasElementCount: document.querySelectorAll('canvas').length,
                    activeHostWidth: rect?.width || 0,
                    activeHostHeight: rect?.height || 0,
                    visibleTextSample: text.slice(0, 500)
                };
            }
            """);

    private static async Task WriteManifestAsync(string path, object manifest)
    {
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private static string CreateOutputDirectory(string viewport)
    {
        var root = FindRepositoryRoot();
        var output = Path.Combine(
            root.FullName,
            "tests",
            "Tempo.Blazor.E2E",
            "TestResults",
            "document-editor-canvas",
            "before-redesign",
            "2026-06-04",
            viewport);
        Directory.CreateDirectory(output);
        return output;
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx from test output directory.");
    }

    private sealed class CanvasBaselineProbe
    {
        public string Url { get; set; } = string.Empty;
        public string RenderEngine { get; set; } = string.Empty;
        public string RequestedRenderEngine { get; set; } = string.Empty;
        public bool HasCoreHost { get; set; }
        public bool HasWysiwygHost { get; set; }
        public bool HasCanvasHost { get; set; }
        public int CanvasElementCount { get; set; }
        public double ActiveHostWidth { get; set; }
        public double ActiveHostHeight { get; set; }
        public string VisibleTextSample { get; set; } = string.Empty;
    }
}
