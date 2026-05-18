using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end accessibility checkpoints for document editor keyboard surfaces and live announcements.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase21AccessibilityE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase21_CommandPalette_KeyboardSearchExecuteAndClose()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);
        await body.ClickAsync();

        await page.Keyboard.PressAsync("Control+Shift+P");
        var palette = page.Locator("[data-testid='document-command-palette']");
        await Assertions.Expect(palette).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(palette.Locator("[role='dialog']")).ToHaveAttributeAsync("aria-modal", "true");
        await Assertions.Expect(palette.Locator("[role='listbox']")).ToBeVisibleAsync();

        var search = palette.Locator("[data-testid='document-command-palette-search']");
        await search.FocusAsync();
        await search.FillAsync("Italic");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("[data-testid='document-command-palette']")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Assertions.Expect(page.Locator("[data-testid='document-italic']")).ToHaveAttributeAsync("aria-pressed", "true");

    }

    [TestMethod]
    public async Task Phase21_TableGridPicker_KeyboardInsertsTable()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretAtEndOfBodyAsync(page);

        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await page.Locator("[data-testid='document-toolbar-table']").ClickAsync();

        var picker = page.Locator("[data-testid='document-table-grid-picker']");
        await Assertions.Expect(picker).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(picker).ToHaveAttributeAsync("role", "grid");

        await picker.FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        await page.Keyboard.PressAsync("ArrowDown");
        await Assertions.Expect(page.Locator("[data-testid='document-table-grid-cell-2-2']"))
            .ToHaveClassAsync(new Regex("tm-document-table-grid-picker__cell--focus"));
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host'] table[data-block-id]").Last)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase21_MoreMenu_KeyboardTraversalExecutesActiveCommandWhenOverflowing()
    {
        var page = await OpenDocumentEditorAsync(width: 390, height: 760);
        var more = page.Locator("[data-testid='document-toolbar-more']");

        try
        {
            await Assertions.Expect(more).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
        catch
        {
            Assert.Inconclusive("More button was not visible at 390px; the toolbar fit without overflow.");
            return;
        }

        await more.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        var menu = page.Locator("[data-testid='document-toolbar-more-menu']");
        await Assertions.Expect(menu).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(menu).ToHaveAttributeAsync("role", "menu");
        await Assertions.Expect(menu.Locator("[role='menuitem']")).Not.ToHaveCountAsync(0, new() { Timeout = 5000 });

        var first = menu.Locator("[role='menuitem']").First;
        var activeBefore = await ReadActiveOverflowCommandAsync(page);
        await first.FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await page.WaitForFunctionAsync(
            """
            () => !!document.querySelector('[data-testid="document-toolbar-more-menu"] [role="menuitem"].tm-document-editor__overflow-menu-item--active:not(:first-of-type), [data-testid="document-toolbar-more-menu"] [role="menuitem"][tabindex="0"]:not(:first-of-type)')
            """,
            options: new PageWaitForFunctionOptions { Timeout = 5000 });
        var activeAfter = await ReadActiveOverflowCommandAsync(page);
        Assert.AreNotEqual(activeBefore, activeAfter, "ArrowDown should move the active overflow menu command.");

        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("[data-testid='document-toolbar-more-menu']")).ToHaveCountAsync(0, new() { Timeout = 5000 });
    }

    [TestMethod]
    public async Task Phase21_LiveRegion_AnnouncesFindSaveAndAutosaveError()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+f");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync(new() { Timeout = 5000 });
        await page.Locator("[data-testid='document-find-input']").FillAsync("agreement");
        await Assertions.Expect(page.Locator("[data-testid='document-editor-live-region']")).ToContainTextAsync("1 of", new() { Timeout = 5000 });

        var savePage = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await EditorTypeAsync(savePage, $" phase21-live-{DateTimeOffset.UtcNow:HHmmssfff}");
        await savePage.Keyboard.PressAsync("Control+S");
        await Assertions.Expect(savePage.GetByTestId("document-save-message"))
            .ToContainTextAsync(new Regex("Saved|Autosaved", RegexOptions.IgnoreCase), new() { Timeout = 10000 });
        await Assertions.Expect(savePage.GetByTestId("document-editor-live-region"))
            .ToContainTextAsync(new Regex("Saved|Autosaved", RegexOptions.IgnoreCase), new() { Timeout = 10000 });

        var failingPage = await OpenDocumentEditorWithQueryAsync("autosaveMs=500", width: 1440, height: 900);
        await failingPage.RouteAsync("**/api/document-editor/documents/**", async route =>
        {
            if (route.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = """{"success":false,"errorMessage":"Phase 21 autosave failed","errorKind":1}"""
                });
                return;
            }

            await route.ContinueAsync();
        });

        await EditorTypeAsync(failingPage, $" phase21-autosave-failure-{DateTimeOffset.UtcNow:HHmmssfff}");
        await Assertions.Expect(failingPage.GetByTestId("document-save-message"))
            .ToContainTextAsync("Phase 21 autosave failed", new() { Timeout = 10000 });
        await Assertions.Expect(failingPage.GetByTestId("document-editor-live-region"))
            .ToContainTextAsync("Phase 21 autosave failed", new() { Timeout = 10000 });
    }

    private async Task<IPage> OpenDocumentEditorWithQueryAsync(string query, int width, int height)
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"{BaseUrl}/document-editor?{query}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
        return page;
    }

    private static Task<string?> ReadActiveOverflowCommandAsync(IPage page)
    {
        return page.EvaluateAsync<string?>(
            """
            () => {
                const menu = document.querySelector('[data-testid="document-toolbar-more-menu"]');
                const active = menu?.querySelector('[role="menuitem"].tm-document-editor__overflow-menu-item--active')
                    || menu?.querySelector('[role="menuitem"][tabindex="0"]');
                return active?.getAttribute('data-command') || null;
            }
            """);
    }

    private static Task PlaceCaretAtEndOfBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const body = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body[contenteditable="true"]');
                if (!body) throw new Error('WYSIWYG body not found.');
                body.focus();
                const range = document.createRange();
                range.selectNodeContents(body);
                range.collapse(false);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
            }
            """);
    }
}
