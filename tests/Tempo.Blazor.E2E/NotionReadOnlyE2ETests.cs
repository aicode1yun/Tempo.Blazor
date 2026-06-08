using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E backfill coverage for the Notion editor read-only mode.
/// </summary>
[TestClass]
public sealed class NotionReadOnlyE2ETests : NotionE2ETestBase
{
    private const string MutationProbeText = "EB18_READONLY_MUTATION_PROBE";

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("EB18: read-only mode hides edit controls, blocks mutation, allows text selection/copy, and captures the UX baseline")]
    public async Task EB18_ReadOnly_RichPage_HidesEditControlsBlocksMutationAndAllowsCopy()
    {
        await SeedDatabaseAsync("many");
        var page = await OpenNotionEditorAsync("?readonly=true");
        await SeedRichPageAsync();

        await AssertReadOnlySurfaceAsync(page, "rich");

        var editable = page.Locator(".tm-notion-page .tm-notion-editable").First;
        await editable.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var beforeText = await editable.InnerTextAsync();
        await editable.ClickAsync();
        await page.Keyboard.TypeAsync(MutationProbeText);
        await page.WaitForTimeoutAsync(300);

        var afterText = await editable.InnerTextAsync();
        Assert.AreEqual(beforeText, afterText, "Typing into a read-only text block must not mutate visible content.");
        Assert.AreEqual(0, await page.Locator($".tm-notion-page :text('{MutationProbeText}')").CountAsync(),
            "The mutation probe text must not appear anywhere inside the read-only page.");

        await editable.ClickAsync();
        await page.Keyboard.TypeAsync("/");
        await page.WaitForTimeoutAsync(300);
        Assert.AreEqual(0, await page.Locator(".tm-notion-slash-menu:visible").CountAsync(),
            "Typing '/' in read-only mode must not open the slash command menu.");

        await AssertSelectionAndClipboardCopyAsync(page, editable, beforeText);

        var capture = await CaptureBaselineAsync("read-only", "eb18-rich-page", page.Locator(".tm-notion-page").First);
        Assert.IsTrue(File.Exists(capture.FullPagePath), "Read-only full-page UX baseline should be written.");
        Assert.IsTrue(File.Exists(capture.RegionPath), "Read-only page-region UX baseline should be written.");
    }

    [TestMethod]
    [Description("EB18: read-only mode is clean across seeded text, list, media, table, layout, special, drag/drop and database block surfaces")]
    public async Task EB18_ReadOnly_AllSeededBlockSurfaces_DoNotExposeEditControls()
    {
        await SeedDatabaseAsync("many");
        var page = await OpenNotionEditorAsync("?readonly=true");

        var seedCases = new (string Name, Func<Task> Seed)[]
        {
            ("rich", SeedRichPageAsync),
            ("text-formatting", SeedTextFormattingPageAsync),
            ("lists", SeedListTodoPageAsync),
            ("media", SeedMediaPageAsync),
            ("tables", SeedTablePageAsync),
            ("layout", SeedLayoutPageAsync),
            ("special-blocks", SeedSpecialBlocksPageAsync),
            ("drag-drop", SeedDragDropPageAsync)
        };

        foreach (var seedCase in seedCases)
        {
            await seedCase.Seed();
            await AssertReadOnlySurfaceAsync(page, seedCase.Name);
        }
    }

    private static async Task AssertReadOnlySurfaceAsync(IPage page, string seedName)
    {
        await page.Locator(".tm-notion-editor--locked").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        await page.Locator(".tm-notion-page--readonly").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });

        Assert.AreEqual(0, await page.Locator(".tm-notion-page [contenteditable='true']").CountAsync(),
            $"{seedName}: read-only page must not expose editable contenteditable regions.");

        var forbiddenVisibleControls = new[]
        {
            ".tm-notion-handle",
            ".tm-notion-block-list__after",
            ".tm-notion-slash-menu",
            ".tm-notion-mention-menu",
            ".tm-notion-token-dropdown",
            ".tm-notion-inline-toolbar",
            ".tm-notion-table__handle-cell",
            ".tm-notion-table__row-delete",
            ".tm-notion-table__col-delete",
            ".tm-notion-table-block__add-row",
            ".tm-notion-table-block__add-col",
            ".tm-notion-column-list__add-col",
            ".tm-notion-media-upload-zone",
            ".tm-template-btn__config-toggle",
            ".tm-template-btn__config",
            ".tm-dbt__add-row-btn",
            ".tm-dbt__add-field",
            ".tm-dbt__resize-handle",
            ".tm-dbb__add-card",
            ".tm-dbb__empty-add",
            ".tm-dbg__add-card",
            ".tm-dblv__add-row",
            ".tm-dbfe__add-option",
            ".tm-dbfb__add-btn",
            ".tm-dbsb__add-btn",
            ".tm-dbie__upload-zone"
        };

        foreach (var selector in forbiddenVisibleControls)
        {
            var visibleCount = await page.Locator($"{selector}:visible").CountAsync();
            Assert.AreEqual(0, visibleCount, $"{seedName}: selector '{selector}' must not be visible in read-only mode.");
        }
    }

    private async Task AssertSelectionAndClipboardCopyAsync(IPage page, ILocator locator, string expectedText)
    {
        await page.Context.GrantPermissionsAsync(
            ["clipboard-read", "clipboard-write"],
            new BrowserContextGrantPermissionsOptions { Origin = BaseUrl });

        await SelectLocatorContentsAsync(page, locator);
        var selectedText = await page.EvaluateAsync<string>("() => window.getSelection()?.toString() ?? ''");
        AssertSelectedText(expectedText, selectedText, "Selected text should contain the read-only block content.");

        await page.Keyboard.PressAsync("Control+C");
        var clipboardText = await page.EvaluateAsync<string>("async () => await navigator.clipboard.readText()");
        AssertSelectedText(expectedText, clipboardText, "Clipboard text should contain the copied read-only block content.");
    }

    private static void AssertSelectedText(string expectedText, string actualText, string message)
    {
        var expectedWords = expectedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 4)
            .Take(3)
            .ToArray();

        Assert.IsTrue(expectedWords.Length > 0, "The selected read-only block should have meaningful text.");
        foreach (var word in expectedWords)
        {
            StringAssert.Contains(actualText, word, message);
        }
    }
}
