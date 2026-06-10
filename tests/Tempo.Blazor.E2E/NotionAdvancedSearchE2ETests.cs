using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionAdvancedSearchE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF22: advanced page search captures filters, highlighted results, no-results, edge filters, and mobile overflow.")]
    public async Task AdvancedSearch_FiltersHighlightsNoResultsEdgesAndMobile_AreCaptured()
    {
        var page = await OpenNotionEditorAsync();
        await SeedSearchPageAsync();

        await OpenSearchAsync(page);
        await page.Locator(".tm-nps__filter-toggle").ClickAsync();

        var filtersCapture = await CaptureBaselineAsync("search", "cf22-advanced-search-filters", page.Locator(".tm-nps__card").First);
        TestContext.WriteLine($"UX CF22 filter panel baseline captured: {filtersCapture.FullPagePath} / {filtersCapture.RegionPath}");

        await page.Locator(".tm-nps__search-input").FillAsync("beacon");
        await WaitForHighlightedResultAsync(page, "beacon");

        var highlightsCapture = await CaptureBaselineAsync("search", "cf22-advanced-search-highlighted-results", page.Locator(".tm-nps__card").First);
        TestContext.WriteLine($"UX CF22 highlighted result baseline captured: {highlightsCapture.FullPagePath} / {highlightsCapture.RegionPath}");

        await ApplyEdgeFiltersAsync(page, query: "beacon", author: "alice", label: "engineering", space: "CF22 Knowledge Space");
        await WaitForHighlightedResultAsync(page, "beacon");

        var edgeCapture = await CaptureBaselineAsync("search", "cf22-advanced-search-edge-filters", page.Locator(".tm-nps__card").First);
        TestContext.WriteLine($"UX CF22 edge filters baseline captured: {edgeCapture.FullPagePath} / {edgeCapture.RegionPath}");

        await page.GetByTestId("notion-search-filter-label").FillAsync("support");
        await page.GetByTestId("notion-search-filter-label").PressAsync("Tab");
        await page.Locator(".tm-nps__status").Filter(new() { HasText = "No results found" })
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var noResultsCapture = await CaptureBaselineAsync("search", "cf22-advanced-search-no-results", page.Locator(".tm-nps__card").First);
        TestContext.WriteLine($"UX CF22 no-results baseline captured: {noResultsCapture.FullPagePath} / {noResultsCapture.RegionPath}");

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

        var mobile = await OpenNotionEditorAsync(390, 844);
        await SeedSearchPageAsync();
        await OpenSearchAsync(mobile);
        await mobile.Locator(".tm-nps__filter-toggle").ClickAsync();
        await mobile.Locator(".tm-nps__search-input").FillAsync("customer");
        await ApplyEdgeFiltersAsync(mobile, query: "customer", author: string.Empty, label: "support", space: "CF22 Support Space");
        await mobile.Locator(".tm-nps__item").Filter(new() { HasText = "CF22 Escalation Notes" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var mobileCapture = await CaptureBaselineAsync("search", "cf22-advanced-search-mobile-overflow", mobile.Locator(".tm-nps__card").First);
        TestContext.WriteLine($"UX CF22 mobile overflow baseline captured: {mobileCapture.FullPagePath} / {mobileCapture.RegionPath}");
    }

    private static async Task ApplyEdgeFiltersAsync(IPage page, string query, string author, string label, string space)
    {
        await page.Locator(".tm-nps__search-input").FillAsync(query);
        await page.GetByTestId("notion-search-filter-author").FillAsync(author);
        await page.GetByTestId("notion-search-filter-author").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-label").FillAsync(label);
        await page.GetByTestId("notion-search-filter-label").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-type").SelectOptionAsync("Paragraph");
        await page.GetByTestId("notion-search-filter-space").FillAsync(space);
        await page.GetByTestId("notion-search-filter-space").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-created-after").FillAsync("2026-01-01");
        await page.GetByTestId("notion-search-filter-created-after").PressAsync("Tab");
        await page.GetByTestId("notion-search-filter-edited-before").FillAsync("2026-01-31");
        await page.GetByTestId("notion-search-filter-edited-before").PressAsync("Tab");
    }

    private static Task WaitForHighlightedResultAsync(IPage page, string text)
    {
        return page.Locator(".tm-nps__item-snippet mark").Filter(new() { HasText = text })
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
