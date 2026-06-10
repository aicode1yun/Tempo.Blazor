using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionChildrenDisplayE2ETests : NotionE2ETestBase
{
    private static ILocator ChildrenBlock(IPage page, string blockId) =>
        page.Locator($"[data-block-id='{blockId}'] .tm-children").First;

    private async Task CaptureChildrenBaselineAsync(string state, ILocator region)
    {
        await region.ScrollIntoViewIfNeededAsync();
        await region.EvaluateAsync("element => element.scrollIntoView({ block: 'center', inline: 'nearest' })");
        await Page.WaitForTimeoutAsync(300);
        var capture = await CaptureBaselineAsync("children-display", state, region);
        TestContext.WriteLine($"UX CF13 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF13: Children Display renders current-page children, empty roots, depth-limited and unlimited trees, many children, captures UX baseline, and navigates to a child page.")]
    public async Task ChildrenDisplay_RenderDepthEmptyManyAndNavigate()
    {
        var page = await OpenNotionEditorAsync(1366, 1100);
        await SeedChildrenDisplayPageAsync();

        var blocks = page.Locator(".tm-children");
        await blocks.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await blocks.CountAsync() >= 5, "The seeded page should render current, empty, depth-limited, unlimited, and many-child Children Display blocks.");

        var current = ChildrenBlock(page, "cf130000-0000-0000-0000-000000000010");
        await current.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Product Space" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await current.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Deep Troubleshooting" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await current.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Current-page Children Display block should not overflow horizontally.");
        Assert.IsTrue(await current.Locator(".tm-children__page-icon").CountAsync() >= 1, "Current-page Children Display should render icons by default.");
        await CaptureChildrenBaselineAsync("cf13-current-page-children", current);

        var empty = ChildrenBlock(page, "cf130000-0000-0000-0000-000000000020");
        await empty.Locator(".tm-children__empty").Filter(new() { HasText = "has no child pages" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureChildrenBaselineAsync("cf13-empty-root", empty);

        var depthOne = ChildrenBlock(page, "cf130000-0000-0000-0000-000000000030");
        await depthOne.Locator(".tm-children__page").Filter(new() { HasText = "CF13 API Guide" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(0, await depthOne.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Deep Troubleshooting" }).CountAsync());
        await CaptureChildrenBaselineAsync("cf13-depth-limited", depthOne);

        var unlimited = ChildrenBlock(page, "cf130000-0000-0000-0000-000000000040");
        await unlimited.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Deep Troubleshooting" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureChildrenBaselineAsync("cf13-all-depths", unlimited);

        var many = ChildrenBlock(page, "cf130000-0000-0000-0000-000000000050");
        var longChildTitle = "CF13 Child 01 with a very long title that should truncate cleanly in the tree row without pushing the navigation arrow away";
        var longChild = many.Locator(".tm-children__page").Filter(new() { HasText = longChildTitle }).First;
        await longChild.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await many.Locator(".tm-children__page").CountAsync() >= 16, "Many-child state should render all direct children at depth 1.");
        Assert.AreEqual(0, await many.Locator(".tm-children__page-icon").CountAsync(), "ShowIcons=false should hide page icons.");
        Assert.AreEqual(longChildTitle, await longChild.GetAttributeAsync("title"), "Long child rows should expose the full title as a native tooltip.");
        Assert.IsTrue(await many.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Many-child Children Display block should not overflow horizontally.");
        await CaptureChildrenBaselineAsync("cf13-many-children-long-title", many.Locator(".tm-children__tree-wrap").First);

        var capture = await CaptureBaselineAsync("children-display", "cf13-children-display-baseline", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine($"UX CF13 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        await current.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Product Space" }).First.ClickAsync();
        await page.Locator(".tm-notion-header-title").Filter(new() { HasText = "CF13 Product Space" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
