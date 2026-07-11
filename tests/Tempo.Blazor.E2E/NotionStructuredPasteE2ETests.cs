using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Pasting from a web page or from Word must keep the structure: headings stay headings, lists stay
/// lists, a table stays a table. A snippet with a single block level still pastes inline.
/// Runs against the self-hosted HTTPS demo API (5100) and HTTPS demo WASM (7106).
/// </summary>
[TestClass]
public class NotionStructuredPasteE2ETests : NotionE2ETestBase
{
    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Pasting a heading and a paragraph creates two blocks, not one flattened block")]
    public async Task PastingAnArticle_CreatesOneBlockPerElement()
    {
        var page = await OpenNotionEditorAsync();
        var blockId = await FocusTopLevelParagraphAsync(page);
        var before = await BlockCountAsync(page);

        await PasteHtmlAsync(page, "<h1>Pasted title</h1><p>Pasted body</p>");
        await page.WaitForTimeoutAsync(1500);

        Assert.IsTrue(await BlockCountAsync(page) >= before + 2, "Two blocks must have been created.");
        await page.Locator(".tm-notion-heading--h1", new() { HasTextString = "Pasted title" }).First
            .WaitForAsync(Visible());

        await CaptureBaselineAsync("structured-paste", "article", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine("UX: a pasted article arrives as editable blocks, so the very next keystroke works on the right heading.");
    }

    [TestMethod]
    [Description("Pasting a list creates one block per item")]
    public async Task PastingAList_CreatesOneBlockPerItem()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page);
        var before = await BlockCountAsync(page);

        await PasteHtmlAsync(page, "<ul><li>alpha</li><li>beta</li><li>gamma</li></ul>");
        await page.WaitForTimeoutAsync(1500);

