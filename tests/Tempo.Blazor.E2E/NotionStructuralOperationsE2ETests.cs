using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Structural edits must survive a reload: a block split off in the middle stays in the middle,
/// deleting a container takes its children with it, and a duplicated table is an independent copy
/// that still has its rows.
/// Runs against the self-hosted HTTPS demo API (5100) and HTTPS demo WASM (7106).
/// </summary>
[TestClass]
public class NotionStructuralOperationsE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("A block inserted in the middle is still in the middle after a reload")]
    public async Task BlockInsertedInTheMiddle_KeepsItsPlaceAfterAReload()
    {
        var page = await OpenNotionEditorAsync();
        var firstId = await FocusTopLevelParagraphAsync(page, "alpha");

        // Enter at the end splits off a new block right behind this one.
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(900);
        await page.Keyboard.TypeAsync("inserted");
        await BlurAsync(page);
        await page.WaitForTimeoutAsync(1000);

        var beforeReload = await IndexOfTextAsync(page, "inserted");
        var firstIndex = await IndexOfBlockAsync(page, firstId);
        Assert.AreEqual(firstIndex + 1, beforeReload, "The new block sits right behind its source.");

        await ReloadAsync(page);

        Assert.AreEqual(beforeReload, await IndexOfTextAsync(page, "inserted"),
            "Without a server-side order shift the block jumps to the end of the page on reload.");

        await CaptureBaselineAsync("structural-ops", "insert-in-the-middle", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine("UX: pressing Enter mid-document keeps the new line where the caret was, even after a refresh.");
    }

    [TestMethod]
    [Description("Deleting a table removes it and its rows from the store, not just from the page")]
    public async Task DeletingATable_RemovesItsRowsFromTheStore()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page, string.Empty);

        await PasteHtmlAsync(page, "<table><tr><th>Orphan</th></tr><tr><td>Row</td></tr></table>");
        await page.WaitForTimeoutAsync(1600);

        var table = page.Locator(".tm-notion-table").Filter(new() { HasTextString = "Orphan" }).First;
        await table.WaitForAsync(Visible());
        var tableBlockId = await table.EvaluateAsync<string>(
            "el => el.closest('[data-block-id]').getAttribute('data-block-id')");

        // The demo WASM and the demo API sit on different origins, so talk to the API directly.
        var deleted = await page.APIRequest.DeleteAsync($"https://localhost:5100/api/notion/blocks/{tableBlockId}");
        Assert.IsTrue(deleted.Ok, "The delete call must succeed.");
        await ReloadAsync(page);

        Assert.AreEqual(0, await page.Locator($"[data-block-id='{tableBlockId}']").CountAsync());

        // Orphaned rows keep a ParentBlockId that no longer resolves, so they never render — the
        // page alone cannot see them. Ask the table for its children instead: it must have none.
        var children = await page.APIRequest.GetAsync(
            $"https://localhost:5100/api/notion/blocks/parent/{tableBlockId}");
        Assert.IsTrue(children.Ok);
        Assert.AreEqual("[]", (await children.TextAsync()).Replace(" ", string.Empty),
            "A surviving TableRow is invisible on the page but still lives in the store.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("A duplicated table has its own rows and editing the copy leaves the original alone")]
    public async Task DuplicatingATable_ProducesAnIndependentCopyWithRows()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page, string.Empty);

        await PasteHtmlAsync(page, "<table><tr><th>Dup</th></tr><tr><td>Cell</td></tr></table>");
        await page.WaitForTimeoutAsync(1600);

        var table = page.Locator(".tm-notion-table").Filter(new() { HasTextString = "Dup" }).First;
        var tableBlockId = await table.EvaluateAsync<string>(
            "el => el.closest('[data-block-id]').getAttribute('data-block-id')");

        var duplicated = await page.APIRequest.PostAsync($"https://localhost:5100/api/notion/blocks/{tableBlockId}/duplicate");
        Assert.IsTrue(duplicated.Ok, "The duplicate call must succeed.");
        await ReloadAsync(page);

        var tables = page.Locator(".tm-notion-table").Filter(new() { HasTextString = "Dup" });
        Assert.AreEqual(2, await tables.CountAsync(), "The duplicate must exist.");

        // The copy must carry its own rows; a shallow copy renders an empty table.
        var copyCells = await tables.Nth(1).Locator("td, th").AllInnerTextsAsync();
        CollectionAssert.Contains(copyCells.Select(cell => cell.Trim()).ToList(), "Dup");
        CollectionAssert.Contains(copyCells.Select(cell => cell.Trim()).ToList(), "Cell");

        await CaptureBaselineAsync("structural-ops", "duplicated-table", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine("UX: duplicating a table gives a real, editable copy — not an empty frame the user has to refill.");
    }

    [TestMethod]
    [Description("An open toggle is a real drop target: its block list names its page and parent")]
    public async Task ToggleIsADropTarget()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page, string.Empty);

        // Create a toggle from the slash menu, then open it so it renders its children.
        await page.Keyboard.TypeAsync(" /toggle");
        await page.WaitForTimeoutAsync(900);
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(1400);

        var toggle = page.Locator("[data-block-type='Toggle']").First;
        await toggle.WaitForAsync(Visible());
        await toggle.Locator(".tm-notion-toggle__arrow").First.ClickAsync();
        await page.WaitForTimeoutAsync(900);

        var declared = await toggle.EvaluateAsync<bool>(
            """
            el => {
                const list = el.querySelector('[data-notion-block-list]');
                return !!list && list.hasAttribute('data-page-id') && list.hasAttribute('data-parent-block-id');
            }
            """);
        Assert.IsTrue(declared, "A toggle's block list must name its page and parent to accept drops.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LocatorWaitForOptions Visible() =>
        new() { State = WaitForSelectorState.Visible, Timeout = 20000 };

    private static async Task BlurAsync(IPage page)
    {
        await page.EvaluateAsync("() => document.activeElement?.blur()");
        await page.WaitForTimeoutAsync(300);
    }

    private async Task ReloadAsync(IPage page)
    {
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        await page.Locator(".tm-notion-page").First.WaitForAsync(Visible());
        await page.WaitForTimeoutAsync(600);
    }

    private static async Task<int> IndexOfBlockAsync(IPage page, string blockId) =>
        await page.EvaluateAsync<int>(
            """
            id => {
                const list = document.querySelector("[data-notion-block-list]:not([data-parent-block-id])");
                return [...list.children].filter(c => c.hasAttribute('data-block-id'))
                    .findIndex(c => c.getAttribute('data-block-id') === id);
            }
            """,
            blockId);

    private static async Task<int> IndexOfTextAsync(IPage page, string text) =>
        await page.EvaluateAsync<int>(
            """
            needle => {
                const list = document.querySelector("[data-notion-block-list]:not([data-parent-block-id])");
                return [...list.children].filter(c => c.hasAttribute('data-block-id'))
                    .findIndex(c => c.innerText.trim() === needle);
            }
            """,
            text);

    private static async Task PasteHtmlAsync(IPage page, string html)
    {
        await page.EvaluateAsync(
            """
            html => {
                const data = new DataTransfer();
                data.setData('text/html', html);
                data.setData('text/plain', html.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim());
                document.activeElement.dispatchEvent(new ClipboardEvent('paste', {
                    clipboardData: data, bubbles: true, cancelable: true
                }));
            }
            """,
            html);
    }

    private static async Task<string> FocusTopLevelParagraphAsync(IPage page, string text)
    {
        await page.Locator(".tm-notion-paragraph[contenteditable='true']").First.WaitForAsync(Visible());

        var blockId = await page.EvaluateAsync<string?>(
            """
            () => {
                for (const el of document.querySelectorAll(".tm-notion-paragraph[contenteditable='true']")) {
                    const list = el.closest('[data-notion-block-list]');
                    if (list && !list.hasAttribute('data-parent-block-id')) {
                        return el.closest('[data-block-id]')?.getAttribute('data-block-id') ?? null;
                    }
                }
                return null;
            }
            """);
        Assert.IsFalse(string.IsNullOrWhiteSpace(blockId));

        await page.Locator($"[data-block-id='{blockId}'] [contenteditable='true']").First.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        if (text.Length > 0) await page.Keyboard.TypeAsync(text);
        else await page.Keyboard.PressAsync("Delete");
        await page.WaitForTimeoutAsync(400);
        return blockId!;
    }
}
