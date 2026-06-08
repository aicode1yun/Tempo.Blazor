using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionBulkPagesE2ETests : NotionE2ETestBase
{
    private const string SourceRootId = "11111111-1111-1111-1111-111111111111";
    private const string ChildAId = "22222222-2222-2222-2222-222222222222";
    private const string DeleteAId = "55555555-5555-5555-5555-555555555555";
    private const string DeleteBId = "66666666-6666-6666-6666-666666666666";

    [TestMethod]
    [Description("CF24: bulk page selection supports guarded move, deep copy, descendant delete confirmation, and baseline toolbar capture")]
    public async Task CF24_BulkPageOperations_WorkThroughSidebar()
    {
        var page = await OpenNotionEditorAsync();
        await SeedBulkPagesAsync();

        await SelectPageByIdAsync(page, DeleteAId);
        await SelectPageByIdAsync(page, DeleteBId);
        await page.Locator("[data-testid='notion-page-bulk-toolbar']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await CaptureBaselineAsync("page-bulk-pages", "cf24-multi-select-toolbar", page.Locator(".tm-notion-sidebar").First);

        await page.Locator("[data-testid='notion-page-bulk-delete']").ClickAsync();
        await page.Locator("[data-testid='notion-page-bulk-delete-confirm-button']").ClickAsync();
        await ExpectTitleCountAsync(page, "CF24 Delete Candidate A", 0);
        await ExpectTitleCountAsync(page, "CF24 Delete Candidate B", 0);

        await ExpandPageTreeItemAsync(page, "CF24 Source Root");
        await ExpandPageTreeItemAsync(page, "CF24 Child A");

        await SelectPageByIdAsync(page, SourceRootId);
        await page.Locator("[data-testid='notion-page-bulk-move']").ClickAsync();
        await page.Locator("[data-testid='notion-page-bulk-target-search']").FillAsync("Grandchild A1");
        await page.Locator("[data-testid='notion-page-bulk-target-page']").First.ClickAsync();
        await page.WaitForTimeoutAsync(800);
        Assert.AreEqual(1, await page.Locator(".tm-npt-root > .tm-npt-item > .tm-npt-row .tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF24 Source Root" }).CountAsync());
        if (await page.Locator("[data-testid='notion-page-bulk-error']").CountAsync() > 0)
        {
            StringAssert.Contains(await page.Locator("[data-testid='notion-page-bulk-error']").InnerTextAsync(), "descendants");
        }
        await page.Locator("[data-testid='notion-page-bulk-clear']").ClickAsync();

        await SelectPageByIdAsync(page, SourceRootId);
        await page.Locator("[data-testid='notion-page-bulk-copy']").ClickAsync();
        await page.Locator("[data-testid='notion-page-bulk-target-search']").FillAsync("Target Space");
        await page.Locator("[data-testid='notion-page-bulk-target-page']").First.ClickAsync();
        await ExpandPageTreeItemAsync(page, "CF24 Target Space");
        await page.Locator(".tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF24 Source Root (Copy)" }).First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await ExpandPageTreeItemAsync(page, "CF24 Source Root (Copy)");
        var copiedRootItem = PageTreeItemByTitle(page, "CF24 Source Root (Copy)");
        var copiedChildItem = copiedRootItem
            .Locator(".tm-npt-title[title='CF24 Child A']")
            .First
            .Locator("xpath=ancestor::li[contains(concat(' ', normalize-space(@class), ' '), ' tm-npt-item ')][1]");
        await copiedChildItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        if (await copiedChildItem.GetAttributeAsync("aria-expanded") != "true")
        {
            await copiedChildItem.Locator("xpath=./div[contains(@class,'tm-npt-row')]//button[contains(@class,'tm-npt-toggle')]").First.ClickAsync();
        }
        await copiedRootItem.Locator(".tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF24 Grandchild A1" }).First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        await SelectPageByTitleAsync(page, "CF24 Source Root (Copy)");
        await page.Locator("[data-testid='notion-page-bulk-delete']").ClickAsync();
        await page.Locator("[data-testid='notion-page-bulk-delete-confirm-button']").ClickAsync();
        await ExpectTitleCountAsync(page, "CF24 Source Root (Copy)", 0);

        await ExpandPageTreeItemAsync(page, "CF24 Source Root");
        await SelectPageByIdAsync(page, ChildAId);
        await page.Locator("[data-testid='notion-page-bulk-move']").ClickAsync();
        await page.Locator("[data-testid='notion-page-bulk-target-root']").ClickAsync();
        await PageTreeItemByTitle(page, "CF24 Child A").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        Assert.AreEqual(1, await page.Locator(".tm-npt-root > .tm-npt-item > .tm-npt-row .tm-npt-title").Filter(new LocatorFilterOptions { HasText = "CF24 Child A" }).CountAsync());

        await CaptureBaselineAsync("page-bulk-pages", "cf24-after-bulk-operations", page.Locator(".tm-notion-sidebar").First);
    }

    private static async Task SelectPageByIdAsync(IPage page, string pageId)
    {
        var checkbox = page.Locator($"[data-testid='notion-page-select-{pageId}']").First;
        await checkbox.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 10000 });
        await checkbox.CheckAsync(new LocatorCheckOptions { Force = true });
    }

    private static async Task SelectPageByTitleAsync(IPage page, string title)
    {
        var item = PageTreeItemByTitle(page, title);
        await item.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await item.Locator(".tm-npt-select").First.CheckAsync(new LocatorCheckOptions { Force = true });
    }

    private static async Task ExpandPageTreeItemAsync(IPage page, string title)
    {
        var item = PageTreeItemByTitle(page, title);
        await item.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        if (await item.GetAttributeAsync("aria-expanded") != "true")
        {
            await item.Locator("xpath=./div[contains(@class,'tm-npt-row')]//button[contains(@class,'tm-npt-toggle')]").First.ClickAsync();
        }
    }

    private static async Task ExpectTitleCountAsync(IPage page, string title, int expectedCount)
    {
        await page.WaitForFunctionAsync(
            "args => Array.from(document.querySelectorAll('.tm-npt-title')).filter(el => el.textContent && el.textContent.includes(args.title)).length === args.expectedCount",
            new { title, expectedCount },
            new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    private static ILocator PageTreeItemByTitle(IPage page, string title)
        => page.Locator($".tm-npt-title[title='{title}']")
            .First
            .Locator("xpath=ancestor::li[contains(concat(' ', normalize-space(@class), ' '), ' tm-npt-item ')][1]");
}
