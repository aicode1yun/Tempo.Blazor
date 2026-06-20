using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionLabelsE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Page labels can be added, removed, completed from existing labels, and used to filter matching pages.")]
    public async Task Labels_AddRemoveAutocompleteAndFilterPages()
    {
        var page = await OpenNotionEditorAsync();
        await SeedLabelsPageAsync();

        var labels = page.Locator(".tm-notion-labels").First;
        await labels.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        Assert.IsTrue(await labels.Locator(".tm-notion-labels__chip").CountAsync() >= 8, "The seeded labels should exercise wrapping in the page header.");
        Assert.IsTrue(await labels.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "The label editor should not overflow horizontally.");
        await CaptureBaselineAndAssertAsync(page, "cf6-labels-header-wrapping", labels);

        var input = labels.Locator(".tm-notion-labels__input");
        await input.FillAsync("  zákaznický portál  ");
        await labels.Locator(".tm-notion-labels__add").ClickAsync();
        await labels.Locator(".tm-notion-labels__chip").Filter(new() { HasText = "zákaznický portál" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var releaseChips = labels.Locator(".tm-notion-labels__chip").Filter(new() { HasText = "release" });
        var releaseCountBefore = await releaseChips.CountAsync();
        await input.FillAsync("RELEASE");
        await labels.Locator(".tm-notion-labels__add").ClickAsync();
        await page.WaitForTimeoutAsync(250);
        Assert.AreEqual(releaseCountBefore, await releaseChips.CountAsync(), "Duplicate labels should be ignored case-insensitively.");

        await input.FillAsync("team");
        var suggestion = labels.Locator(".tm-notion-labels__suggestion").Filter(new() { HasText = "team notes" }).First;
        await suggestion.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await CaptureBaselineAndAssertAsync(page, "cf6-label-editor-suggestions", labels);
        await suggestion.ClickAsync();
        await labels.Locator(".tm-notion-labels__chip").Filter(new() { HasText = "team notes" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        var opsChip = labels.Locator(".tm-notion-labels__chip").Filter(new() { HasText = "ops" }).First;
        await opsChip.Locator(".tm-notion-labels__remove").ClickAsync();
        await page.WaitForTimeoutAsync(250);
        Assert.AreEqual(0, await labels.Locator(".tm-notion-labels__chip").Filter(new() { HasText = "ops" }).CountAsync());

        await TakeScreenshotAsync(page, "notion_labels_editor_header");
        await CaptureBaselineAndAssertAsync(page, "cf6-labels-editor-header", labels);

        await labels.Locator(".tm-notion-labels__chip").Filter(new() { HasText = "release" }).First
            .Locator(".tm-notion-labels__chip-filter")
            .ClickAsync();
        var filter = labels.Locator(".tm-notion-labels__filter");
        await filter.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await filter.Locator(".tm-notion-labels__filter-page").Filter(new() { HasText = "CF6 Labels Baseline" }).IsVisibleAsync());
        await CaptureBaselineAndAssertAsync(page, "cf6-label-filter-dropdown", labels);
        var companion = filter.Locator(".tm-notion-labels__filter-page").Filter(new() { HasText = "CF6 Release Companion" }).First;
        await companion.ClickAsync();
        await page.Locator(".tm-notion-header-title").Filter(new() { HasText = "CF6 Release Companion" }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("A page without labels shows the localized empty state while the header layout remains stable.")]
    public async Task Labels_EmptyPage_ShowsEmptyState()
    {
        var page = await OpenNotionEditorAsync();
        await SeedLabelsPageAsync();

        var emptyPage = page.Locator(".tm-notion-sidebar").Locator("button, a").Filter(new() { HasText = "CF6 Empty Labels" }).First;
        await emptyPage.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emptyPage.ClickAsync();

        var labels = page.Locator(".tm-notion-labels").First;
        await labels.Locator(".tm-notion-labels__empty").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(0, await labels.Locator(".tm-notion-labels__chip").CountAsync());
        Assert.IsTrue(await labels.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth + 1"), "The empty label state should not overflow the page header.");

        await TakeScreenshotAsync(page, "notion_labels_empty_header");
        await CaptureBaselineAndAssertAsync(page, "cf6-empty-labels-header", labels);
    }

    private async Task CaptureBaselineAndAssertAsync(IPage page, string state, ILocator region)
    {
        await region.ScrollIntoViewIfNeededAsync();
        var capture = await CaptureBaselineAsync("labels", state, region);
        Assert.IsTrue(File.Exists(capture.FullPagePath), $"CF6 full-page baseline should be written for {state}.");
        Assert.IsTrue(File.Exists(capture.RegionPath), $"CF6 region baseline should be written for {state}.");
    }
}