        Assert.IsTrue(await BlockCountAsync(page) >= before + 3, "Three list blocks must have been created.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Word-flavoured HTML keeps its structure and does not fall back to plain text")]
    public async Task PastingWordHtml_KeepsItsStructure()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page);
        var before = await BlockCountAsync(page);

        // Word wraps everything in <div>, uses &nbsp; and leaves void elements unclosed.
        await PasteHtmlAsync(page,
            "<div><meta charset=\"utf-8\"><h2>Word&nbsp;heading</h2><p>Body&mdash;dash</p></div>");
        await page.WaitForTimeoutAsync(1500);

        Assert.IsTrue(await BlockCountAsync(page) >= before + 2);
        var heading = page.Locator(".tm-notion-heading--h2", new() { HasTextString = "Word" }).First;
        await heading.WaitForAsync(Visible());

        // The entity must have been decoded into a character. A non-breaking space is still a
        // space to the reader, so compare with whitespace normalized.
        var headingText = (await heading.InnerTextAsync()).Replace('\u00A0', ' ');
        StringAssert.Contains(headingText, "Word heading");
        Assert.IsFalse(headingText.Contains("&nbsp;", StringComparison.Ordinal),
            "The raw entity must never reach the DOM.");

        await CaptureBaselineAsync("structured-paste", "word-html", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine("UX: content from Word lands as real headings and paragraphs — no entity noise, no single glued paragraph.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Pasting a table creates a table block with its rows")]
    public async Task PastingATable_CreatesATableBlock()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page);
        var before = await page.Locator(".tm-notion-table").CountAsync();

        await PasteHtmlAsync(page,
            "<table><tr><th>Name</th><th>Status</th></tr><tr><td>CF26</td><td>Ready</td></tr></table>");
        await page.WaitForTimeoutAsync(1800);

        Assert.AreEqual(before + 1, await page.Locator(".tm-notion-table").CountAsync(),
            "A lone table has no inline form and must become a table block.");

        // The rows must be attached to the new table, not orphaned by the id remap on insert.
        // The demo page has tables of its own, so find the one holding the pasted cells.
        var pasted = page.Locator(".tm-notion-table").Filter(new() { HasTextString = "CF26" });
        await pasted.First.WaitForAsync(Visible());

        var cells = (await pasted.First.Locator("td, th").AllInnerTextsAsync())
            .Select(cell => cell.Trim()).ToList();
        CollectionAssert.Contains(cells, "Name");
        CollectionAssert.Contains(cells, "Status");
        CollectionAssert.Contains(cells, "CF26");

        await CaptureBaselineAsync("structured-paste", "table", page.Locator(".tm-notion-page").First);
        TestContext.WriteLine("UX: a table pasted from a web page stays a table — the rows are immediately editable cells.");
    }

    [TestMethod]
    [Description("Pasting inline markup keeps the text in the current block")]
    public async Task PastingInlineMarkup_DoesNotSplitTheBlock()
    {
        var page = await OpenNotionEditorAsync();
        var blockId = await FocusTopLevelParagraphAsync(page);
        await page.Keyboard.TypeAsync("start ");
        await page.WaitForTimeoutAsync(300);
        var before = await BlockCountAsync(page);

        await PasteHtmlAsync(page, "<strong>bold</strong>");
        await page.WaitForTimeoutAsync(1200);

        Assert.AreEqual(before, await BlockCountAsync(page), "One block level must paste inline.");
        var text = await Block(page, blockId).InnerTextAsync();
        StringAssert.Contains(text, "start");
        StringAssert.Contains(text, "bold");
    }

    [TestMethod]
    [Description("A pasted script payload never executes and never reaches the DOM")]
    public async Task PastedScriptPayload_IsSanitized()
    {
        var page = await OpenNotionEditorAsync();
        await FocusTopLevelParagraphAsync(page);

        await PasteHtmlAsync(page, """<h1>Title</h1><p>x<img src=q onerror="window.__pwned = true"></p>""");
        await page.WaitForTimeoutAsync(1500);

        Assert.IsNull(await page.EvaluateAsync<bool?>("() => window.__pwned ?? null"),
            "The pasted payload must not execute.");
    }

    [TestMethod]
    [Description("A fenced code block survives the Markdown round-trip without gaining escapes")]
    public async Task CodeBlock_SurvivesTheMarkdownRoundTrip()
    {
        var page = await OpenNotionEditorAsync();

        var source = page.Locator("[data-testid='markdown-table-source']");
        await source.WaitForAsync(Visible());
        await source.FillAsync("```csharp\nvar x = **1**;\n```");
        await page.WaitForTimeoutAsync(900);

        var roundTrip = await page.Locator("[data-testid='markdown-table-roundtrip']").InnerTextAsync();

        StringAssert.Contains(roundTrip, "```csharp", "The fence and its language must come back.");
        StringAssert.Contains(roundTrip, "var x = **1**;", "Code is verbatim — no inline parsing, no escaping.");
        Assert.IsFalse(roundTrip.Contains(@"\*", StringComparison.Ordinal), "Code must never be escaped.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LocatorWaitForOptions Visible() =>
        new() { State = WaitForSelectorState.Visible, Timeout = 20000 };

    private static ILocator Block(IPage page, string blockId) =>
        page.Locator($"[data-block-id='{blockId}']").First;

    private static async Task<int> BlockCountAsync(IPage page) =>
        await page.Locator("[data-notion-block-list]:not([data-parent-block-id]) > [data-block-id]").CountAsync();

    /// <summary>Fires a real paste event carrying text/html, exactly as the browser would.</summary>
    private static async Task PasteHtmlAsync(IPage page, string html)
    {
        await page.EvaluateAsync(
            """
            html => {
                const target = document.activeElement;
                const data = new DataTransfer();
                data.setData('text/html', html);
                // Real clipboards separate blocks with newlines; a single run-on token would
                // look like a hostname to the smart-link detector.
                data.setData('text/plain', html.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim());
                target.dispatchEvent(new ClipboardEvent('paste', {
                    clipboardData: data, bubbles: true, cancelable: true
                }));
            }
            """,
            html);
    }

    private static async Task<string> FocusTopLevelParagraphAsync(IPage page)
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
        Assert.IsFalse(string.IsNullOrWhiteSpace(blockId), "The demo page must expose a top-level paragraph.");

        await Block(page, blockId!).Locator("[contenteditable='true']").First.ClickAsync();
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.PressAsync("Delete");
        await page.WaitForTimeoutAsync(400);
        return blockId!;
    }
}
