using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionPageInfoE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF16: Page Info opens from the page settings menu, renders page metadata, word count, reading time, optional views, edge states, and captures a UX baseline.")]
    public async Task PageInfoPanel_MetadataStatsAnalyticsAndEdges()
    {
        var page = await OpenNotionEditorAsync();
        await SeedPageInfoPageAsync();

        await OpenPageInfoPanelAsync(page);
        var panel = page.Locator(".tm-page-info").First;
        await panel.Locator(".tm-page-info__created").Filter(new() { HasText = "Ada Lovelace" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await panel.Locator(".tm-page-info__last-edited").Filter(new() { HasText = "Grace Hopper" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual("10", (await panel.Locator(".tm-page-info__words .tm-page-info__metric-value").TextContentAsync())?.Trim());
        Assert.AreEqual("1 min", (await panel.Locator(".tm-page-info__reading-time .tm-page-info__metric-value").TextContentAsync())?.Trim());
        Assert.AreEqual("128", (await panel.Locator(".tm-page-info__views .tm-page-info__metric-value").TextContentAsync())?.Trim());
        Assert.IsTrue(await panel.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Page Info panel should not overflow horizontally.");

        await CaptureBaselineAsync("page-info", "cf16-page-info-metadata-stats-analytics", panel);
        var capture = await CaptureBaselineAsync("page-info", "cf16-page-info-panel-baseline", panel);
        TestContext.WriteLine($"UX CF16 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        var edgePage = await OpenNotionEditorAsync("?disableAnalyticsProvider=true");
        await SeedEmptyPageInfoPageAsync();
        await OpenPageInfoPanelAsync(edgePage);
        var edgePanel = edgePage.Locator(".tm-page-info").First;
        await edgePanel.Locator(".tm-page-info__created").Filter(new() { HasText = "Unknown author" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual("0", (await edgePanel.Locator(".tm-page-info__words .tm-page-info__metric-value").TextContentAsync())?.Trim());
        Assert.AreEqual("0 min", (await edgePanel.Locator(".tm-page-info__reading-time .tm-page-info__metric-value").TextContentAsync())?.Trim());
        Assert.AreEqual(0, await edgePanel.Locator(".tm-page-info__views").CountAsync());
        Assert.IsTrue(await edgePanel.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Empty Page Info panel should not overflow horizontally.");

        await CaptureBaselineAsync("page-info", "cf16-page-info-empty-no-analytics", edgePanel);
        await CaptureBaselineAsync("page-info", "cf16-page-info-unknown-author-fallback", edgePanel.Locator(".tm-page-info__created").First);
    }

    private static async Task OpenPageInfoPanelAsync(IPage page)
    {
        await page.Locator(".tm-npsm-trigger").First.ClickAsync();
        await page.Locator(".tm-npsm__item").Filter(new() { HasText = "Page info" }).ClickAsync();
        await page.Locator(".tm-page-info").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
