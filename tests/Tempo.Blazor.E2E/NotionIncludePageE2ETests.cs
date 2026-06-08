using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionIncludePageE2ETests : NotionE2ETestBase
{
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

        var configurable = page.Locator("[data-block-id='cf120000-0000-0000-0000-000000000010'] .tm-include-page").First;
        await configurable.Locator(".tm-include-page__select").SelectOptionAsync("22222222-2222-2222-2222-222222222222");
        await configurable.Locator(".tm-include-page__paragraph").Filter(new() { HasText = "Included source paragraph" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(0, await configurable.Locator("[contenteditable='true']").CountAsync(), "Included source blocks must render read-only.");
        Assert.IsTrue(await configurable.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Configured Include Page block should not overflow horizontally.");

        var deleted = page.Locator("[data-block-id='cf120000-0000-0000-0000-000000000020'] .tm-include-page").First;
        await deleted.Locator(".tm-include-page__state").Filter(new() { HasText = "could not be found" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var cyclic = page.Locator("[data-block-id='cf120000-0000-0000-0000-000000000030'] .tm-include-page").First;
        await cyclic.Locator(".tm-include-page__state").Filter(new() { HasText = "cycle" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var empty = page.Locator("[data-block-id='cf120000-0000-0000-0000-000000000040'] .tm-include-page").First;
        await empty.Locator(".tm-include-page__state").Filter(new() { HasText = "has no blocks" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var nested = page.Locator("[data-block-id='cf120000-0000-0000-0000-000000000050'] .tm-include-page").First;
        await nested.Locator(".tm-include-page__paragraph").Filter(new() { HasText = "CF12 deep child content" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

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
