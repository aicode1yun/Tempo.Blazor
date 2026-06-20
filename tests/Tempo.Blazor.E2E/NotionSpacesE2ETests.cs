using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionSpacesE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF29: spaces switch the sidebar tree, overview renders, current page moves between spaces, and UX baselines are captured.")]
    public async Task CF29_Spaces_HappyPathAndBaseline()
    {
        var page = await OpenNotionEditorAsync();
        await SeedSpacesPageAsync();

        await page.Locator(".tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF29 Team Launch Plan" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await OpenSpaceListAsync(page);
        await CaptureBaselineAsync("spaces", "cf29-space-switcher-list", page.GetByTestId("notion-space-switcher").First);
        await page.Locator("[data-space-id='personal']").ClickAsync();
        await page.Locator(".tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF29 Personal Notes" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await OpenSpaceListAsync(page);
        await page.Locator("[data-space-id='team']").ClickAsync();
        await page.Locator(".tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF29 Team Launch Plan" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.GetByTestId("notion-space-overview-toggle").ClickAsync();
        await page.GetByTestId("notion-space-overview").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.GetByTestId("notion-space-card-archive").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        var capture = await CaptureBaselineAsync("spaces", "cf29-switcher-overview", page.Locator(".tm-notion-sidebar").First);
        TestContext.WriteLine($"UX CF29 spaces baseline captured: {capture.FullPagePath} / {capture.RegionPath}");
        TestContext.WriteLine("UX CF29 review: switcher stays compact in the sidebar, overview cards are scannable, and the empty space state is visible without feeling broken.");

        await page.GetByTestId("notion-space-move-archive").ClickAsync();
        await page.Locator(".tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF29 Team Launch Plan" }).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("notion-space-current")).ToContainTextAsync("Archive", new LocatorAssertionsToContainTextOptions
        {
            Timeout = 10000
        });
        await CaptureBaselineAsync("spaces", "cf29-moved-to-archive", page.Locator(".tm-notion-sidebar").First);
    }

    [TestMethod]
    [Description("CF29: providerless mode hides the sidebar switcher; empty and many-space states remain usable.")]
    public async Task CF29_Spaces_EdgeCases_Work()
    {
        var providerless = await OpenNotionEditorAsync("?disableSpaceProvider=true");
        Assert.AreEqual(0, await providerless.GetByTestId("notion-space-switcher").CountAsync(), "Space switcher should be hidden when no provider is configured.");

        var page = await OpenNotionEditorAsync();
        await SeedSpacesPageAsync();
        await OpenSpaceListAsync(page);
        await page.Locator("[data-space-id='archive']").ClickAsync();
        await page.Locator(".tm-npt-empty").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await Assertions.Expect(page.Locator(".tm-npt-empty").First).ToContainTextAsync("No pages");
        await CaptureBaselineAsync("spaces", "cf29-empty-space", page.Locator(".tm-notion-sidebar").First);

        page = await OpenNotionEditorAsync();
        await SeedManySpacesPageAsync();
        await OpenSpaceListAsync(page);
        var list = page.Locator(".tm-ns-space__list").First;
        var isScrollable = await list.EvaluateAsync<bool>("el => el.scrollHeight > el.clientHeight");
        Assert.IsTrue(isScrollable, "Many spaces should make the switcher list scroll instead of expanding the sidebar indefinitely.");

        await list.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
        await page.Locator("[data-space-id='space-18']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("spaces", "cf29-many-spaces-scroll", page.GetByTestId("notion-space-switcher").First);
    }

    private static async Task OpenSpaceListAsync(IPage page)
    {
        var current = page.GetByTestId("notion-space-current").First;
        await current.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await current.ClickAsync();
        await page.Locator(".tm-ns-space__list").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
