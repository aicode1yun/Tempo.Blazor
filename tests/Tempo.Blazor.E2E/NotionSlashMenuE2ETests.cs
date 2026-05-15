using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests covering the slash menu in the Notion editor:
/// open/close, search filtering, keyboard navigation, and block insertion.
/// </summary>
[TestClass]
public class NotionSlashMenuE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    private async Task OpenSlashMenuAsync(IPage page)
    {
        var para = page.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await para.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await para.ClickAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        // Wait for new block to render and for initKeyboardHandler to run
        await page.WaitForTimeoutAsync(1000);

        // Type "/" to trigger the slash menu — must be in the focused (new) contenteditable
        await page.Keyboard.TypeAsync("/");
        await page.WaitForSelectorAsync(".tm-notion-slash",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Open / Close
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Typing / in a text block opens the slash menu")]
    public async Task SlashMenu_Slash_Opens()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var menu = page.Locator(".tm-notion-slash").First;
        Assert.IsTrue(await menu.IsVisibleAsync(), "Slash menu should be visible after typing /");
        await TakeScreenshotAsync(page, "slash_menu_open");
    }

    [TestMethod]
    [Description("Pressing Escape closes the slash menu")]
    public async Task SlashMenu_Escape_Closes()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(500);

        var menu = page.Locator(".tm-notion-slash").First;
        Assert.IsFalse(await menu.IsVisibleAsync(), "Slash menu should close after Escape");
        await TakeScreenshotAsync(page, "slash_menu_escape_close");
    }

    [TestMethod]
    [Description("Clicking outside the slash menu closes it")]
    public async Task SlashMenu_ClickOutside_Closes()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        // Click the backdrop via JS (Playwright actionability is blocked by overlay)
        await page.EvaluateAsync("() => document.querySelector('.tm-notion-slash-backdrop')?.click()");
        await page.WaitForTimeoutAsync(500);

        var menu = page.Locator(".tm-notion-slash").First;
        Assert.IsFalse(await menu.IsVisibleAsync(), "Slash menu should close after clicking outside");
        await TakeScreenshotAsync(page, "slash_menu_click_outside_close");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Search filtering
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Typing 'head' in the slash menu filters to heading items")]
    public async Task SlashMenu_SearchFilter_FiltersItems()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("head");
        await page.WaitForTimeoutAsync(400);

        var items = page.Locator(".tm-notion-slash__item");
        var count = await items.CountAsync();
        Assert.IsTrue(count > 0, "Filtered slash menu should still show some items");

        // Verify filtering actually happened (fewer items than unfiltered)
        // and that at least one heading-related item is present
        var names = await items.Locator(".tm-notion-slash__item-name").AllInnerTextsAsync();
        Assert.IsTrue(names.Any(n => n.ToLowerInvariant().Contains("head")),
            "At least one item should match 'head' filter");
        Assert.IsTrue(count < 20, $"Filtering should reduce item count. Actual: {count}");

        await TakeScreenshotAsync(page, "slash_menu_search_filter");
    }

    [TestMethod]
    [Description("Typing a non-matching query shows the empty state")]
    public async Task SlashMenu_SearchFilter_NoMatch_ShowsEmpty()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("xyznonexistent");
        await page.WaitForTimeoutAsync(400);

        var empty = page.Locator(".tm-notion-slash__empty").First;
        await empty.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await empty.IsVisibleAsync(), "Empty state should appear for non-matching query");

        var emptyText = await empty.TextContentAsync();
        StringAssert.Contains(emptyText?.ToLowerInvariant() ?? "", "no results",
            "Empty state should indicate no results");

        await TakeScreenshotAsync(page, "slash_menu_search_empty");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Keyboard navigation
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Pressing ArrowDown in the slash menu navigates to the next item")]
    public async Task SlashMenu_ArrowDown_NavigatesItems()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        // Wait for items to render
        var items = page.Locator(".tm-notion-slash__item");
        await items.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        // First item should be selected by default
        var firstSelected = items.First;
        var firstClass = await firstSelected.EvaluateAsync<string>("el => el.className");
        Assert.IsTrue(firstClass.Contains("tm-notion-slash__item--selected"),
            "First item should be selected by default");

        // Arrow down once
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForTimeoutAsync(200);

        // Second item should now be selected
        var second = items.Nth(1);
        var secondClass = await second.EvaluateAsync<string>("el => el.className");
        Assert.IsTrue(secondClass.Contains("tm-notion-slash__item--selected"),
            "Second item should be selected after ArrowDown");

        await TakeScreenshotAsync(page, "slash_menu_arrow_down");
    }

    [TestMethod]
    [Description("Pressing ArrowUp in the slash menu navigates to the previous item")]
    public async Task SlashMenu_ArrowUp_NavigatesItems()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var items = page.Locator(".tm-notion-slash__item");
        await items.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        // Navigate down twice
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForTimeoutAsync(200);
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForTimeoutAsync(200);

        var third = items.Nth(2);
        var thirdClass = await third.EvaluateAsync<string>("el => el.className");
        Assert.IsTrue(thirdClass.Contains("tm-notion-slash__item--selected"),
            "Third item should be selected after two ArrowDown presses");

        // Navigate up once
        await page.Keyboard.PressAsync("ArrowUp");
        await page.WaitForTimeoutAsync(200);

        var second = items.Nth(1);
        var secondClass = await second.EvaluateAsync<string>("el => el.className");
        Assert.IsTrue(secondClass.Contains("tm-notion-slash__item--selected"),
            "Second item should be selected after ArrowUp");

        await TakeScreenshotAsync(page, "slash_menu_arrow_up");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Block insertion
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Pressing Enter on a filtered Heading 1 item converts the block to H1")]
    public async Task SlashMenu_Enter_InsertsBlock()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("heading 1");
        await page.WaitForTimeoutAsync(400);

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(800);

        var h1 = page.Locator(".tm-notion-heading--h1").Last;
        await h1.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await h1.IsVisibleAsync(), "H1 block should be inserted after Enter on Heading 1");
        await TakeScreenshotAsync(page, "slash_menu_enter_h1");
    }

    [TestMethod]
    [Description("Clicking the 'Bulleted list' item in the slash menu inserts a bullet block")]
    public async Task SlashMenu_Click_InsertsBlock()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("bullet");
        await page.WaitForTimeoutAsync(400);

        var item = page.Locator(".tm-notion-slash__item").Filter(new() { HasText = "Bulleted list" }).First;
        await item.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await item.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var bullet = page.Locator(".tm-notion-bullet").Last;
        await bullet.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await bullet.IsVisibleAsync(), "Bullet list block should be inserted after clicking item");
        await TakeScreenshotAsync(page, "slash_menu_click_bullet");
    }

    [TestMethod]
    [Description("Selecting Table from the slash menu creates a table block")]
    public async Task SlashMenu_InsertTable_CreatesTable()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("table");
        await page.WaitForTimeoutAsync(400);

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(800);

        var table = page.Locator(".tm-notion-table").Last;
        await table.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await table.IsVisibleAsync(), "Table block should be inserted");
        await TakeScreenshotAsync(page, "slash_menu_insert_table");
    }

    [TestMethod]
    [Description("Selecting 2 Columns from the slash menu creates a column list block")]
    public async Task SlashMenu_InsertColumnList_Creates2Columns()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("2 columns");
        await page.WaitForTimeoutAsync(400);

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(800);

        var columnList = page.Locator(".tm-notion-column-list").Last;
        await columnList.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await columnList.IsVisibleAsync(), "Column list block should be inserted");

        var columns = columnList.Locator(".tm-notion-column");
        var count = await columns.CountAsync();
        Assert.IsTrue(count >= 2, $"Column list should contain at least 2 columns. Actual: {count}");

        await TakeScreenshotAsync(page, "slash_menu_insert_columns");
    }

    [TestMethod]
    [Description("Selecting Diagram from the slash menu shows a 'Create diagram' button")]
    public async Task SlashMenu_InsertDiagram_ShowsCreateButton()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("diagram");
        await page.WaitForTimeoutAsync(400);

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(800);

        var diagramBlock = page.Locator(".tm-notion-diagram-block").Last;
        await diagramBlock.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await diagramBlock.IsVisibleAsync(), "Diagram block should be inserted");

        var createBtn = diagramBlock.Locator("button").Filter(new() { HasText = "Create diagram" }).First;
        await createBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        Assert.IsTrue(await createBtn.IsVisibleAsync(), "Create diagram button should be visible");

        await TakeScreenshotAsync(page, "slash_menu_insert_diagram");
    }

    [TestMethod]
    [Description("Selecting Wireframe from the slash menu shows a 'Create wireframe' button")]
    public async Task SlashMenu_InsertWireframe_ShowsCreateButton()
    {
        var page = await OpenNotionEditorAsync();
        await OpenSlashMenuAsync(page);

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.FillAsync("wireframe");
        await page.WaitForTimeoutAsync(400);

        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(800);

        var wireframeBlock = page.Locator(".tm-notion-wireframe-block").Last;
        await wireframeBlock.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.IsTrue(await wireframeBlock.IsVisibleAsync(), "Wireframe block should be inserted");

        var createBtn = wireframeBlock.Locator("button").Filter(new() { HasText = "Create wireframe" }).First;
        await createBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        Assert.IsTrue(await createBtn.IsVisibleAsync(), "Create wireframe button should be visible");

        await TakeScreenshotAsync(page, "slash_menu_insert_wireframe");
    }
}
