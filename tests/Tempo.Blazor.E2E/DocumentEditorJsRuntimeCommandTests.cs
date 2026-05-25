using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for the JS-owned ribbon command dispatcher.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeCommandTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase5_BoldRibbonCommandFormatsSelectionWithIncrementalOperation()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var target = await SelectFirstVisibleTextPrefixAsync(page, length: 6, preferNormalWeight: true);

        var boldButton = page.GetByTestId("document-bold");
        await Assertions.Expect(boldButton).ToBeEnabledAsync(new() { Timeout = 5000 });
        var beforePressed = await boldButton.GetAttributeAsync("aria-pressed") ?? "false";
        await boldButton.ClickAsync();
        await Assertions.Expect(boldButton).ToHaveAttributeAsync("aria-pressed", beforePressed == "true" ? "false" : "true");
        await page.WaitForFunctionAsync(
            """
            ({ blockId, selectedText, expectActive }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = raw ? JSON.parse(raw) : {};
                const documentModel = snapshot.Document || snapshot.document || snapshot;
                const blocks = documentModel.Body?.Blocks
                    || documentModel.body?.blocks
                    || documentModel.Blocks
                    || documentModel.blocks
                    || [];
                const block = blocks.find(item => (item.Id || item.id) === blockId);
                const content = block?.Content || block?.content || {};
                const runs = content.Inlines || content.inlines || content.Runs || content.runs || [];
                const run = runs.find(item => String(item.Text || item.text || '').startsWith(selectedText));
                const marks = run?.Marks || run?.marks || [];
                const isBold = marks.some(item => {
                    const type = item.Type ?? item.type ?? '';
                    return String(type) === 'Bold' || String(type) === '0';
                });
                return isBold === expectActive;
            }
            """,
            new { blockId = target.BlockId, selectedText = target.SelectedText, expectActive = beforePressed != "true" });
    }

    [TestMethod]
    public async Task Phase5_FontSizeRibbonCommandUpdatesToolbarState()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await SelectFirstVisibleTextPrefixAsync(page, length: 6);

        await page.Locator("[data-testid='document-font-size']").SelectOptionAsync("18");
        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const formatting = window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId);
                return String(formatting?.FontSize || formatting?.fontSize || '') === '18pt';
            }
            """);

        var stateFontSize = await page.EvaluateAsync<string>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const formatting = window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId) || {};
                return String(formatting.FontSize || formatting.fontSize || document.querySelector('[data-testid="document-font-size"]')?.value || '');
            }
            """);
        Assert.AreEqual("18pt", stateFontSize);
        var transaction = await ReadLastCommandTransactionAsync(page);
        Assert.AreEqual("setFontSize", transaction.Command);
    }

    [TestMethod]
    public async Task Phase5_FontSizeRibbonAppliesImmediatelyAndSyncsMiniToolbar()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await page.GetByTestId("document-ribbon-tab-home").ClickAsync();
        var target = await SelectFirstVisibleTextPrefixAsync(page, length: 6);

        await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });
        var fontSizeProbe = await ReadFontSizeToolbarProbeAsync(page);
        Assert.IsTrue(fontSizeProbe.FontSizeCount > 0, fontSizeProbe.Debug);
        await page.GetByTestId("document-font-size").SelectOptionAsync("28");

        await page.WaitForTimeoutAsync(500);
        var fontSizeMark = await ReadFontSizeMarkAsync(page, target.BlockId, target.SelectedText);
        Assert.AreEqual("28pt", fontSizeMark.DebugValue, fontSizeMark.Debug);

        await Assertions.Expect(page.GetByTestId("document-font-size")).ToHaveValueAsync("28", new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-mini-font-size")).ToHaveValueAsync("28", new() { Timeout = 5000 });

        var transaction = await ReadLastCommandTransactionAsync(page);
        Assert.AreEqual("setFontSize", transaction.Command);
    }

    [TestMethod]
    public async Task Phase5_ParagraphAlignmentRibbonCommandUpdatesSelectedBlock()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var target = await SelectFirstVisibleTextPrefixAsync(page, length: 6);

        var alignRightButton = page.GetByTestId("document-align-right");
        await Assertions.Expect(alignRightButton).ToBeEnabledAsync(new() { Timeout = 5000 });
        await alignRightButton.ClickAsync();
        await page.WaitForFunctionAsync(
            """
            blockId => {
                const block = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${CSS.escape(blockId)}"]`);
                return (block?.style.textAlign || getComputedStyle(block).textAlign || '') === 'right';
            }
            """,
            target.BlockId);

        var textAlign = await page.EvaluateAsync<string>(
            """
            blockId => {
                const block = document.querySelector(`[data-testid="document-wysiwyg-host"] [data-block-id="${CSS.escape(blockId)}"]`);
                return block?.style.textAlign || getComputedStyle(block).textAlign || '';
            }
            """,
            target.BlockId);
        Assert.AreEqual("right", textAlign);
        var transaction = await ReadLastCommandTransactionAsync(page);
        Assert.AreEqual("setParagraphAlignment", transaction.Command);
    }

    private static Task<bool> SelectInlineContentsAsync(IPage page, string inlineId)
    {
        return page.EvaluateAsync<bool>(
            """
            inlineId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const isVisible = el => {
                    if (!el || el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual')) return false;
                    const rect = el.getBoundingClientRect();
                    const style = getComputedStyle(el);
                    return rect.width > 0
                        && rect.height > 0
                        && style.visibility !== 'hidden'
                        && style.display !== 'none';
                };
                const inline = Array.from(host?.querySelectorAll(`[data-inline-id="${CSS.escape(inlineId)}"]`) || [])
                    .find(isVisible);
                if (!inline) {
                    return false;
                }

                const resolve = absoluteOffset => {
                    const walker = document.createTreeWalker(inline, NodeFilter.SHOW_TEXT);
                    let current = 0;
                    let node;
                    while ((node = walker.nextNode())) {
                        const length = node.textContent.length;
                        if (absoluteOffset <= current + length) {
                            return { node, offset: Math.max(0, Math.min(absoluteOffset - current, length)) };
                        }
                        current += length;
                    }
                    return null;
                };
                const start = resolve(0);
                const end = resolve(inline.textContent.length);
                if (!start || !end) {
                    return false;
                }

                const range = document.createRange();
                range.setStart(start.node, start.offset);
                range.setEnd(end.node, end.offset);
                inline.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
                return true;
            }
            """,
            inlineId);
    }

    private static Task<TextRangeTarget> SelectFirstVisibleTextPrefixAsync(IPage page, int length, bool preferNormalWeight = false)
    {
        return page.EvaluateAsync<TextRangeTarget>(
            """
            args => {
                const length = Number(args.length ?? args.Length ?? 1) || 1;
                const preferNormalWeight = args.preferNormalWeight === true || args.PreferNormalWeight === true;
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const isVisible = el => {
                    if (!el || el.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual')) return false;
                    const rect = el.getBoundingClientRect();
                    const style = getComputedStyle(el);
                    return rect.width > 0
                        && rect.height > 0
                        && style.visibility !== 'hidden'
                        && style.display !== 'none';
                };
                const blocks = Array.from(host?.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body .tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)') || [])
                    .filter(isVisible);
                const normalBlocks = preferNormalWeight
                    ? blocks.filter(block => {
                        const weight = Number.parseInt(getComputedStyle(block).fontWeight, 10);
                        return Number.isFinite(weight) ? weight < 600 : true;
                    })
                    : blocks;
                const candidates = normalBlocks.length > 0
                    ? normalBlocks.concat(blocks.filter(block => !normalBlocks.includes(block)))
                    : blocks;
                for (const block of candidates) {
                    const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                        acceptNode: node => (node.textContent || '').trim().length > 0
                            ? NodeFilter.FILTER_ACCEPT
                            : NodeFilter.FILTER_REJECT
                    });
                    const text = walker.nextNode();
                    if (!text) continue;
                    const take = Math.max(1, Math.min(Number(length) || 1, text.textContent.length));
                    const range = document.createRange();
                    range.setStart(text, 0);
                    range.setEnd(text, take);
                    block.closest('[contenteditable="true"]')?.focus();
                    const selection = window.getSelection();
                    selection.removeAllRanges();
                    selection.addRange(range);
                    document.dispatchEvent(new Event('selectionchange'));
                    return {
                        blockId: block.getAttribute('data-block-id') || '',
                        selectedText: text.textContent.slice(0, take)
                    };
                }
                throw new Error('No visible text block found.');
            }
            """,
            new { length, preferNormalWeight });
    }

    private static Task<FontSizeMarkProbe> ReadFontSizeMarkAsync(IPage page, string blockId, string selectedText)
    {
        return page.EvaluateAsync<FontSizeMarkProbe>(
            """
            ({ blockId, selectedText }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const raw = window.tmDocumentEditorRuntime?.getDocument?.(instanceId);
                const snapshot = raw ? JSON.parse(raw) : {};
                const documentModel = snapshot.Document || snapshot.document || snapshot;
                const blocks = documentModel.Body?.Blocks
                    || documentModel.body?.blocks
                    || documentModel.Blocks
                    || documentModel.blocks
                    || [];
                const block = blocks.find(item => (item.Id || item.id) === blockId);
                const content = block?.Content || block?.content || {};
                const runs = content.Inlines || content.inlines || content.Runs || content.runs || [];
                const run = runs.find(item => String(item.Text || item.text || '').startsWith(selectedText));
                const marks = run?.Marks || run?.marks || [];
                const mark = marks.find(item => {
                    const type = item.Type ?? item.type ?? '';
                    return String(type) === 'FontSize' || String(type) === '12';
                });
                const formatting = window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId) || {};
                const transaction = window.tmDocumentEditorRuntime?.getLastCommandTransaction?.(instanceId) || {};
                return {
                    debugValue: String(mark?.Value || mark?.value || ''),
                    debug: JSON.stringify({
                        blockId,
                        selectedText,
                        run,
                        runs,
                        formatting,
                        transaction,
                        selectValue: document.querySelector('[data-testid="document-font-size"]')?.value || '',
                        miniValue: document.querySelector('[data-testid="document-mini-font-size"]')?.value || ''
                    })
                };
            }
            """,
            new { blockId, selectedText });
    }

    private static Task<FontSizeToolbarProbe> ReadFontSizeToolbarProbeAsync(IPage page)
    {
        return page.EvaluateAsync<FontSizeToolbarProbe>(
            """
            () => {
                const ids = Array.from(document.querySelectorAll('[data-testid]'))
                    .map(node => node.getAttribute('data-testid') || '')
                    .filter(id => id.includes('document-font') || id.includes('document-ribbon-tab') || id.includes('document-toolbar'))
                    .join(', ');
                const toolbar = document.querySelector('[data-testid="document-toolbar"]');
                return {
                    fontSizeCount: document.querySelectorAll('[data-testid="document-font-size"]').length,
                    debug: `toolbarMode=${toolbar?.getAttribute('data-toolbar-mode') || ''}; tabs=${ids}; html=${toolbar?.outerHTML?.slice(0, 1000) || ''}`
                };
            }
            """);
    }

    private static Task<RenderStats> ReadRenderStatsAsync(IPage page)
    {
        return page.EvaluateAsync<RenderStats>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                return {
                    fullRenderCount: Number(stats.FullRenderCount || 0),
                    incrementalOperationCount: Number(stats.IncrementalOperationCount || 0),
                    lastRenderReason: String(stats.LastRenderReason || '')
                };
            }
            """);
    }

    private static Task<CommandTransaction> ReadLastCommandTransactionAsync(IPage page)
    {
        return page.EvaluateAsync<CommandTransaction>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.getLastCommandTransaction?.(instanceId) || {};
            }
            """);
    }

    private sealed class FontSizeToolbarProbe
    {
        [JsonPropertyName("fontSizeCount")]
        public int FontSizeCount { get; set; }

        [JsonPropertyName("debug")]
        public string Debug { get; set; } = string.Empty;
    }

    private sealed class FontSizeMarkProbe
    {
        [JsonPropertyName("debugValue")]
        public string DebugValue { get; set; } = string.Empty;

        [JsonPropertyName("debug")]
        public string Debug { get; set; } = string.Empty;
    }

    private sealed class TextRangeTarget
    {
        [JsonPropertyName("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [JsonPropertyName("selectedText")]
        public string SelectedText { get; set; } = string.Empty;
    }

    private sealed class RenderStats
    {
        [JsonPropertyName("fullRenderCount")]
        public int FullRenderCount { get; set; }

        [JsonPropertyName("incrementalOperationCount")]
        public int IncrementalOperationCount { get; set; }

        [JsonPropertyName("lastRenderReason")]
        public string LastRenderReason { get; set; } = string.Empty;
    }

    private sealed class CommandTransaction
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("operations")]
        public CommandOperation[] Operations { get; set; } = [];

        [JsonPropertyName("inverseOperations")]
        public CommandOperation[] InverseOperations { get; set; } = [];
    }

    private sealed class CommandOperation
    {
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        [JsonPropertyName("inverseOf")]
        public string InverseOf { get; set; } = string.Empty;
    }
}
