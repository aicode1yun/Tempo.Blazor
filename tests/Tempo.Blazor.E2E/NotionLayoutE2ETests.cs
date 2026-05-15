using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests covering column layout blocks in the Notion editor.
/// Phase 9: ColumnList rendering, column editing, add column, max columns, divider visibility.
/// </summary>
[TestClass]
public class NotionLayoutE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        using var http = new HttpClient();
        try { await http.PostAsync("https://localhost:5100/api/notion/reset", null); }
        catch { /* API may not be running; ignore */ }

        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    /// <summary>
    /// Inserts a block via the slash menu. Types searchTerm into the slash menu
    /// search input and clicks the first matching item.
    /// </summary>
    private async Task InsertBlockViaSlashMenuAsync(IPage page, string searchTerm)
    {
        // Use the last paragraph on the page so we insert at the end
        var paras = page.Locator(".tm-notion-paragraph[contenteditable='true']");
        var count = await paras.CountAsync();
        var para = count > 0 ? paras.Last : paras.First;
        await para.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await para.ClickAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(1200);

        await page.Keyboard.TypeAsync("/");
        await page.WaitForSelectorAsync(".tm-notion-slash",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });

        var searchInput = page.Locator(".tm-notion-slash__input");
        await searchInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await searchInput.FillAsync(searchTerm);
        await page.WaitForTimeoutAsync(600);

        var firstItem = page.Locator(".tm-notion-slash__item").First;
        await firstItem.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await firstItem.ClickAsync();
        await page.WaitForTimeoutAsync(1500);
    }

    private ILocator ColumnListBlock(IPage page) =>
        page.Locator("[data-block-type='ColumnList']").First;

    private ILocator Columns(IPage page) =>
        page.Locator(".tm-notion-column");

    // ══════════════════════════════════════════════════════════════════════════
    //  Insert & render
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Inserting '2 Columns' via slash menu creates a column list with 2 columns")]
    public async Task ColumnList_Insert_Creates2Columns()
    {
        var page = await OpenNotionEditorAsync();

        // Insert a new ColumnList block via slash menu
        await InsertBlockViaSlashMenuAsync(page, "2 col");
        await page.WaitForTimeoutAsync(1500);

        // The newly inserted ColumnList should have 2 columns
        var colList = page.Locator(".tm-notion-column-list").Last;
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var cols = colList.Locator(".tm-notion-column");
        var count = await cols.CountAsync();
        Assert.AreEqual(2, count, "Inserted ColumnList should have 2 columns");

        await TakeScreenshotAsync(page, "column_list_insert");
    }

    [TestMethod]
    [Description("The default demo ColumnList renders with 2 visible columns")]
    public async Task ColumnList_Default_Renders2Columns()
    {
        var page = await OpenNotionEditorAsync();

        var colList = ColumnListBlock(page);
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var cols = Columns(page);
        var count = await cols.CountAsync();
        Assert.IsTrue(count >= 2, "Demo page should have at least 2 columns in the ColumnList block");

        // Verify columns have correct data-col-index attributes
        var col0 = page.Locator(".tm-notion-column[data-col-index='0']").First;
        var col1 = page.Locator(".tm-notion-column[data-col-index='1']").First;
        Assert.IsTrue(await col0.IsVisibleAsync(), "Column 0 should be visible");
        Assert.IsTrue(await col1.IsVisibleAsync(), "Column 1 should be visible");

        await TakeScreenshotAsync(page, "column_list_default");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Content editing inside columns
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Typing text into a paragraph inside column 1 updates the DOM")]
    public async Task ColumnList_Column1_TypesContent()
    {
        var page = await OpenNotionEditorAsync();

        var colList = ColumnListBlock(page);
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Find the first paragraph inside column 0
        var col0 = page.Locator(".tm-notion-column[data-col-index='0']");
        var para = col0.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await para.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await para.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var testText = "Col1 E2E";
        await page.Keyboard.TypeAsync(testText);
        await page.WaitForTimeoutAsync(300);

        // Blur to save
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(500);

        var text = await para.InnerTextAsync();
        StringAssert.Contains(text, testText, "Column 1 paragraph should contain the typed text");

        await TakeScreenshotAsync(page, "column_list_col1_type");
    }

    [TestMethod]
    [Description("Typing text into a paragraph inside column 2 updates the DOM")]
    public async Task ColumnList_Column2_TypesContent()
    {
        var page = await OpenNotionEditorAsync();

        var colList = ColumnListBlock(page);
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Find the first paragraph inside column 1
        var col1 = page.Locator(".tm-notion-column[data-col-index='1']");
        var para = col1.Locator(".tm-notion-paragraph[contenteditable='true']").First;
        await para.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await para.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        var testText = "Col2 E2E";
        await page.Keyboard.TypeAsync(testText);
        await page.WaitForTimeoutAsync(300);

        // Blur to save
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(500);

        var text = await para.InnerTextAsync();
        StringAssert.Contains(text, testText, "Column 2 paragraph should contain the typed text");

        await TakeScreenshotAsync(page, "column_list_col2_type");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Add column & max columns
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking the add-column button inserts a third column")]
    public async Task ColumnList_AddColumn_Button_AddsColumn()
    {
        var page = await OpenNotionEditorAsync();

        var colList = ColumnListBlock(page);
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var colsBefore = await Columns(page).CountAsync();
        Assert.IsTrue(colsBefore >= 2, "Should have at least 2 columns before adding");

        // Click the add-column button inside the ColumnList
        var addBtn = colList.Locator(".tm-notion-column-list__add-col").First;
        await addBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await addBtn.ClickAsync();
        await page.WaitForTimeoutAsync(1200);

        var colsAfter = await Columns(page).CountAsync();
        Assert.AreEqual(colsBefore + 1, colsAfter, "Column count should increase by 1 after clicking add column");

        await TakeScreenshotAsync(page, "column_list_add_column");
    }

    [TestMethod]
    [Description("Adding columns up to MaxColumns (5) hides the add-column button")]
    public async Task ColumnList_MaxColumns_HidesAddButton()
    {
        var page = await OpenNotionEditorAsync();

        var colList = ColumnListBlock(page);
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Repeatedly click add-column until button disappears or we reach 5 columns
        for (var i = 0; i < 10; i++)
        {
            var addBtn = colList.Locator(".tm-notion-column-list__add-col");
            var btnCount = await addBtn.CountAsync();
            if (btnCount == 0) break;

            var colsCount = await Columns(page).CountAsync();
            if (colsCount >= 5) break;

            await addBtn.First.ClickAsync();
            await page.WaitForTimeoutAsync(1200);
        }

        // Should have 5 columns now
        var finalCols = await Columns(page).CountAsync();
        Assert.AreEqual(5, finalCols, "Should reach 5 columns (MaxColumns)");

        // Add button should be hidden
        var addBtnAfter = colList.Locator(".tm-notion-column-list__add-col");
        var btnVisible = await addBtnAfter.CountAsync();
        Assert.AreEqual(0, btnVisible, "Add-column button should be hidden when MaxColumns is reached");

        await TakeScreenshotAsync(page, "column_list_max_columns");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Divider visibility
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("A vertical divider is visible between the two columns")]
    public async Task ColumnList_Divider_Visible()
    {
        var page = await OpenNotionEditorAsync();

        var colList = ColumnListBlock(page);
        await colList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var dividers = colList.Locator(".tm-notion-column-list__divider");
        var dividerCount = await dividers.CountAsync();
        Assert.IsTrue(dividerCount >= 1, "There should be at least 1 divider between columns");

        var firstDivider = dividers.First;
        Assert.IsTrue(await firstDivider.IsVisibleAsync(), "Divider should be visible");

        // Verify the divider grip element is present
        var grip = firstDivider.Locator(".tm-notion-column-list__divider-grip").First;
        Assert.IsTrue(await grip.IsVisibleAsync(), "Divider grip should be visible");

        await TakeScreenshotAsync(page, "column_list_divider");
    }
}
