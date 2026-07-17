using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E for TmPdfAnnotator on the PDF Annotator demo page (WASM demo at 7106, API at 5100).
/// Covers the seeded showcase (all four annotation kinds with author colors), the two-user
/// collaboration flow (comment → visible in the other pane → reply → resolve), reload
/// persistence through the localStorage provider, freehand drawing, stamps, export
/// downloads (real annotations + flattened), and edge cases (cancelled draft, click
/// without drag in draw mode). Screenshots land in <c>__screenshots__/pdf-annotator/</c>.
/// </summary>
[TestClass]
public class PdfAnnotatorE2ETests : WasmTestBase
{
    private const string AnnotatorPage = "/pdf-annotator";

    private sealed record AnnotatorPageHandle(IPage Page, List<string> Errors);

    private async Task<AnnotatorPageHandle> OpenAnnotatorPageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1600, 1000);

        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error" && msg.Text.Contains("Unhandled exception"))
            {
                errors.Add(msg.Text);
            }
        };

        await page.GotoAsync($"{BaseUrl}{AnnotatorPage}",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        return new AnnotatorPageHandle(page, errors);
    }

    private static void AssertNoBlazorErrors(AnnotatorPageHandle handle)
        => Assert.AreEqual(0, handle.Errors.Count,
            "The page raised unhandled exceptions: " + string.Join(" | ", handle.Errors));

    private static async Task WaitForSurfaceReadyAsync(ILocator pane)
    {
        var surface = pane.Locator("[data-testid='pdf-annotator-surface']").First;
        await surface.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000, State = WaitForSelectorState.Attached });
        // The interaction surface is kept aligned with the rendered PDF canvas; a pre-render
        // canvas is 300x150, so wait until the synced size reflects an actual PDF page.
        await surface.Page.WaitForFunctionAsync(
            "el => el.clientWidth > 350 && el.clientHeight > 400",
            await surface.ElementHandleAsync(),
            new PageWaitForFunctionOptions { Timeout = 60000 });
    }

    // ── Seeded showcase: all four kinds + author colors ──────────────────────

    [TestMethod]
    [TestCategory("WASM")]
    public async Task Annotator_Showcase_RendersAllKindsWithAuthorColors()
    {
        var handle = await OpenAnnotatorPageAsync();
        var page = handle.Page;

        var showcase = page.Locator("[data-testid='pdf-annotator-showcase']");
        await showcase.ScrollIntoViewIfNeededAsync();
        await WaitForSurfaceReadyAsync(showcase);

        var highlight = showcase.Locator("[data-testid='pdf-annotator-highlight']").First;
        await highlight.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        var stamp = showcase.Locator("[data-testid='pdf-annotator-stamp']").First;
        await stamp.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        StringAssert.Contains(await stamp.InnerTextAsync(), "APPROVED");
        var ink = showcase.Locator("[data-testid='pdf-annotator-ink']").First;
        await ink.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000, State = WaitForSelectorState.Attached });
        var marker = showcase.Locator("[data-testid='pdf-annotator-marker']").First;
        await marker.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

        // Author colors: Alena's highlight is amber (#b45309), Bedřich's marker violet (#7c3aed).
        StringAssert.Contains((await highlight.GetAttributeAsync("style")) ?? "", "#b45309");
        StringAssert.Contains((await marker.GetAttributeAsync("style")) ?? "", "#7c3aed");

        // Panel lists all four seeded threads.
        Assert.AreEqual(4, await showcase.Locator("[data-testid='pdf-annotator-thread']").CountAsync());

        await SaveScreenshotAsync(page, "showcase-all-kinds");
        AssertNoBlazorErrors(handle);
    }

    // ── Two users: comment → other pane → reply → resolve ───────────────────

    [TestMethod]
    [TestCategory("WASM")]
    public async Task Annotator_TwoUsers_CommentReplyResolveFlow()
    {
        var handle = await OpenAnnotatorPageAsync();
        var page = handle.Page;

        var alice = page.Locator("[data-testid='pdf-annotator-pane-alice']");
        var bob = page.Locator("[data-testid='pdf-annotator-pane-bob']");
        await alice.ScrollIntoViewIfNeededAsync();
        await WaitForSurfaceReadyAsync(alice);
        await WaitForSurfaceReadyAsync(bob);

        // Alice adds a comment pin.
        await alice.Locator("[data-testid='pdf-annotator-mode-comment']").ClickAsync();
        await alice.Locator("[data-testid='pdf-annotator-surface']")
            .ClickAsync(new LocatorClickOptions { Position = new Position { X = 150, Y = 120 } });
        var composer = alice.Locator("[data-testid='pdf-annotator-new-input']");
        await composer.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await composer.FillAsync("Prosím zkontrolovat tento odstavec.");
        await SaveScreenshotAsync(page, "two-users-draft");
        await alice.Locator("[data-testid='pdf-annotator-new-submit']").ClickAsync();

        // The thread appears in BOTH panes (shared provider).
        await alice.Locator("[data-testid='pdf-annotator-thread']").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await bob.Locator("[data-testid='pdf-annotator-thread']").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await SaveScreenshotAsync(page, "two-users-thread-shared");

        // Bob opens the thread and replies.
        await bob.Locator("[data-testid='pdf-annotator-thread']").First.ClickAsync();
        var replyInput = bob.Locator("[data-testid='pdf-annotator-reply-input']");
        await replyInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await replyInput.FillAsync("Opraveno, díky.");
        await bob.Locator("[data-testid='pdf-annotator-reply-submit']").ClickAsync();

        // The reply shows up in Alice's detail as well. Alice's detail is already open
        // (creating a thread selects it), so clicking the head again would toggle it closed.
        if (await alice.Locator("[data-testid='pdf-annotator-detail']").CountAsync() == 0)
        {
            await alice.Locator("[data-testid='pdf-annotator-thread']").First.ClickAsync();
        }

        await alice.Locator("[data-testid='pdf-annotator-comment']").Nth(1)
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await SaveScreenshotAsync(page, "two-users-reply");

        // Bob resolves — the thread disappears from the default (open) filter in both panes.
        await bob.Locator("[data-testid='pdf-annotator-resolve']").ClickAsync();
        await Assertions.Expect(alice.Locator("[data-testid='pdf-annotator-thread']"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 15000 });
        await Assertions.Expect(bob.Locator("[data-testid='pdf-annotator-thread']"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 15000 });

        // The resolved filter shows it again, marked as resolved.
        await alice.Locator("[data-testid='pdf-annotator-show-resolved']").CheckAsync();
        await alice.Locator("[data-testid='pdf-annotator-thread']").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await SaveScreenshotAsync(page, "two-users-resolved");
        AssertNoBlazorErrors(handle);
    }

    // ── Reload persistence (localStorage provider) ───────────────────────────

    [TestMethod]
    [TestCategory("WASM")]
    public async Task Annotator_Annotations_SurviveReload()
    {
        var handle = await OpenAnnotatorPageAsync();
        var page = handle.Page;

        var alice = page.Locator("[data-testid='pdf-annotator-pane-alice']");
        await alice.ScrollIntoViewIfNeededAsync();
        await WaitForSurfaceReadyAsync(alice);

        await alice.Locator("[data-testid='pdf-annotator-mode-comment']").ClickAsync();
        await alice.Locator("[data-testid='pdf-annotator-surface']")
            .ClickAsync(new LocatorClickOptions { Position = new Position { X = 200, Y = 200 } });
        var composer = alice.Locator("[data-testid='pdf-annotator-new-input']");
        await composer.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await composer.FillAsync("Persistence check");
        await alice.Locator("[data-testid='pdf-annotator-new-submit']").ClickAsync();
        await alice.Locator("[data-testid='pdf-annotator-thread']").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

        // Reload the page: the same browser context keeps localStorage.
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);

        var aliceAfter = page.Locator("[data-testid='pdf-annotator-pane-alice']");
        await aliceAfter.ScrollIntoViewIfNeededAsync();
        var thread = aliceAfter.Locator("[data-testid='pdf-annotator-thread']").First;
        await thread.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
        await thread.ClickAsync();
        var comment = aliceAfter.Locator("[data-testid='pdf-annotator-comment']").First;
        await comment.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        StringAssert.Contains(await comment.InnerTextAsync(), "Persistence check");

        await SaveScreenshotAsync(page, "reload-persistence");
        AssertNoBlazorErrors(handle);
    }

    // ── Stamp + freehand drawing ─────────────────────────────────────────────

    [TestMethod]
    [TestCategory("WASM")]
    public async Task Annotator_StampAndDrawing_CreateAnnotations()
    {
        var handle = await OpenAnnotatorPageAsync();
        var page = handle.Page;

        var alice = page.Locator("[data-testid='pdf-annotator-pane-alice']");
        await alice.ScrollIntoViewIfNeededAsync();
        await WaitForSurfaceReadyAsync(alice);

        // Stamp: pick REJECTED from the picker and click the page — created immediately.
        await alice.Locator("[data-testid='pdf-annotator-mode-stamp']").ClickAsync();
        var stampSelect = alice.Locator("[data-testid='pdf-annotator-stamp-select']");
        await stampSelect.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await stampSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await alice.Locator("[data-testid='pdf-annotator-surface']")
            .ClickAsync(new LocatorClickOptions { Position = new Position { X = 260, Y = 100 } });
        var stamp = alice.Locator("[data-testid='pdf-annotator-stamp']").First;
        await stamp.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await SaveScreenshotAsync(page, "stamp-placed");

        // Drawing: drag a stroke, then save the draft.
        await alice.Locator("[data-testid='pdf-annotator-mode-draw']").ClickAsync();
        var surface = alice.Locator("[data-testid='pdf-annotator-surface']");
        var box = await surface.BoundingBoxAsync();
        Assert.IsNotNull(box, "The drawing surface should have a bounding box.");
        await page.Mouse.MoveAsync((float)(box!.X + 60), (float)(box.Y + 220));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(box.X + 140), (float)(box.Y + 260), new MouseMoveOptions { Steps = 8 });
        await page.Mouse.MoveAsync((float)(box.X + 220), (float)(box.Y + 210), new MouseMoveOptions { Steps = 8 });
        await page.Mouse.UpAsync();

        var composer = alice.Locator("[data-testid='pdf-annotator-new']");
        await composer.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await alice.Locator("[data-testid='pdf-annotator-new-submit']").ClickAsync();

        var ink = alice.Locator("[data-testid='pdf-annotator-ink']").First;
        await ink.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000, State = WaitForSelectorState.Attached });
        await SaveScreenshotAsync(page, "drawing-saved");
        AssertNoBlazorErrors(handle);
    }

    // ── Export: real annotations + flattened variant download ───────────────

    [TestMethod]
    [TestCategory("WASM")]
    public async Task Annotator_Export_DownloadsAnnotatedPdf()
    {
        var handle = await OpenAnnotatorPageAsync();
        var page = handle.Page;

        var showcase = page.Locator("[data-testid='pdf-annotator-showcase']");
        await showcase.ScrollIntoViewIfNeededAsync();
        await WaitForSurfaceReadyAsync(showcase);
        await showcase.Locator("[data-testid='pdf-annotator-thread']").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        // Export with embedded PDF annotations.
        IDownload download;
        try
        {
            download = await page.RunAndWaitForDownloadAsync(
                () => showcase.Locator("[data-testid='pdf-annotator-export']").ClickAsync(),
                new PageRunAndWaitForDownloadOptions { Timeout = 90000 });
        }
        catch (TimeoutException)
        {
            // Surface the real JS failure instead of a bare timeout.
            var diagnostic = await page.EvaluateAsync<string>(
                """
                async () => {
                    try {
                        await window.tmPdfAnnotator.exportPdf(
                            'https://mozilla.github.io/pdf.js/web/compressed.tracemonkey-pldi-09.pdf',
                            '[]',
                            '{"flatten":false,"fileName":"diag.pdf"}');
                        return 'direct exportPdf succeeded — the button path failed silently';
                    } catch (e) {
                        return 'exportPdf failed: ' + (e && (e.stack || e.message || String(e)));
                    }
                }
                """);
            throw new AssertFailedException(
                $"Download event did not arrive. Diagnostic: {diagnostic} | PageErrors: {string.Join(" | ", handle.Errors)}");
        }

        Assert.AreEqual("tempo-annotated.pdf", download.SuggestedFilename);
        var exportSize = await AssertDownloadedPdfAsync(page, download, "annotation-object export");

        // Flattened export (annotations drawn into page content + summary page).
        var flatDownload = await page.RunAndWaitForDownloadAsync(
            () => showcase.Locator("[data-testid='pdf-annotator-export-flat']").ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 90000 });
        var flatSize = await AssertDownloadedPdfAsync(page, flatDownload, "flattened export");

        // The flattened file must differ from the annotation-object file.
        Assert.AreNotEqual(exportSize, flatSize,
            "Flattened export should produce different content than the annotation-object export.");

        await SaveScreenshotAsync(page, "export-buttons");
        AssertNoBlazorErrors(handle);
    }

    private static async Task<long> AssertDownloadedPdfAsync(IPage page, IDownload download, string label)
    {
        // Read the exported bytes through the blob URL rather than the download artifact:
        // Playwright tracing instruments blob downloads and the artifact ends up "canceled",
        // while the blob content itself stays readable (the export revokes it after 60 s).
        var info = await page.EvaluateAsync<System.Text.Json.JsonElement>(
            """
            async (url) => {
                const response = await fetch(url);
                const buffer = await response.arrayBuffer();
                return {
                    size: buffer.byteLength,
                    header: new TextDecoder().decode(new Uint8Array(buffer.slice(0, 4)))
                };
            }
            """,
            download.Url);

        var size = info.GetProperty("size").GetInt64();
        Assert.IsTrue(size > 1000, $"The {label} is suspiciously small ({size} bytes).");
        Assert.AreEqual("%PDF", info.GetProperty("header").GetString(),
            $"The {label} should start with the %PDF header.");
        return size;
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [TestMethod]
    [TestCategory("WASM")]
    public async Task Annotator_EdgeCases_CancelDraftAndClickWithoutDrag()
    {
        var handle = await OpenAnnotatorPageAsync();
        var page = handle.Page;

        var alice = page.Locator("[data-testid='pdf-annotator-pane-alice']");
        await alice.ScrollIntoViewIfNeededAsync();
        await WaitForSurfaceReadyAsync(alice);

        // Edge 1: cancelling a comment draft creates nothing.
        await alice.Locator("[data-testid='pdf-annotator-mode-comment']").ClickAsync();
        await alice.Locator("[data-testid='pdf-annotator-surface']")
            .ClickAsync(new LocatorClickOptions { Position = new Position { X = 120, Y = 160 } });
        var cancel = alice.Locator("[data-testid='pdf-annotator-new-cancel']");
        await cancel.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await SaveScreenshotAsync(page, "edge-draft-before-cancel");
        await cancel.ClickAsync();
        await Assertions.Expect(alice.Locator("[data-testid='pdf-annotator-new']"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 10000 });
        await Assertions.Expect(alice.Locator("[data-testid='pdf-annotator-thread']"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 10000 });

        // Edge 2: in draw mode a click without dragging (single-point stroke) must not open a draft.
        await alice.Locator("[data-testid='pdf-annotator-mode-draw']").ClickAsync();
        var surface = alice.Locator("[data-testid='pdf-annotator-surface']");
        var box = await surface.BoundingBoxAsync();
        Assert.IsNotNull(box);
        await page.Mouse.MoveAsync((float)(box!.X + 100), (float)(box.Y + 100));
        await page.Mouse.DownAsync();
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);
        await Assertions.Expect(alice.Locator("[data-testid='pdf-annotator-new']"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 5000 });

        // Edge 3: empty panel shows the localized empty state.
        var empty = alice.Locator("[data-testid='pdf-annotator-empty']");
        await empty.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await SaveScreenshotAsync(page, "edge-empty-state");
        AssertNoBlazorErrors(handle);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task SaveScreenshotAsync(IPage page, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot().FullName,
            "tests", "Tempo.Blazor.E2E", "__screenshots__", "pdf-annotator");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{fileName}.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
    }

    private static DirectoryInfo FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            {
                return directory;
            }

            directory = directory.Parent!;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
