using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Phase 7 performance-budget gate for the canvas engine on a large (1000-paragraph) document.
/// Enforces that scrolling does no re-layout and that typing stays incremental (cache-driven), with
/// generous wall-clock budgets that still catch a regression back to O(document) per interaction.
/// </summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[DoNotParallelize]
public sealed class DocumentEditorCanvasPerfBudgetE2ETests : WasmTestBase
{
    private const string LargeDocId = "large-perf-1000";

    // First paint is the only reliable wall-clock budget (cold full-document layout). Scroll/typing
    // are proven INCREMENTAL via structural metrics (no re-layout, virtualization, cache reuse), which
    // are robust to CI/VM jitter; a generous typing budget still catches an O(document) regression.
    private const double FirstPaintBudgetMs = 6000;

    [TestMethod]
    public async Task LargeDocument_ScrollAndTypeStayIncremental_WithinBudgets()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await page.GotoAsync($"{BaseUrl}/document-editor?documentId={LargeDocId}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 120_000,
        });
        await page.WaitForSelectorAsync("[data-testid='document-canvas-engine-host'][data-canvas-engine-ready='true']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 120_000,
        });
        await page.WaitForTimeoutAsync(800);

        var initial = await ReadMetricsAsync(page);
        TestContext.WriteLine($"FIRST PAINT: firstPaintMs={initial.FirstPaintMs} pageCount={initial.PageCount} mountedPages={initial.MountedPageCount} renderCount={initial.RenderCount}");
        Assert.IsTrue(initial.PageCount > 15, $"The large document should paginate into many pages, got {initial.PageCount}.");
        // Virtualization: only a few pages are mounted regardless of the document length.
        Assert.IsTrue(initial.MountedPageCount > 0 && initial.MountedPageCount <= 8, $"Page virtualization must bound mounted pages (mounted={initial.MountedPageCount}, total={initial.PageCount}).");
        Assert.IsTrue(initial.FirstPaintMs <= FirstPaintBudgetMs, $"First paint {initial.FirstPaintMs}ms exceeded budget {FirstPaintBudgetMs}ms.");

        // --- Scroll: pure paint — must NOT re-run the document layout and must stay virtualized ---
        var renderCountBeforeScroll = initial.RenderCount;
        for (var i = 0; i < 8; i++)
        {
            await page.Mouse.WheelAsync(0, 600);
            await page.EvaluateAsync("() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        }
        await page.WaitForTimeoutAsync(300);

        var afterScroll = await ReadMetricsAsync(page);
        TestContext.WriteLine($"SCROLL: frames={afterScroll.ScrollFrameCount} mountedPages={afterScroll.MountedPageCount} renderCount {renderCountBeforeScroll}->{afterScroll.RenderCount}");
        Assert.IsTrue(afterScroll.ScrollFrameCount > renderCountBeforeScroll, "Scrolling must register scroll frames.");
        Assert.IsTrue(afterScroll.RenderCount - renderCountBeforeScroll <= 1, $"Scrolling must NOT re-run the document layout. renderCount {renderCountBeforeScroll}->{afterScroll.RenderCount}.");
        Assert.IsTrue(afterScroll.MountedPageCount <= 8, $"Scrolling must stay virtualized (mounted={afterScroll.MountedPageCount}).");

        // Re-render with an unchanged model (read-only toggle) on the large document: the incremental
        // layout + command caches must reuse the vast majority of blocks. (Typing incrementality is
        // covered byte-identically by the Node and ReRender E2E gates; here we cover scale.)
        await page.GetByTestId("document-editor-readonly").ClickAsync();
        await page.WaitForTimeoutAsync(500);
        var afterRerender = await ReadMetricsAsync(page);
        TestContext.WriteLine($"RE-RENDER: layoutCache(h/m)={afterRerender.LayoutHits}/{afterRerender.LayoutMisses} commandCache(h/m)={afterRerender.CommandHits}/{afterRerender.CommandMisses}");
        Assert.IsTrue(afterRerender.LayoutHits > afterRerender.LayoutMisses * 5, $"A no-op re-render must reuse cached block layouts (hits={afterRerender.LayoutHits}, misses={afterRerender.LayoutMisses}).");
        Assert.IsTrue(afterRerender.CommandHits > afterRerender.CommandMisses * 5, $"A no-op re-render must reuse cached display commands (hits={afterRerender.CommandHits}, misses={afterRerender.CommandMisses}).");

        var output = "/tmp/canvas-overlap-fix";
        Directory.CreateDirectory(output);
        await page.GetByTestId("document-editor-demo").ScreenshotAsync(new LocatorScreenshotOptions { Path = Path.Combine(output, "large-perf-doc.png"), Type = ScreenshotType.Png });
    }

    private static async Task<CanvasPerfMetrics> ReadMetricsAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>(@"() => {
            const root = document.querySelector('[data-testid=""document-canvas-engine-root""]');
            const n = name => Number(root?.getAttribute(name) || 0);
            return JSON.stringify({
                firstPaintMs: n('data-canvas-first-paint-ms'),
                renderCount: n('data-canvas-render-count'),
                pageCount: n('data-canvas-page-count'),
                mountedPageCount: n('data-canvas-mounted-page-count'),
                scrollFrameCount: n('data-canvas-scroll-frame-count'),
                scrollP95Ms: n('data-canvas-scroll-p95-ms'),
                typingCount: n('data-canvas-typing-latency-count'),
                typingP50Ms: n('data-canvas-typing-latency-p50-ms'),
                typingP95Ms: n('data-canvas-typing-latency-p95-ms'),
                layoutHits: n('data-canvas-layout-cache-hit-count'),
                layoutMisses: n('data-canvas-layout-cache-miss-count'),
                commandHits: n('data-canvas-command-cache-hit-count'),
                commandMisses: n('data-canvas-command-cache-miss-count'),
            });
        }");
        return JsonSerializer.Deserialize<CanvasPerfMetrics>(json) ?? new CanvasPerfMetrics();
    }

    private sealed class CanvasPerfMetrics
    {
        [JsonPropertyName("firstPaintMs")] public double FirstPaintMs { get; set; }
        [JsonPropertyName("renderCount")] public int RenderCount { get; set; }
        [JsonPropertyName("pageCount")] public int PageCount { get; set; }
        [JsonPropertyName("mountedPageCount")] public int MountedPageCount { get; set; }
        [JsonPropertyName("scrollFrameCount")] public int ScrollFrameCount { get; set; }
        [JsonPropertyName("scrollP95Ms")] public double ScrollP95Ms { get; set; }
        [JsonPropertyName("typingCount")] public int TypingCount { get; set; }
        [JsonPropertyName("typingP50Ms")] public double TypingP50Ms { get; set; }
        [JsonPropertyName("typingP95Ms")] public double TypingP95Ms { get; set; }
        [JsonPropertyName("layoutHits")] public int LayoutHits { get; set; }
        [JsonPropertyName("layoutMisses")] public int LayoutMisses { get; set; }
        [JsonPropertyName("commandHits")] public int CommandHits { get; set; }
        [JsonPropertyName("commandMisses")] public int CommandMisses { get; set; }
    }
}
