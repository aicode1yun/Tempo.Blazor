using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Phase 0/1 evidence for the canvas engine performance + rendering fix
/// (planning/tmdocumenteditor-canvas-performance-and-rendering-fix-todo-2026-06-08.md).
/// Opens the live /document-editor (canvas default) contract demo, captures screenshots,
/// asserts that text runs from different blocks do not overlap, and records a perf baseline.
/// </summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[DoNotParallelize]
public sealed class DocumentEditorCanvasOverlapPerfE2ETests : WasmTestBase
{
    private const string OutputDir = "/tmp/canvas-overlap-fix";

    [TestMethod]
    public async Task ContractDemo_CanvasEngine_RendersWithoutOverlaps_AndCapturesBaseline()
    {
        Directory.CreateDirectory(OutputDir);

        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/document-editor", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 120_000,
        });

        // Wait for the canvas engine host to mount and report ready.
        await page.WaitForSelectorAsync("[data-testid='document-canvas-engine-host'][data-canvas-engine-ready='true']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 120_000,
        });
        await page.WaitForTimeoutAsync(800);

        // Confirm the page is really running the canvas engine, not legacy.
        var renderEngine = await page.GetAttributeAsync("[data-testid='document-editor-demo']", "data-render-engine");
        Assert.AreEqual("CanvasEnginePreview", renderEngine, "The /document-editor demo must run the canvas engine by default.");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(OutputDir, "contract-demo-full.png"),
            FullPage = true,
            Type = ScreenshotType.Png,
        });
        await page.GetByTestId("document-editor-demo").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = Path.Combine(OutputDir, "contract-demo-editor.png"),
            Type = ScreenshotType.Png,
        });

        // Collect text-run rectangles from the canvas metadata layer and detect cross-block overlaps.
        var overlapJson = await page.EvaluateAsync<string>(@"() => {
            const rects = [...document.querySelectorAll('[data-canvas-text-rect]')].map(el => {
                const r = el.getBoundingClientRect();
                return {
                    blockId: el.getAttribute('data-block-id') || '',
                    text: (el.getAttribute('data-canvas-text') || '').slice(0, 24),
                    x: r.x, y: r.y, w: r.width, h: r.height,
                };
            }).filter(r => r.w > 1 && r.h > 4 && r.text.trim().length > 0);

            const overlaps = [];
            for (let i = 0; i < rects.length; i++) {
                for (let j = i + 1; j < rects.length; j++) {
                    const a = rects[i], b = rects[j];
                    if (!a.blockId || !b.blockId || a.blockId === b.blockId) continue;
                    const ox = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x);
                    const oy = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
                    // require a meaningful 2D overlap (ignore 1px touching / shared baselines)
                    if (ox > 3 && oy > 4) {
                        overlaps.push({ a, b, ox: Math.round(ox), oy: Math.round(oy) });
                    }
                }
            }

            const objects = [...document.querySelectorAll('[data-canvas-object][data-wrap-mode]')].map(el => {
                const r = el.getBoundingClientRect();
                return { id: el.getAttribute('data-object-id') || '', wrap: el.getAttribute('data-wrap-mode') || '', x: r.x, y: r.y, w: r.width, h: r.height };
            }).filter(o => (o.wrap === 'Square' || o.wrap === 'Tight' || o.wrap === 'TopBottom') && o.w > 1 && o.h > 1);

            const objectOverlaps = [];
            for (let i = 0; i < objects.length; i++) {
                for (let j = i + 1; j < objects.length; j++) {
                    const a = objects[i], b = objects[j];
                    const ox = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x);
                    const oy = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
                    if (ox > 2 && oy > 2) objectOverlaps.push({ a, b });
                }
            }

            const root = document.querySelector('[data-testid=""document-canvas-engine-root""]');
            const perf = root ? {
                firstPaintMs: root.getAttribute('data-canvas-first-paint-ms'),
                renderCount: root.getAttribute('data-canvas-render-count'),
                renderP95Ms: root.getAttribute('data-canvas-render-p95-ms'),
                mountedPageCount: root.getAttribute('data-canvas-mounted-page-count'),
                pageCount: root.getAttribute('data-canvas-page-count'),
            } : {};

            return JSON.stringify({
                rectCount: rects.length,
                overlapCount: overlaps.length,
                overlaps: overlaps.slice(0, 8),
                floatingObjectCount: objects.length,
                objectOverlapCount: objectOverlaps.length,
                objectOverlaps: objectOverlaps.slice(0, 8),
                perf,
            });
        }");

        await File.WriteAllTextAsync(Path.Combine(OutputDir, "overlap-report.json"), Prettify(overlapJson));
        TestContext.WriteLine("Overlap/perf report: " + overlapJson);

        using var report = JsonDocument.Parse(overlapJson);
        var root = report.RootElement;
        var rectCount = root.GetProperty("rectCount").GetInt32();
        var overlapCount = root.GetProperty("overlapCount").GetInt32();
        var objectOverlapCount = root.GetProperty("objectOverlapCount").GetInt32();

        Assert.IsTrue(rectCount > 20, $"Expected the contract demo to render many text runs, got {rectCount}.");
        Assert.AreEqual(0, objectOverlapCount, $"Floating images must not overlap each other. Report: {overlapJson}");
        Assert.AreEqual(0, overlapCount, $"Text runs from different blocks must not overlap. Report: {overlapJson}");
    }

    private static string Prettify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}
