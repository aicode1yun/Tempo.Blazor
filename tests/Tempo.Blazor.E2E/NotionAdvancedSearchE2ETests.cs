using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionAdvancedSearchE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF22: advanced page search filters by author, label, dates, content type, space, reset state, no results, and diacritics.")]
    public async Task AdvancedSearch_FiltersHighlightsAndHandlesEdges()
    {
        var page = await OpenNotionEditorAsync();
        await SeedSearchPageAsync();

        await OpenSearchAsync(page);
        await page.Locator(".tm-nps__search-input").FillAsync("beacon");
        await page.Locator(".tm-nps__filter-toggle").ClickAsync();
        await page.GetByTestId("notion-search-filter-author").FillAsync("alice");
        await page.GetByTestId("notion-search-filter-author").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-label").FillAsync("engineering");
        await page.GetByTestId("notion-search-filter-label").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-type").SelectOptionAsync("Paragraph");
        await page.GetByTestId("notion-search-filter-space").FillAsync("CF22 Knowledge Space");
        await page.GetByTestId("notion-search-filter-space").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-created-after").FillAsync("2026-01-01");
        await page.GetByTestId("notion-search-filter-edited-before").FillAsync("2026-01-31");

        await page.Locator(".tm-nps__item-snippet mark").Filter(new() { HasText = "beacon" })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var capture = await CaptureBaselineAsync("search", "cf22-advanced-search-filters", page.Locator(".tm-nps__card").First);
        TestContext.WriteLine($"UX CF22 search baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        await page.GetByTestId("notion-search-filter-label").FillAsync("support");
        await page.GetByTestId("notion-search-filter-label").PressAsync("Tab");
        await page.Locator(".tm-nps__status").Filter(new() { HasText = "No results found" })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.GetByTestId("notion-search-filter-clear").ClickAsync();
        await page.Locator(".tm-nps__item").Filter(new() { HasText = "CF22 Knowledge Space" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.Locator(".tm-nps__search-input").FillAsync("zlutoucky");
        await page.Locator(".tm-nps__item-snippet").Filter(new() { HasText = "žluťoučký" })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.GetByTestId("notion-search-filter-space").FillAsync("CF22 Support Space");
        await page.GetByTestId("notion-search-filter-space").PressAsync("Tab");
        await page.Locator(".tm-nps__search-input").FillAsync("customer");
        await page.Locator(".tm-nps__item").Filter(new() { HasText = "CF22 Escalation Notes" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
    }

    private static async Task OpenSearchAsync(IPage page)
    {
        await page.Keyboard.PressAsync("Control+p");
        await page.Locator(".tm-nps").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
