using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionIncludePageE2ETests : NotionE2ETestBase
{
    private static ILocator IncludeBlock(IPage page, string blockId) =>
        page.Locator($"[data-block-id='{blockId}'] .tm-include-page").First;

    private async Task CaptureIncludeBaselineAsync(string state, ILocator region)
    {
        await region.ScrollIntoViewIfNeededAsync();
        await Page.WaitForTimeoutAsync(300);
        var capture = await CaptureBaselineAsync("include-page", state, region);
        TestContext.WriteLine($"UX CF12 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF12: Include Page macro selects a source page, renders read-only source blocks, handles edge states, captures UX baseline, and navigates to the source page.")]
    public async Task IncludePage_SelectRenderEdgeStatesAndNavigate()
    {
        var page = await OpenNotionEditorAsync();
        await SeedIncludePagePageAsync();

        var blocks = page.Locator(".tm-include-page");
        await blocks.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await blocks.CountAsync() >= 5, "The seeded page should render configurable, deleted, cyclic, empty, and nested Include Page blocks.");

        var configurable = IncludeBlock(page, "cf120000-0000-0000-0000-000000000010");
        await configurable.Locator(".tm-include-page__select").SelectOptionAsync("22222222-2222-2222-2222-222222222222");
        await configurable.Locator(".tm-include-page__paragraph").Filter(new() { HasText = "Included source paragraph" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(0, await configurable.Locator("[contenteditable='true']").CountAsync(), "Included source blocks must render read-only.");
        Assert.IsTrue(await configurable.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Configured Include Page block should not overflow horizontally.");
        await CaptureIncludeBaselineAsync("cf12-valid-include-page", configurable);

        var deleted = IncludeBlock(page, "cf120000-0000-0000-0000-000000000020");
        await deleted.Locator(".tm-include-page__source").Filter(new() { HasText = "Missing source page" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await deleted.Locator(".tm-include-page__state").Filter(new() { HasText = "could not be found" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureIncludeBaselineAsync("cf12-deleted-missing-source", deleted);

        var cyclic = IncludeBlock(page, "cf120000-0000-0000-0000-000000000030");
        await cyclic.Locator(".tm-include-page__state").Filter(new() { HasText = "cycle" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureIncludeBaselineAsync("cf12-cycle-guard", cyclic);

        var empty = IncludeBlock(page, "cf120000-0000-0000-0000-000000000040");
        await empty.Locator(".tm-include-page__state").Filter(new() { HasText = "has no blocks" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureIncludeBaselineAsync("cf12-empty-source", empty);

        var nested = IncludeBlock(page, "cf120000-0000-0000-0000-000000000050");
        await nested.Locator(".tm-include-page__paragraph").Filter(new() { HasText = "CF12 deep child content" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await nested.Locator(".tm-include-page .tm-include-page__paragraph").Filter(new() { HasText = "Included source paragraph" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await nested.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Nested Include Page block should not overflow horizontally.");
        await CaptureIncludeBaselineAsync("cf12-nested-include", nested);

        var capture = await CaptureBaselineAsync("include-page", "cf12-include-page-baseline", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine($"UX CF12 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        await configurable.Locator(".tm-include-page__source-link").ClickAsync();
        await page.Locator(".tm-notion-header-title").Filter(new() { HasText = "CF12 Source Handbook" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
