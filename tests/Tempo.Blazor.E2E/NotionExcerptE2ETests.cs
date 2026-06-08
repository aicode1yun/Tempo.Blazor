using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionExcerptE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF14: Excerpt and Excerpt Include render valid source excerpts, no-excerpt and deleted-source states, capture UX baseline, and navigate to the source page.")]
    public async Task Excerpt_RenderIncludeEdgeStatesAndNavigate()
    {
        var page = await OpenNotionEditorAsync();
        await SeedExcerptPageAsync();

        var excerpt = page.Locator("[data-block-id='cf140000-0000-0000-0000-000000000010'] .tm-excerpt").First;
        await excerpt.Locator(".tm-excerpt__editor").Filter(new() { HasText = "CF14 target page reusable excerpt" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual("true", await excerpt.Locator(".tm-excerpt__editor").GetAttributeAsync("contenteditable"));

        var validInclude = page.Locator("[data-block-id='cf140000-0000-0000-0000-000000000020'] .tm-excerpt-include").First;
        await validInclude.Locator(".tm-excerpt-include__content").Filter(new() { HasText = "CF14 reusable source excerpt" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.IsTrue(await validInclude.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "Valid Excerpt Include block should not overflow horizontally.");
        Assert.AreEqual(0, await validInclude.Locator("text=This body paragraph must not be rendered").CountAsync());

        var noExcerpt = page.Locator("[data-block-id='cf140000-0000-0000-0000-000000000030'] .tm-excerpt-include").First;
        await noExcerpt.Locator(".tm-excerpt-include__state").Filter(new() { HasText = "has no excerpt" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var deleted = page.Locator("[data-block-id='cf140000-0000-0000-0000-000000000040'] .tm-excerpt-include").First;
        await deleted.Locator(".tm-excerpt-include__state").Filter(new() { HasText = "could not be found" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var capture = await CaptureBaselineAsync("excerpt", "cf14-excerpt-include-baseline", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine($"UX CF14 baseline captured: {capture.FullPagePath} / {capture.RegionPath}");

        await validInclude.Locator(".tm-excerpt-include__source-link").ClickAsync();
        await page.Locator(".tm-notion-header-title").Filter(new() { HasText = "CF14 Source With Excerpt" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
