using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionChildrenDisplayE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF13: Children Display renders current-page children, empty roots, depth-limited and unlimited trees, many children, captures UX baseline, and navigates to a child page.")]
    public async Task ChildrenDisplay_RenderDepthEmptyManyAndNavigate()
    {
        var page = await OpenNotionEditorAsync();
        await SeedChildrenDisplayPageAsync();

        var blocks = page.Locator(".tm-children");
        await blocks.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await blocks.CountAsync() >= 5, "The seeded page should render current, empty, depth-limited, unlimited, and many-child Children Display blocks.");

        var current = page.Locator("[data-block-id='cf130000-0000-0000-0000-000000000010'] .tm-children").First;
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

        var empty = page.Locator("[data-block-id='cf130000-0000-0000-0000-000000000020'] .tm-children").First;
        await empty.Locator(".tm-children__empty").Filter(new() { HasText = "has no child pages" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var depthOne = page.Locator("[data-block-id='cf130000-0000-0000-0000-000000000030'] .tm-children").First;
        await depthOne.Locator(".tm-children__page").Filter(new() { HasText = "CF13 API Guide" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(0, await depthOne.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Deep Troubleshooting" }).CountAsync());

        var unlimited = page.Locator("[data-block-id='cf130000-0000-0000-0000-000000000040'] .tm-children").First;
        await unlimited.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Deep Troubleshooting" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var many = page.Locator("[data-block-id='cf130000-0000-0000-0000-000000000050'] .tm-children").First;
        await many.Locator(".tm-children__page").Filter(new() { HasText = "CF13 Child 14" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await many.Locator(".tm-children__page").CountAsync() >= 16, "Many-child state should render all direct children at depth 1.");
        Assert.AreEqual(0, await many.Locator(".tm-children__page-icon").CountAsync(), "ShowIcons=false should hide page icons.");

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
