using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for the JS-owned WYSIWYG undo/redo runtime.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeUndoTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase7_ImmediateCtrlZUndoesFreshTyping()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase7-immediate-{DateTimeOffset.UtcNow:HHmmssfff}";

        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        var fullRenderBefore = await ReadFullRenderCountAsync(page);
        await page.Keyboard.TypeAsync(marker, new() { Delay = 5 });
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(marker);

        await RuntimeUndoAsync(page);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).Not.ToContainTextAsync(marker);

        var undo = await ReadUndoStateAsync(page);
        Assert.IsTrue(undo.CanRedo, "Immediate undo should move the typing transaction to the redo stack.");
        Assert.AreEqual(fullRenderBefore, await ReadFullRenderCountAsync(page), "JS undo should not perform a full snapshot render.");
    }

    [TestMethod]
    public async Task Phase7_TypingPauseCreatesSeparateUndoBoundary()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var first = $"phase7-a-{DateTimeOffset.UtcNow:HHmmssfff}";
        var second = $"phase7-b-{DateTimeOffset.UtcNow:HHmmssfff}";

        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        await page.Keyboard.TypeAsync(first);
        await WaitForCommittedUndoDepthAsync(page, 1);
        await PlaceCaretAtEndOfBlockContainingTextAsync(page, first);
        await InsertTextThroughBeforeInputAsync(page, second);
        await WaitForCommittedUndoDepthAsync(page, 2);

        await RuntimeUndoAsync(page);

        var text = await ReadHostTextAsync(page);
        var debug = await ReadUndoDebugAsync(page);
        Assert.IsTrue(text.Contains(first, StringComparison.Ordinal), $"The earlier typing transaction should remain after one undo. Undo debug: {debug}");
        Assert.IsFalse(text.Contains(second, StringComparison.Ordinal), $"Only the latest typing transaction should be undone. Undo debug: {debug}");
    }

    [TestMethod]
    public async Task Phase7_EnterAndFollowingTypingAreSeparateUndoSteps()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var first = $"phase7-enter-a-{DateTimeOffset.UtcNow:HHmmssfff}";
        var second = $"phase7-enter-b-{DateTimeOffset.UtcNow:HHmmssfff}";

        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        await page.Keyboard.TypeAsync(first);
        await WaitForCommittedUndoDepthAsync(page, 1);
        await PlaceCaretAtEndOfBlockContainingTextAsync(page, first);
        await page.Keyboard.PressAsync("Enter");
        await WaitForCommittedUndoDepthAsync(page, 2);
        await InsertTextThroughBeforeInputAsync(page, second);
        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(second);

        await page.Keyboard.PressAsync("Control+Z");

        var text = await ReadHostTextAsync(page);
        var debug = await ReadUndoDebugAsync(page);
        Assert.IsTrue(text.Contains(first, StringComparison.Ordinal), debug);
        Assert.IsFalse(text.Contains(second, StringComparison.Ordinal), debug);
    }

    [TestMethod]
    public async Task Phase7_ToolbarUndoUsesSameRuntimeStackAsCtrlZ()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase7-toolbar-{DateTimeOffset.UtcNow:HHmmssfff}";

        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        await page.Keyboard.TypeAsync(marker);
        await WaitForUndoAsync(page);

        await page.GetByTestId("document-undo").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).Not.ToContainTextAsync(marker);
    }

    [TestMethod]
    public async Task Phase7_RedoRestoresTextAndSelection()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase7-redo-{DateTimeOffset.UtcNow:HHmmssfff}";

        await PlaceCaretInVisibleTextBlockAsync(page, 0, 6);
        await page.Keyboard.TypeAsync(marker);
        await page.WaitForTimeoutAsync(650);

        await page.Keyboard.PressAsync("Control+Z");
        await page.Keyboard.PressAsync("Control+Y");

        await Assertions.Expect(page.Locator("[data-testid='document-wysiwyg-host']")).ToContainTextAsync(marker);
        var selection = await ReadSelectionAsync(page);
        Assert.IsTrue(selection.Offset >= marker.Length, "Redo should restore the caret after the redone typing.");
    }

    private static Task WaitForUndoAsync(IPage page)
    {
        return page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getUndoState?.(instanceId);
                return !!state?.CanUndo && !document.querySelector('[data-testid="document-undo"]')?.disabled;
            }
            """);
    }

    private static Task WaitForCommittedUndoDepthAsync(IPage page, int depth)
    {
        return page.WaitForFunctionAsync(
            """
            expected => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getUndoState?.(instanceId);
                const pending = state?.PendingTransactionId ?? state?.pendingTransactionId ?? null;
                return !pending && Number(state?.UndoDepth ?? state?.undoDepth ?? 0) >= expected;
            }
            """,
            depth);
    }

    private static Task<UndoState> ReadUndoStateAsync(IPage page)
    {
        return page.EvaluateAsync<UndoState>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.getUndoState?.(instanceId) || {};
            }
            """);
    }

    private static Task RuntimeUndoAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.undo?.(instanceId);
            }
            """);
    }

    private static Task<string> ReadHostTextAsync(IPage page)
    {
        return page.EvaluateAsync<string>(
            """
            () => document.querySelector('[data-testid="document-wysiwyg-host"]')?.textContent || ''
            """);
    }

    private static async Task<string> ReadUndoDebugAsync(IPage page)
    {
        var debug = await page.EvaluateAsync<JsonElement>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return {
                    text: document.querySelector('[data-testid="document-wysiwyg-host"]')?.textContent || '',
                    undo: window.tmDocumentWysiwygDebug?.getUndoStack?.(instanceId) || null
                };
            }
            """);
        return JsonSerializer.Serialize(debug);
    }

    private static Task<int> ReadFullRenderCountAsync(IPage page)
    {
        return page.EvaluateAsync<int>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentWysiwygDebug?.getRenderStats?.(instanceId) || {};
                return Number(stats.FullRenderCount || 0);
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
                    offset: Number(selection.AnchorOffset ?? selection.anchorOffset ?? 0)
                };
            }
            """);
    }

    private static Task PlaceCaretInVisibleTextBlockAsync(IPage page, int blockIndex, int offset)
    {
        return page.EvaluateAsync(
            """
            ({ blockIndex, offset }) => {
                const blocks = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id]'))
                    .filter(el => {
                        const rect = el.getBoundingClientRect();
                        const style = getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden' && el.textContent.length > 0;
                    });
                const block = blocks[blockIndex];
                if (!block) {
                    throw new Error(`Visible text block ${blockIndex} was not found.`);
                }

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                let current = 0;
                let node;
                while ((node = walker.nextNode())) {
                    const length = node.textContent.length;
                    if (offset <= current + length) {
                        const range = document.createRange();
                        range.setStart(node, Math.max(0, Math.min(offset - current, length)));
                        range.collapse(true);
                        block.closest('[contenteditable="true"]')?.focus();
                        const selection = window.getSelection();
                        selection.removeAllRanges();
                        selection.addRange(range);
                        document.dispatchEvent(new Event('selectionchange'));
                        return;
                    }

                    current += length;
                }

                throw new Error('Editable text node was not found.');
            }
            """,
            new { blockIndex, offset });
    }

    private static Task PlaceCaretAtEndOfBlockContainingTextAsync(IPage page, string text)
    {
        return page.EvaluateAsync(
            """
            text => {
                const block = Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id]'))
                    .find(el => (el.textContent || '').includes(text));
                if (!block) {
                    throw new Error(`Block containing ${text} was not found.`);
                }

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
                let node;
                let last = null;
                while ((node = walker.nextNode())) {
                    last = node;
                }

                if (!last) {
                    throw new Error('Editable text node was not found.');
                }

                const range = document.createRange();
                range.setStart(last, last.textContent.length);
                range.collapse(true);
                block.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """,
            text);
    }

    private static Task InsertTextThroughBeforeInputAsync(IPage page, string text)
    {
        return page.EvaluateAsync(
            """
            text => {
                const target = document.activeElement?.closest?.('[contenteditable="true"]')
                    || document.querySelector('[data-testid="document-wysiwyg-host"] [contenteditable="true"]');
                if (!target) {
                    throw new Error('Active editor target was not found.');
                }

                const event = new InputEvent('beforeinput', {
                    inputType: 'insertText',
                    data: text,
                    bubbles: true,
                    cancelable: true,
                    composed: true
                });
                target.dispatchEvent(event);
            }
            """,
            text);
    }

    private sealed class UndoState
    {
        [JsonPropertyName("CanRedo")]
        public bool CanRedo { get; set; }
    }

    private sealed class SelectionSnapshot
    {
        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }
}
