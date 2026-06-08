using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class NotionWorkItemsE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [Description("Inserts a work item by ID through the slash menu and switches it to inline chip mode.")]
    public async Task WorkItem_InsertById_RendersCardAndInlineChip()
    {
        var page = await OpenNotionEditorAsync();
        await SeedEmptyPageAsync();

        await InsertWorkItemBlockViaSlashAsync(page);

        var providerSelect = page.Locator(".tm-work-item-picker__select").First;
        await providerSelect.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        var optionCount = await providerSelect.Locator("option").CountAsync();
        Assert.IsTrue(optionCount > 1, "Multiple work-item providers should show the provider selector.");
        await providerSelect.SelectOptionAsync("demo");

        var input = page.Locator(".tm-work-item-picker__input").First;
        await input.FillAsync("DEMO-101");
        await page.Locator(".tm-work-item-picker__button").First.ClickAsync();
        await page.Locator(".tm-work-item-picker__result").Filter(new() { HasText = "DEMO-101" }).First.ClickAsync();

        var card = page.Locator(".tm-work-item--card[data-work-item-id='DEMO-101']").First;
        await card.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        StringAssert.Contains(await card.Locator(".tm-work-item__title").TextContentAsync(), "Prepare release checklist");
        StringAssert.Contains(await card.Locator(".tm-work-item__status").TextContentAsync(), "To Do");
        Assert.AreEqual("https://tracker.demo.local/work/DEMO-101", await card.Locator(".tm-work-item__link").GetAttributeAsync("href"));

        await card.Locator(".tm-work-item__mode").Filter(new() { HasText = "Inline" }).ClickAsync();
        var inline = page.Locator(".tm-work-item--inline[data-work-item-id='DEMO-101']").First;
        await inline.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.AreEqual("DEMO-101", await inline.Locator(".tm-work-item__chip-id").TextContentAsync());
        var iconBox = await inline.Locator(".tm-work-item__type-icon").BoundingBoxAsync();
        Assert.IsNotNull(iconBox);
        Assert.IsTrue(iconBox.Height <= 32, "Inline type icon should stay chip-sized.");

        await TakeScreenshotAsync(page, "notion_work_item_insert_inline");
    }

    [TestMethod]
    [Description("Seeded work items cover card, list, inline, missing ID, and cached provider outage states.")]
    public async Task WorkItem_EdgeStates_RenderErrorsAndCachedFallback()
    {
        var page = await OpenNotionEditorAsync();
        await SeedWorkItemsPageAsync();

        Assert.IsTrue(await page.Locator("[data-block-id='cf500000-0000-0000-0000-000000000010'] .tm-work-item--card").IsVisibleAsync());
        Assert.IsTrue(await page.Locator("[data-block-id='cf500000-0000-0000-0000-000000000020'] .tm-work-item--list").IsVisibleAsync());
        Assert.IsTrue(await page.Locator("[data-block-id='cf500000-0000-0000-0000-000000000030'] .tm-work-item--inline").IsVisibleAsync());

        Assert.IsTrue(await page.Locator("[data-block-id='cf500000-0000-0000-0000-000000000040'] .tm-work-item__empty-error").IsVisibleAsync());
        Assert.IsTrue(await page.Locator("[data-block-id='cf500000-0000-0000-0000-000000000050'] .tm-work-item__fallback").IsVisibleAsync());
        StringAssert.Contains(
            await page.Locator("[data-block-id='cf500000-0000-0000-0000-000000000050'] .tm-work-item__title").TextContentAsync(),
            "Cached fallback survives provider outage");

        await TakeScreenshotAsync(page, "notion_work_item_card_list_inline_error");
    }

    [TestMethod]
    [Description("When no work-item provider is available, the slash menu does not offer the feature.")]
    public async Task WorkItem_NoProvider_HidesSlashFeature()
    {
        var page = await OpenNotionEditorAsync("?disableWorkItemProvider=true");
        await SeedEmptyPageAsync();

        var editor = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await editor.ClickAsync();
        await page.Keyboard.TypeAsync("/");
        await page.WaitForSelectorAsync(".tm-notion-slash", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        await page.Locator(".tm-notion-slash__input").FillAsync("work item");
        await page.WaitForTimeoutAsync(300);

        Assert.AreEqual(0, await page.Locator(".tm-notion-slash__item-name").Filter(new() { HasText = "Work item" }).CountAsync());
    }

    private static async Task InsertWorkItemBlockViaSlashAsync(IPage page)
    {
        var paragraph = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await paragraph.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await paragraph.ClickAsync();
        await page.Keyboard.TypeAsync("/");
        await page.WaitForSelectorAsync(".tm-notion-slash", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        await page.Locator(".tm-notion-slash__input").FillAsync("work item");
        var item = page.Locator(".tm-notion-slash__item").Filter(new() { HasText = "Work item" }).First;
        await item.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await item.ClickAsync();
        await page.Locator(".tm-work-item-picker").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }
}
