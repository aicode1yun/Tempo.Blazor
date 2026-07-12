using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E for TmPdfViewer annotations and improved search on the PDF Viewer demo page
/// (WASM demo at 7106, API at 5100). Verifies the seeded annotation thread renders in the
/// side panel, selecting it opens the comment detail, and full-text search shows a live
/// counter with highlight and next-match navigation. Screenshots land in
/// <c>__screenshots__/pdf-viewer/</c> for UX review.
/// </summary>
[TestClass]
public class PdfViewerAnnotationsE2ETests : WasmTestBase
{
    private const string PdfPage = "/pdf-viewer";

    private async Task<IPage> OpenPdfPageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await page.GotoAsync($"{BaseUrl}{PdfPage}",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        return page;
    }

    [TestMethod]
    [TestCategory("WASM")]
    public async Task PdfViewer_Annotations_SeededThreadRendersAndOpensDetail()
    {
        var page = await OpenPdfPageAsync();

        var section = page.Locator("section:has(h2:has-text('Annotations'))");
        await section.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await section.ScrollIntoViewIfNeededAsync();

        var panel = section.Locator("[data-testid='pdf-annotation-panel']").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        var thread = section.Locator("[data-testid='pdf-annotation-thread']").First;
        await thread.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await SaveScreenshotAsync(page, "annotations-panel");

        await thread.ClickAsync();

        var detail = section.Locator("[data-testid='pdf-annotation-detail']").First;
        await detail.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        Assert.IsTrue(await detail.IsVisibleAsync(), "Selecting the seeded thread should open its comment detail.");

        var comment = section.Locator("[data-testid='pdf-annotation-comment']").First;
        await comment.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await SaveScreenshotAsync(page, "annotations-detail");
    }

    [TestMethod]
    [TestCategory("WASM")]
    public async Task PdfViewer_Search_HighlightsMatchesAndNavigates()
    {
        var page = await OpenPdfPageAsync();

        var section = page.Locator("section:has(h2:has-text('Document Search'))");
        await section.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await section.ScrollIntoViewIfNeededAsync();

        var input = section.Locator("[data-testid='pdf-search-input']").First;
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await input.FillAsync("the");
        await input.PressAsync("Enter");

        // The counter only appears once matches are found, which requires the PDF text content.
        var count = section.Locator("[data-testid='pdf-search-count']").First;
        await count.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
        var firstCount = (await count.InnerTextAsync()).Trim();
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstCount), "Search should report a match counter.");
        await SaveScreenshotAsync(page, "search-highlight");

        // Highlights are drawn into the search layer.
        var highlight = section.Locator(".tm-pdf-search-highlight").First;
        await highlight.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

        await section.Locator("[data-testid='pdf-search-next']").First.ClickAsync();
        await page.WaitForTimeoutAsync(600);
        await SaveScreenshotAsync(page, "search-next-match");
    }

    private static async Task SaveScreenshotAsync(IPage page, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot().FullName,
            "tests", "Tempo.Blazor.E2E", "__screenshots__", "pdf-viewer");
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

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
