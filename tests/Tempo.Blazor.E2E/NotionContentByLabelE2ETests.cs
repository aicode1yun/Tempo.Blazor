using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionContentByLabelE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Content-by-Label macro can be configured, lists matching pages, applies MaxItems, hides deleted pages, and navigates to a result.")]
    public async Task ContentByLabel_ConfigureRenderLimitAndNavigate()
    {
        var page = await OpenNotionEditorAsync();
        await SeedContentByLabelPageAsync();

        var blocks = page.Locator(".tm-cbl");
        await blocks.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await blocks.CountAsync() >= 3, "The seeded page should render configurable, limited, and empty Content-by-Label blocks.");

        var configurable = blocks.Nth(0);
        await configurable.Locator(".tm-cbl__label-select").SelectOptionAsync("release");
        await configurable.Locator(".tm-cbl__add-label").ClickAsync();
        await configurable.Locator(".tm-cbl__page").Filter(new() { HasText = "CF7 Alpha Release" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await configurable.Locator(".tm-cbl__page").Filter(new() { HasText = "CF7 Beta Release" }).IsVisibleAsync());
        Assert.AreEqual(0, await configurable.Locator(".tm-cbl__page").Filter(new() { HasText = "CF7 Deleted Release" }).CountAsync());
        Assert.IsTrue(await configurable.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Configured Content-by-Label block should not overflow horizontally.");
        await CaptureBaselineAndAssertAsync("cf7-configured-block", configurable);

        var limited = blocks.Nth(1);
        await limited.Locator(".tm-cbl__page").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.AreEqual(1, await limited.Locator(".tm-cbl__page").CountAsync());
        Assert.IsTrue(await limited.Locator(".tm-cbl__page").Filter(new() { HasText = "CF7 Beta Release" }).IsVisibleAsync());
        await CaptureBaselineAndAssertAsync("cf7-limited-max-items", limited);

        var empty = blocks.Nth(2);
        await empty.Locator(".tm-cbl__empty").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAndAssertAsync("cf7-empty-no-match", empty);

        await TakeScreenshotAsync(page, "notion_content_by_label_macro");
        await CaptureBaselineAsync("content-by-label", "cf7-configured-limited-empty", page.Locator(".tm-notion-page").First);

        await configurable.Locator(".tm-cbl__page").Filter(new() { HasText = "CF7 Alpha Release" }).First.ClickAsync();
        await page.Locator(".tm-notion-header-title").Filter(new() { HasText = "CF7 Alpha Release" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    private async Task CaptureBaselineAndAssertAsync(string state, ILocator region)
    {
        await region.ScrollIntoViewIfNeededAsync();
        var capture = await CaptureBaselineAsync("content-by-label", state, region);
        Assert.IsTrue(File.Exists(capture.FullPagePath), $"CF7 full-page baseline should be written for {state}.");
        Assert.IsTrue(File.Exists(capture.RegionPath), $"CF7 region baseline should be written for {state}.");
    }
}
