using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for the JS-owned typing/input pipeline.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeInputTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase6_FastTypingUsesJsOwnedInputWithoutFullRender()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        var before = await ReadRenderStatsAsync(page);
        var text = "aaaaaaaaaaaaaaaaaaaa";

        await page.Keyboard.TypeAsync(text, new() { Delay = 5 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(text);
        await page.WaitForTimeoutAsync(120);

        var after = await ReadRenderStatsAsync(page);
        var debug = await ReadDebugSnapshotAsync(page);

        Assert.AreEqual(before.FullRenderCount, after.FullRenderCount, "Typing must mutate the JS-owned surface without a full Blazor render.");
        Assert.IsTrue(debug.JsOwnedInputCount >= text.Length, $"Expected JS-owned input for each typed character. Actual: {debug.JsOwnedInputCount}.");
        Assert.AreEqual(0, debug.NativeInputCount, "Normal typing should not fall back to native browser input mutation.");
    }

    [TestMethod]
    public async Task Phase16_LongTypingRecordsLatencyAndDoesNotRenderThroughBlazor()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);

        await page.Keyboard.TypeAsync("x", new() { Delay = 1 });
        await page.WaitForTimeoutAsync(450);
        await ClearRenderStatsAsync(page);
        var before = await ReadRenderStatsAsync(page);
        var text = new string('a', 96);

        await page.Keyboard.TypeAsync(text, new() { Delay = 1 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(text);
        await page.WaitForTimeoutAsync(200);

        var after = await ReadRenderStatsAsync(page);
        var debug = await ReadDebugSnapshotAsync(page);

        Assert.AreEqual(before.FullRenderCount, after.FullRenderCount, "Long typing must not use a full runtime document render.");
        var blazorRenderDelta = after.BlazorRenderCount - before.BlazorRenderCount;
        Assert.IsTrue(blazorRenderDelta <= 6, $"Long typing should not continuously render through Blazor. Delta: {blazorRenderDelta}.");
        Assert.IsTrue(after.IncrementalOperationCount >= text.Length, $"Expected incremental text operations. Actual: {after.IncrementalOperationCount}.");
        Assert.IsTrue(after.InputOperationCount >= text.Length, $"Expected input metrics for each typed character. Actual: {after.InputOperationCount}.");
        Assert.IsTrue(after.MaxInputOperationMs < 50, $"Input operation max latency is too high: {after.MaxInputOperationMs:0.##} ms.");
        Assert.IsTrue(after.MaxInputLatencyMs < 75, $"Input event latency is too high: {after.MaxInputLatencyMs:0.##} ms.");
        Assert.IsTrue(debug.JsOwnedInputCount >= text.Length + 1, $"Expected JS-owned input for long typing. Actual: {debug.JsOwnedInputCount}.");
        Assert.AreEqual(0, debug.NativeInputCount, "Long typing should not fall back to native browser input mutation.");
    }

    [TestMethod]
    public async Task Phase6_EnterSplitsParagraphAndContinuesInNewBlock()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        var before = await ReadSelectionAsync(page);
        var marker = $" phase6-enter-{DateTimeOffset.UtcNow:HHmmssfff} ";

        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.InsertTextAsync(marker);
        var after = await ReadSelectionAsync(page);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(marker.Trim());
        Assert.AreNotEqual(before.BlockId, after.BlockId, "Enter should create and focus a new paragraph block.");
        Assert.IsTrue(after.Offset >= marker.Trim().Length, "Typing after Enter should continue in the new paragraph.");
    }

    [TestMethod]
    public async Task Phase6_ShiftEnterCreatesSoftBreakAndKeepsCurrentBlock()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        var before = await ReadSelectionAsync(page);
        var marker = $" phase6-soft-{DateTimeOffset.UtcNow:HHmmssfff} ";

        await page.Keyboard.PressAsync("Shift+Enter");
        await page.Keyboard.InsertTextAsync(marker);
        var after = await ReadSelectionAsync(page);

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(marker.Trim());
        Assert.AreEqual(before.BlockId, after.BlockId, "Shift+Enter should stay in the same paragraph block.");
        Assert.IsTrue(after.Offset > before.Offset, "Caret should continue after the soft break.");
    }

    [TestMethod]
    public async Task Phase6_ShiftEnterMovesCaretToVisibleSoftBreakLineImmediately()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretInVisibleTextBlockAsync(page, 0, 100_000);
        var before = await ReadCaretGeometryAsync(page);

        await page.Keyboard.PressAsync("Shift+Enter");
        var after = await ReadCaretGeometryAsync(page);

        Assert.AreEqual(before.BlockId, after.BlockId, "Shift+Enter should keep the caret in the same paragraph block.");
        Assert.IsTrue(after.InlineBreakCount > before.InlineBreakCount, "Shift+Enter should insert a soft break immediately.");
        Assert.IsTrue(after.CaretPlaceholderCount > 0, "The empty soft-break line should have a temporary caret placeholder.");
        Assert.IsTrue(after.CaretTop > before.CaretTop + 4, $"Caret should move to the next visual line immediately. Before: {before.CaretTop:0.##}, after: {after.CaretTop:0.##}.");
    }

    [TestMethod]
    public async Task Phase6_EnterCreatesVisibleEmptyParagraphImmediately()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await PlaceCaretInVisibleTextBlockAsync(page, 0, 100_000);
        var before = await ReadCaretGeometryAsync(page);

        await page.Keyboard.PressAsync("Enter");
        var after = await ReadCaretGeometryAsync(page);

        Assert.AreNotEqual(before.BlockId, after.BlockId, "Enter should create and focus a new paragraph block immediately.");
        Assert.IsTrue(after.CaretPlaceholderCount > 0, "The empty paragraph should have a temporary caret placeholder before the user starts typing.");
        Assert.IsTrue(after.CaretTop > before.CaretTop + 4, $"Caret should move to the new paragraph immediately. Before: {before.CaretTop:0.##}, after: {after.CaretTop:0.##}.");
    }

    [TestMethod]
    public async Task Phase6_BackspaceAtParagraphStartMergesWithPreviousParagraph()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var beforeCount = await CountVisibleTopLevelBlocksAsync(page);
        await PlaceCaretInMergeableBlockStartAsync(page);
        var before = await ReadSelectionAsync(page);

        await page.Keyboard.PressAsync("Backspace");
        await page.WaitForFunctionAsync(
            """
            previous => {
                const blocks = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body > .tm-wysiwyg-block[data-block-id]'))
                    .filter(el => el.textContent.trim().length > 0 && !el.matches('figure, table, hr') && el.getBoundingClientRect().height > 0);
                return blocks.length === previous - 1;
            }
            """,
            beforeCount);
        var after = await ReadSelectionAsync(page);
        var afterCount = await CountVisibleTopLevelBlocksAsync(page);

        Assert.AreEqual(beforeCount - 1, afterCount);
        Assert.AreNotEqual(before.BlockId, after.BlockId, "The caret should move into the previous merged paragraph.");
    }

    private static Task<RenderStats> ReadRenderStatsAsync(IPage page)
    {
        return page.EvaluateAsync<RenderStats>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentWysiwygDebug?.getRenderStats?.(instanceId) || {};
                const editor = document.querySelector('[data-testid="document-editor-demo"]');
                return {
                    fullRenderCount: Number(stats.FullRenderCount || 0),
                    incrementalOperationCount: Number(stats.IncrementalOperationCount || 0),
                    lastRenderReason: String(stats.LastRenderReason || ''),
                    inputOperationCount: Number(stats.InputOperationCount || 0),
                    inputLongOperationCount: Number(stats.InputLongOperationCount || 0),
                    maxInputLatencyMs: Number(stats.MaxInputLatencyMs || 0),
                    maxInputOperationMs: Number(stats.MaxInputOperationMs || 0),
                    blazorRenderCount: Number(editor?.getAttribute('data-blazor-render-count') || 0)
                };
            }
            """);
    }

    private static Task ClearRenderStatsAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentWysiwyg?.clearDebugMetrics?.(instanceId);
            }
            """);
    }

    private static Task<DebugSnapshot> ReadDebugSnapshotAsync(IPage page)
    {
        return page.EvaluateAsync<DebugSnapshot>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentWysiwygDebug?.getRuntimeState?.(instanceId) || {};
            }
            """);
    }

    private static Task<SelectionSnapshot> ReadSelectionAsync(IPage page)
    {
        return page.EvaluateAsync<SelectionSnapshot>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const selection = window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId) || {};
                return {
                    blockId: String(selection.AnchorBlockId || selection.anchorBlockId || ''),
                    inlineId: String(selection.AnchorInlineId || selection.anchorInlineId || ''),
                    offset: Number(selection.AnchorOffset ?? selection.anchorOffset ?? 0)
                };
            }
            """);
    }

    private static Task<CaretGeometry> ReadCaretGeometryAsync(IPage page)
    {
        return page.EvaluateAsync<CaretGeometry>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const runtimeSelection = window.tmDocumentEditorRuntime?.getSelectionSnapshot?.(instanceId) || {};
                const selection = window.getSelection();
                if (!host || !selection || selection.rangeCount === 0) {
                    return {
                        blockId: String(runtimeSelection.AnchorBlockId || runtimeSelection.anchorBlockId || ''),
                        offset: Number(runtimeSelection.AnchorOffset ?? runtimeSelection.anchorOffset ?? 0),
                        caretTop: 0,
                        caretHeight: 0,
                        blockTop: 0,
                        blockHeight: 0,
                        inlineBreakCount: 0,
                        caretPlaceholderCount: 0
                    };
                }

                const range = selection.getRangeAt(0);
                const originalRange = range.cloneRange();
                const block = (range.startContainer.nodeType === Node.ELEMENT_NODE
                        ? range.startContainer
                        : range.startContainer.parentElement)
                    ?.closest?.('[data-block-id]');

                const readRect = () => {
                    const direct = Array.from(range.getClientRects()).find(rect => rect.height > 0);
                    if (direct) {
                        return direct;
                    }

                    const marker = document.createElement('span');
                    marker.textContent = '\u200b';
                    marker.setAttribute('data-e2e-caret-marker', 'true');
                    marker.style.cssText = 'display:inline-block;width:0;height:1em;overflow:hidden;padding:0;margin:0;border:0;';
                    const measuringRange = originalRange.cloneRange();
                    measuringRange.insertNode(marker);
                    const markerRect = marker.getBoundingClientRect();
                    marker.remove();
                    selection.removeAllRanges();
                    selection.addRange(originalRange);
                    document.dispatchEvent(new Event('selectionchange'));
                    return markerRect;
                };

                const caretRect = readRect();
                const blockRect = block?.getBoundingClientRect?.() || { top: 0, height: 0 };
                const scrollTop = Number(host.scrollTop || window.scrollY || window.pageYOffset || 0);
                return {
                    blockId: String(runtimeSelection.AnchorBlockId || runtimeSelection.anchorBlockId || block?.getAttribute('data-block-id') || ''),
                    offset: Number(runtimeSelection.AnchorOffset ?? runtimeSelection.anchorOffset ?? 0),
                    caretTop: Number((caretRect?.top || 0) + scrollTop),
                    caretHeight: Number(caretRect?.height || 0),
                    blockTop: Number((blockRect.top || 0) + scrollTop),
                    blockHeight: Number(blockRect.height || 0),
                    inlineBreakCount: Number(block?.querySelectorAll?.('br[data-inline-break]').length || 0),
                    caretPlaceholderCount: Number(block?.querySelectorAll?.('br[data-caret-placeholder]').length || 0)
                };
            }
            """);
    }

    private static Task<int> CountVisibleTopLevelBlocksAsync(IPage page)
    {
        return page.EvaluateAsync<int>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body > .tm-wysiwyg-block[data-block-id]'))
                .filter(el => el.textContent.trim().length > 0 && !el.matches('figure, table, hr') && el.getBoundingClientRect().height > 0)
                .length
            """);
    }

    private static Task PlaceCaretInVisibleTextBlockAsync(IPage page, int blockIndex, int offset)
    {
        return page.EvaluateAsync(
            """
            ({ blockIndex, offset }) => {
                const blocks = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body > .tm-wysiwyg-block[data-block-id]'))
                    .filter(el => {
                        const rect = el.getBoundingClientRect();
                        const style = getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden' && el.textContent.length > 0;
                    });
                const block = blocks[blockIndex];
                if (!block) {
                    throw new Error(`Visible text block ${blockIndex} was not found.`);
                }

                const resolve = absoluteOffset => {
                    const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
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
                const pos = resolve(Math.max(0, Math.min(offset, block.textContent.length)));
                if (!pos) {
                    throw new Error('Editable text node was not found.');
                }

                const range = document.createRange();
                range.setStart(pos.node, pos.offset);
                range.collapse(true);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            new { blockIndex, offset });
    }

    private static Task PlaceCaretInMergeableBlockStartAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const bodies = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-page__body'));
                let block = null;
                for (const body of bodies) {
                    const blocks = Array.from(body.querySelectorAll(':scope > .tm-wysiwyg-block[data-block-id]'))
                        .filter(el => el.textContent.trim().length > 0 && !el.matches('figure, table, hr') && el.getBoundingClientRect().height > 0);
                    block = blocks.find((candidate, index) => index > 0 && blocks[index - 1]);
                    if (block) break;
                }

                if (!block) {
                    throw new Error('Mergeable top-level text block was not found.');
                }

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                const node = walker.nextNode();
                if (!node) {
                    throw new Error('Mergeable block text node was not found.');
                }

                const range = document.createRange();
                range.setStart(node, 0);
                range.collapse(true);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """);
    }

    private sealed class RenderStats
    {
        [JsonPropertyName("fullRenderCount")]
        public int FullRenderCount { get; set; }

        [JsonPropertyName("incrementalOperationCount")]
        public int IncrementalOperationCount { get; set; }

        [JsonPropertyName("lastRenderReason")]
        public string LastRenderReason { get; set; } = string.Empty;

        [JsonPropertyName("inputOperationCount")]
        public int InputOperationCount { get; set; }

        [JsonPropertyName("inputLongOperationCount")]
        public int InputLongOperationCount { get; set; }

        [JsonPropertyName("maxInputLatencyMs")]
        public double MaxInputLatencyMs { get; set; }

        [JsonPropertyName("maxInputOperationMs")]
        public double MaxInputOperationMs { get; set; }

        [JsonPropertyName("blazorRenderCount")]
        public int BlazorRenderCount { get; set; }
    }

    private sealed class DebugSnapshot
    {
        [JsonPropertyName("JsOwnedInputCount")]
        public int JsOwnedInputCount { get; set; }

        [JsonPropertyName("NativeInputCount")]
        public int NativeInputCount { get; set; }
    }

    private sealed class SelectionSnapshot
    {
        [JsonPropertyName("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [JsonPropertyName("inlineId")]
        public string InlineId { get; set; } = string.Empty;

        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }

    private sealed class CaretGeometry
    {
        [JsonPropertyName("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("caretTop")]
        public double CaretTop { get; set; }

        [JsonPropertyName("caretHeight")]
        public double CaretHeight { get; set; }

        [JsonPropertyName("blockTop")]
        public double BlockTop { get; set; }

        [JsonPropertyName("blockHeight")]
        public double BlockHeight { get; set; }

        [JsonPropertyName("inlineBreakCount")]
        public int InlineBreakCount { get; set; }

        [JsonPropertyName("caretPlaceholderCount")]
        public int CaretPlaceholderCount { get; set; }
    }
}
