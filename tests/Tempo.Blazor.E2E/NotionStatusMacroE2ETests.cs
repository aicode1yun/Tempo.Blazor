using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class NotionStatusMacroE2ETests : NotionE2ETestBase
{
    private const string StatusBlockId = "eb500000-0000-0000-0000-000000000002";
    private const string StatusBlockSelector = $"[data-block-id='{StatusBlockId}'] .tm-notion-paragraph[contenteditable='true']";

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF2: /status inserts an inline status, edits label/color, persists HTML, and captures picker/chip baselines")]
    public async Task CF2_StatusMacro_InsertEditPersistAndBaseline()
    {
        var page = await OpenStatusEditorAsync();

        await SetEditableTextAndCaretAsync(page, "Release ", 8);
        await OpenStatusPickerAsync(page);
        var picker = page.Locator(".tm-notion-status-picker").First;
        await CaptureBaselineAsync("status-macro", "palette-colors", picker);

        await InsertStatusAsync(page, "DONE", "green");

        var chip = page.Locator($"{StatusBlockSelector} .tm-notion-status").First;
        await chip.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.AreEqual("DONE", await chip.GetAttributeAsync("data-status-label"), "Inserted status should persist the label as data-status-label.");
        Assert.AreEqual("green", await chip.GetAttributeAsync("data-status-color"), "Inserted status should persist the selected color.");
        StringAssert.Contains(await chip.GetAttributeAsync("class") ?? string.Empty, "tm-notion-status--green");

        await chip.ClickAsync();
        await picker.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        var input = picker.Locator(".tm-notion-status-picker__input").First;
        await input.FillAsync("IN PROGRESS");
        await picker.Locator(".tm-notion-status-picker__swatch--blue").ClickAsync();
        await picker.Locator(".tm-notion-status-picker__insert").ClickAsync();

        var edited = page.Locator($"{StatusBlockSelector} .tm-notion-status").First;
        await edited.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.AreEqual("IN PROGRESS", await edited.GetAttributeAsync("data-status-label"), "Editing should replace the chip label.");
        Assert.AreEqual("blue", await edited.GetAttributeAsync("data-status-color"), "Editing should replace the chip color.");
        Assert.AreEqual(1, await page.Locator($"{StatusBlockSelector} .tm-notion-status").CountAsync(), "Editing should replace the existing chip, not insert a duplicate.");
        StringAssert.DoesNotMatch(await page.Locator(StatusBlockSelector).First.InnerTextAsync(), new System.Text.RegularExpressions.Regex("/"));

        await BlurEditorAsync(page);
        var persistedHtml = await FetchStatusBlockHtmlAsync(page);
        StringAssert.Contains(persistedHtml, "tm-notion-status--blue");
        StringAssert.Contains(persistedHtml, "IN PROGRESS");

        var contrast = await MinimumStatusContrastAsync(page);
        Assert.IsTrue(contrast >= 3.0, $"Status chip contrast should be at least 3:1 for inline UI text. Actual: {contrast:0.00}");

        await CaptureBaselineAsync("status-macro", "inline-chip-edited", page.Locator(StatusBlockSelector).First);
    }

    [TestMethod]
    [Description("CF2: empty labels are rejected, long labels truncate visually, and Backspace removes status chips")]
    public async Task CF2_StatusMacro_EdgeCases_Work()
    {
        var page = await OpenStatusEditorAsync();

        await SetEditableTextAndCaretAsync(page, string.Empty, 0);
        await OpenStatusPickerAsync(page);
        var picker = page.Locator(".tm-notion-status-picker").First;
        var insertButton = picker.Locator(".tm-notion-status-picker__insert").First;
        Assert.IsTrue(await insertButton.IsDisabledAsync(), "Insert should be disabled while the status label is empty.");
        await insertButton.ClickAsync(new LocatorClickOptions { Force = true });
        Assert.AreEqual(0, await page.Locator($"{StatusBlockSelector} .tm-notion-status").CountAsync(), "Empty label should not insert a status chip.");

        const string LongLabel = "WAITING FOR SECURITY REVIEW AND RELEASE COORDINATION WITH ENTERPRISE CUSTOMERS";
        await picker.Locator(".tm-notion-status-picker__input").FillAsync(LongLabel);
        await picker.Locator(".tm-notion-status-picker__swatch--red").ClickAsync();
        await picker.Locator(".tm-notion-status-picker__insert").ClickAsync();

        var longChip = page.Locator($"{StatusBlockSelector} .tm-notion-status").First;
        await longChip.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.AreEqual(LongLabel, await longChip.GetAttributeAsync("data-status-label"), "Long labels should remain available in the stored chip data.");
        var isTruncated = await longChip.Locator(".tm-notion-status__label").EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth");
        Assert.IsTrue(isTruncated, "Long status labels should truncate visually inside the inline chip.");

        await PlaceCaretAfterStatusAsync(page, 0);
        await page.Keyboard.PressAsync("Backspace");
        await page.WaitForTimeoutAsync(500);
        Assert.AreEqual(0, await page.Locator($"{StatusBlockSelector} .tm-notion-status").CountAsync(), "Backspace should remove the status chip at the caret boundary.");
    }

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("CF2: status can be inserted in the middle of text and stays baseline-aligned with surrounding content")]
    public async Task CF2_StatusMacro_MiddleOfTextAndUxMetrics_Work()
    {
        var page = await OpenStatusEditorAsync();

        await SetEditableTextAndCaretAsync(page, "Alpha Beta", 6);
        await OpenStatusPickerAsync(page);
        await InsertStatusAsync(page, "PENDING", "yellow");

        var paragraph = page.Locator(StatusBlockSelector).First;
        var text = await paragraph.InnerTextAsync();
        Assert.IsTrue(text.Contains("Alpha", StringComparison.Ordinal) &&
                      text.Contains("PENDING", StringComparison.Ordinal) &&
                      text.Contains("Beta", StringComparison.Ordinal),
            $"Status inserted in the middle should preserve surrounding text. Actual: {text}");

        var metrics = await paragraph.Locator(".tm-notion-status").First.EvaluateAsync<StatusChipMetrics>(
            """
            el => {
                const style = getComputedStyle(el);
                const rect = el.getBoundingClientRect();
                const parentRect = el.closest('[contenteditable="true"]').getBoundingClientRect();
                return {
                    verticalAlign: style.verticalAlign,
                    lineHeight: style.lineHeight,
                    chipTop: rect.top,
                    paragraphTop: parentRect.top,
                    chipHeight: rect.height,
                    paragraphHeight: parentRect.height
                };
            }
            """);

        Assert.AreEqual("baseline", metrics.VerticalAlign, "Status chip should align to the text baseline.");
        Assert.IsTrue(metrics.ChipHeight <= metrics.ParagraphHeight + 6, "Status chip should not disrupt the paragraph line height.");

        await BlurEditorAsync(page);
        await CaptureBaselineAsync("status-macro", "middle-of-text-chip", page.Locator(StatusBlockSelector).First);
    }

    private async Task<IPage> OpenStatusEditorAsync()
    {
        var page = await OpenNotionEditorAsync();
        await InvokeSeedAsync("seedMentionTokenPage");
        await page.WaitForSelectorAsync(StatusBlockSelector, new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        return page;
    }

    private static async Task SetEditableTextAndCaretAsync(IPage page, string text, int offset)
    {
        await page.EvaluateAsync(
            """
            args => {
                const el = document.querySelector(args.selector);
                if (!el) throw new Error('Status test editable block was not found.');
                el.textContent = args.text;
                el.focus();
                const node = el.firstChild || document.createTextNode('');
                if (!node.parentNode) el.appendChild(node);
                const range = document.createRange();
                range.setStart(node, Math.min(args.offset, node.textContent.length));
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                el.dispatchEvent(new Event('input', { bubbles: true }));
            }
            """,
            new { selector = StatusBlockSelector, text, offset });
        await page.WaitForTimeoutAsync(250);
        await page.EvaluateAsync(
            """
            args => {
                const el = document.querySelector(args.selector);
                if (!el) throw new Error('Status test editable block was not found after input sync.');
                el.focus();
                const node = el.firstChild || document.createTextNode('');
                if (!node.parentNode) el.appendChild(node);
                const range = document.createRange();
                range.setStart(node, Math.min(args.offset, node.textContent.length));
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
            }
            """,
            new { selector = StatusBlockSelector, offset });
    }

    private static async Task OpenStatusPickerAsync(IPage page)
    {
        await page.Keyboard.TypeAsync("/");
        var slash = page.Locator(".tm-notion-slash").First;
        await slash.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var slashInput = slash.Locator(".tm-notion-slash__input").First;
        await slashInput.FillAsync("status");
        await page.WaitForTimeoutAsync(300);

        var statusItem = slash.Locator(".tm-notion-slash__item", new LocatorLocatorOptions { HasTextString = "Status" }).First;
        await statusItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await statusItem.EvaluateAsync("el => el.click()");

        await page.Locator(".tm-notion-status-picker").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 8000
        });
    }

    private static async Task InsertStatusAsync(IPage page, string label, string color)
    {
        var picker = page.Locator(".tm-notion-status-picker").First;
        await picker.Locator(".tm-notion-status-picker__input").FillAsync(label);
        await picker.Locator($".tm-notion-status-picker__swatch--{color}").ClickAsync();
        await picker.Locator(".tm-notion-status-picker__insert").ClickAsync();
        await picker.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 8000 });
    }

    private static async Task PlaceCaretAfterStatusAsync(IPage page, int index)
    {
        await page.EvaluateAsync(
            """
            args => {
                const chips = document.querySelectorAll(args.selector + ' .tm-notion-status');
                const chip = chips[args.index];
                if (!chip) throw new Error('Status chip was not found for Backspace test.');
                const editor = chip.closest('[contenteditable="true"]');
                editor.focus();
                const range = document.createRange();
                const next = chip.nextSibling;
                if (next && next.nodeType === Node.TEXT_NODE) {
                    range.setStart(next, 0);
                } else {
                    range.setStartAfter(chip);
                }
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
            }
            """,
            new { selector = StatusBlockSelector, index });
    }

    private static async Task BlurEditorAsync(IPage page)
    {
        var heading = page.Locator(".tm-notion-h1").First;
        if (await heading.CountAsync() > 0)
        {
            await heading.ClickAsync();
        }
        else
        {
            await page.Keyboard.PressAsync("Escape");
        }

        await page.WaitForTimeoutAsync(1500);
    }

    private static async Task<string> FetchStatusBlockHtmlAsync(IPage page) =>
        await page.EvaluateAsync<string>(
            """
            async args => {
                const pageEl = document.querySelector('.tm-notion-page');
                const pageId = pageEl?.dataset.pageId;
                if (!pageId) throw new Error('Notion page id was not found.');
                const response = await fetch(`https://localhost:5100/api/notion/aggregate/pages/${pageId}`);
                if (!response.ok) throw new Error(`Aggregate request failed: ${response.status}`);
                const aggregate = await response.json();
                const block = aggregate.snapshot.blocks.find(
                    b => String(b.id).toLowerCase() === args.blockId);
                return block?.content?.html || '';
            }
            """,
            new { blockId = StatusBlockId });

    private static async Task<double> MinimumStatusContrastAsync(IPage page) =>
        await page.Locator(".tm-notion-status").EvaluateAllAsync<double>(
            """
            chips => {
                function parseColor(value) {
                    const m = value.match(/rgba?\(([^)]+)\)/);
                    if (!m) return [255, 255, 255];
                    const parts = m[1].split(',').map(v => Number.parseFloat(v.trim()));
                    const alpha = parts.length >= 4 ? parts[3] : 1;
                    return [
                        Math.round(parts[0] * alpha + 255 * (1 - alpha)),
                        Math.round(parts[1] * alpha + 255 * (1 - alpha)),
                        Math.round(parts[2] * alpha + 255 * (1 - alpha))
                    ];
                }
                function channel(v) {
                    const x = v / 255;
                    return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4);
                }
                function luminance(rgb) {
                    return 0.2126 * channel(rgb[0]) + 0.7152 * channel(rgb[1]) + 0.0722 * channel(rgb[2]);
                }
                function ratio(fg, bg) {
                    const a = luminance(fg);
                    const b = luminance(bg);
                    return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
                }
                return Math.min(...chips.map(chip => {
                    const style = getComputedStyle(chip);
                    return ratio(parseColor(style.color), parseColor(style.backgroundColor));
                }));
            }
            """);

    public sealed class StatusChipMetrics
    {
        public string VerticalAlign { get; set; } = string.Empty;
        public string LineHeight { get; set; } = string.Empty;
        public double ChipTop { get; set; }
        public double ParagraphTop { get; set; }
        public double ChipHeight { get; set; }
        public double ParagraphHeight { get; set; }
    }
}
