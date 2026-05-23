using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for the document editor floating text toolbar.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase6E2ETests : DocumentEditorE2ETestBase
{
    private const string SelectionBlockId = "recovery-comment-paragraph";
    private const string SelectionPhrase = "This paragraph";

    [TestMethod]
    public async Task RecoveryTextSelection_MouseShowsFloatingToolbarAndKeepsItAfterMouseUp()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await DragSelectPhraseAsync(page, SelectionBlockId, SelectionPhrase);
        var toolbar = page.GetByTestId("document-mini-toolbar");
        await ExpectMiniToolbarVisibleAsync(page, toolbar);
        await page.WaitForTimeoutAsync(250);
        await Assertions.Expect(toolbar).ToBeVisibleAsync();

        var selectionRect = await ReadNativeSelectionRectAsync(page);
        await ExpectToolbarNearSelectionAsync(toolbar, selectionRect);

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryTextSelection_MouseShowsFloatingToolbarAndKeepsItAfterMouseUp));
    }

    [TestMethod]
    public async Task RecoveryFloatingToolbar_BoldClickKeepsSelectionAndToolbarVisible()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await DragSelectPhraseAsync(page, SelectionBlockId, SelectionPhrase);
        var toolbar = page.GetByTestId("document-mini-toolbar");
        await ExpectMiniToolbarVisibleAsync(page, toolbar);

        await page.GetByTestId("document-mini-bold").ClickAsync();
        await ExpectMiniToolbarVisibleAsync(page, toolbar);
        try
        {
            await Assertions.Expect(page.GetByTestId("document-mini-bold")).ToHaveAttributeAsync("aria-pressed", "true");
        }
        catch (PlaywrightException ex)
        {
            var debug = await ReadFormattingDebugAsync(page, SelectionBlockId);
            throw new AssertFailedException($"{ex.Message}\nFormatting debug: {debug}");
        }

        var selectedText = await ReadNativeSelectedTextAsync(page);
        StringAssert.Contains(selectedText, SelectionPhrase);

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryFloatingToolbar_BoldClickKeepsSelectionAndToolbarVisible));
    }

    [TestMethod]
    public async Task RecoveryFloatingToolbar_ColorPopoverStaysOpenAndOutsideClickClosesToolbar()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await DragSelectPhraseAsync(page, SelectionBlockId, SelectionPhrase);
        var toolbar = page.GetByTestId("document-mini-toolbar");
        await ExpectMiniToolbarVisibleAsync(page, toolbar);

        await page.Locator("[data-testid='document-mini-text-color'] .tm-color-picker-trigger").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='document-mini-text-color'] .tm-color-picker-dropdown")).ToBeVisibleAsync();
        await Assertions.Expect(toolbar).ToBeVisibleAsync();

        await page.Mouse.ClickAsync(16, 16);
        await Assertions.Expect(toolbar).ToHaveCountAsync(0);

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryFloatingToolbar_ColorPopoverStaysOpenAndOutsideClickClosesToolbar));
    }

    [TestMethod]
    public async Task RecoveryFloatingToolbar_StaysInsideViewportAndAwayFromSidePanel()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await DragSelectPhraseAsync(page, SelectionBlockId, SelectionPhrase);
        var toolbar = page.GetByTestId("document-mini-toolbar");
        await ExpectMiniToolbarVisibleAsync(page, toolbar);

        var geometry = await ReadFloatingGeometryAsync(page);
        Assert.IsTrue(geometry.Toolbar.Width > 1, "The floating toolbar must have visible geometry.");
        Assert.IsTrue(geometry.Toolbar.X >= 0, "The floating toolbar must stay inside the left viewport edge.");
        Assert.IsTrue(geometry.Toolbar.X + geometry.Toolbar.Width <= geometry.ViewportWidth + 0.5,
            "The floating toolbar must stay inside the right viewport edge.");
        Assert.IsTrue(geometry.Toolbar.Y >= 0, "The floating toolbar must stay inside the top viewport edge.");
        Assert.IsTrue(geometry.Toolbar.Y + geometry.Toolbar.Height <= geometry.ViewportHeight + 0.5,
            "The floating toolbar must stay inside the bottom viewport edge.");
        if (geometry.SidePanel is not null)
        {
            Assert.IsTrue(geometry.Toolbar.X + geometry.Toolbar.Width <= geometry.SidePanel.X - 2,
                "The floating toolbar must not overlap the right side panel.");
        }

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryFloatingToolbar_StaysInsideViewportAndAwayFromSidePanel));
    }

    private static async Task DragSelectPhraseAsync(IPage page, string blockId, string phrase)
    {
        await ScrollBlockIntoViewAsync(page, blockId);
        await SelectPhraseWithNativeRangeAsync(page, blockId, phrase);
        try
        {
            await page.WaitForFunctionAsync(
                "phrase => (window.getSelection()?.toString() || '').includes(phrase)",
                phrase,
                new() { Timeout = 3000 });
        }
        catch (TimeoutException ex)
        {
            var debug = await ReadFloatingDebugAsync(page);
            throw new AssertFailedException($"{ex.Message}\nFloating debug: {debug}");
        }
    }

    private static Task SelectPhraseWithNativeRangeAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync(
            """
            ({ blockId, phrase }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? window.CSS.escape(blockId) : String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const block = visibleBlock(host, escaped);
                if (!block) throw new Error(`Could not find block '${blockId}'.`);

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue && node.nodeValue.length > 0
                            ? NodeFilter.FILTER_ACCEPT
                            : NodeFilter.FILTER_REJECT;
                    }
                });
                const nodes = [];
                let text = '';
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    nodes.push({ node, start: text.length, end: text.length + node.nodeValue.length });
                    text += node.nodeValue;
                }
                const start = text.indexOf(phrase);
                if (start < 0) throw new Error(`Could not find phrase '${phrase}' in '${text}'.`);
                const end = start + phrase.length;
                const startPosition = positionAt(start);
                const endPosition = positionAt(end);
                const range = document.createRange();
                range.setStart(startPosition.node, startPosition.offset);
                range.setEnd(endPosition.node, endPosition.offset);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                block.closest('[contenteditable="true"]')?.focus?.({ preventScroll: true });
                document.dispatchEvent(new Event('selectionchange'));
                block.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, composed: true }));

                function positionAt(offset) {
                    for (const entry of nodes) {
                        if (offset <= entry.end) {
                            return { node: entry.node, offset: Math.max(0, Math.min(entry.node.nodeValue.length, offset - entry.start)) };
                        }
                    }
                    const last = nodes[nodes.length - 1];
                    return { node: last.node, offset: last.node.nodeValue.length };
                }

                function visibleBlock(root, escapedId) {
                    return Array.from(root?.querySelectorAll(`[data-block-id="${escapedId}"], [data-render-block-id="${escapedId}"]`) || [])
                        .find(node => {
                            const rect = node.getBoundingClientRect();
                            const style = getComputedStyle(node);
                            return rect.width > 1
                                && rect.height > 1
                                && style.visibility !== 'hidden'
                                && style.display !== 'none'
                                && !node.closest('.tm-wysiwyg-page--virtual');
                        }) || null;
                }
            }
            """,
            new { blockId, phrase });

    private static async Task ScrollBlockIntoViewAsync(IPage page, string blockId)
    {
        await page.EvaluateAsync(
            """
            (blockId) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? window.CSS.escape(blockId) : String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const block = visibleBlock(host, escaped);
                if (!block) throw new Error(`Could not find block '${blockId}'.`);
                block.scrollIntoView({ block: 'start', inline: 'nearest' });

                function visibleBlock(root, escapedId) {
                    return Array.from(root?.querySelectorAll(`[data-block-id="${escapedId}"], [data-render-block-id="${escapedId}"]`) || [])
                        .find(node => {
                            const rect = node.getBoundingClientRect();
                            const style = getComputedStyle(node);
                            return rect.width > 1
                                && rect.height > 1
                                && style.visibility !== 'hidden'
                                && style.display !== 'none'
                                && !node.closest('.tm-wysiwyg-page--virtual');
                        }) || null;
                }
            }
            """,
            blockId);
        await page.WaitForTimeoutAsync(150);
    }

    private static Task<PhraseDragTarget> GetPhraseDragTargetAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync<PhraseDragTarget>(
            """
            ({ blockId, phrase }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? window.CSS.escape(blockId) : String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const block = visibleBlock(host, escaped);
                if (!block) throw new Error(`Could not find block '${blockId}'.`);

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue && node.nodeValue.length > 0
                            ? NodeFilter.FILTER_ACCEPT
                            : NodeFilter.FILTER_REJECT;
                    }
                });
                const nodes = [];
                let text = '';
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    nodes.push({ node, start: text.length, end: text.length + node.nodeValue.length });
                    text += node.nodeValue;
                }
                const start = text.indexOf(phrase);
                if (start < 0) throw new Error(`Could not find phrase '${phrase}' in '${text}'.`);
                const end = start + phrase.length;

                const startPosition = positionAt(start);
                const endPosition = positionAt(end);
                const startRect = charRect(startPosition.node, startPosition.offset, true);
                const endRect = charRect(endPosition.node, endPosition.offset, false);
                return {
                    start: { x: startRect.left + 1, y: startRect.top + startRect.height / 2 },
                    end: { x: endRect.right - 1, y: endRect.top + endRect.height / 2 }
                };

                function positionAt(offset) {
                    for (const entry of nodes) {
                        if (offset <= entry.end) {
                            return { node: entry.node, offset: Math.max(0, Math.min(entry.node.nodeValue.length, offset - entry.start)) };
                        }
                    }
                    const last = nodes[nodes.length - 1];
                    return { node: last.node, offset: last.node.nodeValue.length };
                }

                function charRect(node, offset, forward) {
                    const range = document.createRange();
                    const length = node.nodeValue.length;
                    const startOffset = forward
                        ? Math.max(0, Math.min(length - 1, offset))
                        : Math.max(0, Math.min(length - 1, offset - 1));
                    range.setStart(node, startOffset);
                    range.setEnd(node, Math.min(length, startOffset + 1));
                    return range.getBoundingClientRect();
                }

                function visibleBlock(root, escapedId) {
                    return Array.from(root?.querySelectorAll(`[data-block-id="${escapedId}"], [data-render-block-id="${escapedId}"]`) || [])
                        .find(node => {
                            const rect = node.getBoundingClientRect();
                            const style = getComputedStyle(node);
                            return rect.width > 1
                                && rect.height > 1
                                && style.visibility !== 'hidden'
                                && style.display !== 'none'
                                && !node.closest('.tm-wysiwyg-page--virtual');
                        }) || null;
                }
            }
            """,
            new { blockId, phrase });

    private static Task<DocumentEditorRectProbe> ReadNativeSelectionRectAsync(IPage page)
        => page.EvaluateAsync<DocumentEditorRectProbe>(
            """
            () => {
                const selection = window.getSelection();
                if (!selection || selection.rangeCount === 0) return { x: 0, y: 0, width: 0, height: 0 };
                const range = selection.getRangeAt(0);
                const rects = Array.from(range.getClientRects()).filter(rect => rect.width > 0.5 && rect.height > 0.5);
                const source = rects.length ? rects : [range.getBoundingClientRect()];
                const left = Math.min(...source.map(rect => rect.left));
                const top = Math.min(...source.map(rect => rect.top));
                const right = Math.max(...source.map(rect => rect.right));
                const bottom = Math.max(...source.map(rect => rect.bottom));
                return { x: left, y: top, width: right - left, height: bottom - top };
            }
            """);

    private static Task<string> ReadNativeSelectedTextAsync(IPage page)
        => page.EvaluateAsync<string>("() => window.getSelection()?.toString() || ''");

    private static Task<FloatingGeometryProbe> ReadFloatingGeometryAsync(IPage page)
        => page.EvaluateAsync<FloatingGeometryProbe>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-mini-toolbar"]');
                const sidePanel = document.querySelector('[data-testid="document-side-panel"]');
                const rectOf = node => {
                    if (!node) return null;
                    const rect = node.getBoundingClientRect();
                    return { x: rect.left, y: rect.top, width: rect.width, height: rect.height };
                };
                return {
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight,
                    toolbar: rectOf(toolbar) || { x: 0, y: 0, width: 0, height: 0 },
                    sidePanel: rectOf(sidePanel)
                };
            }
            """);

    private static async Task ExpectMiniToolbarVisibleAsync(IPage page, ILocator toolbar)
    {
        try
        {
            await Assertions.Expect(toolbar).ToBeVisibleAsync();
        }
        catch (PlaywrightException ex)
        {
            var debug = await ReadFloatingDebugAsync(page);
            throw new AssertFailedException($"{ex.Message}\nFloating debug: {debug}");
        }
    }

    private static Task<string> ReadFloatingDebugAsync(IPage page)
        => page.EvaluateAsync<string>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const root = document.querySelector('[data-testid="document-wysiwyg-engine-root"]');
                const instanceId = host?.getAttribute('data-instance-id') || root?.querySelector('[data-instance-id]')?.getAttribute('data-instance-id') || '';
                const inst = instanceId && window.tmDocumentEditorEngine?.__testHooks?.instances?.get?.(instanceId);
                const selection = window.getSelection();
                const range = selection && selection.rangeCount ? selection.getRangeAt(0) : null;
                const common = range?.commonAncestorContainer?.nodeType === Node.ELEMENT_NODE
                    ? range.commonAncestorContainer
                    : range?.commonAncestorContainer?.parentElement;
                const rect = range?.getBoundingClientRect?.();
                return JSON.stringify({
                    selectedText: selection?.toString?.() || '',
                    isCollapsed: selection?.isCollapsed ?? null,
                    rangeCount: selection?.rangeCount ?? 0,
                    rootContainsCommon: !!(root && common && root.contains(common)),
                    commonName: common?.nodeName || '',
                    commonClass: common?.className || '',
                    editableClosest: !!common?.closest?.('.tm-wysiwyg-page__body[contenteditable], .tm-wysiwyg-page__header[contenteditable], .tm-wysiwyg-page__footer[contenteditable], .tm-wysiwyg-table-cell, .tm-wysiwyg-block[data-block-id]'),
                    rect: rect ? { x: rect.left, y: rect.top, width: rect.width, height: rect.height } : null,
                    instanceId,
                    hasInstance: !!inst,
                    floatingUiOpen: inst?.floatingUiOpen ?? null,
                    lastMiniToolbarRequest: inst?.lastMiniToolbarRequest ?? null,
                    eventHandlers: inst?.eventHandlers?.length ?? null,
                    documentEventHandlers: inst?.documentEventHandlers?.length ?? null,
                    timelineTail: inst?.diagnostics?.timeline?.slice?.(-6) ?? []
                });
            }
            """);

    private static Task<string> ReadFormattingDebugAsync(IPage page, string blockId)
        => page.EvaluateAsync<string>(
            """
            blockId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const root = document.querySelector('[data-testid="document-wysiwyg-engine-root"]');
                const instanceId = host?.getAttribute('data-instance-id') || root?.querySelector('[data-instance-id]')?.getAttribute('data-instance-id') || '';
                const inst = instanceId && window.tmDocumentEditorEngine?.__testHooks?.instances?.get?.(instanceId);
                const block = inst?.model?.body?.blocks?.find?.(item => item.id === blockId);
                return JSON.stringify({
                    selectedText: window.getSelection?.()?.toString?.() || '',
                    commandTail: inst?.commands?.slice?.(-5) ?? [],
                    selection: inst?.selection ?? null,
                    formatting: instanceId ? window.tmDocumentEditorRuntime?.getFormattingState?.(instanceId) : null,
                    runs: block?.content?.runs ?? null,
                    timelineTail: inst?.diagnostics?.timeline?.slice?.(-10) ?? []
                });
            }
            """,
            blockId);

    private sealed class PhraseDragTarget
    {
        [JsonPropertyName("start")] public DocumentEditorPointProbe Start { get; set; } = new();
        [JsonPropertyName("end")] public DocumentEditorPointProbe End { get; set; } = new();
    }

    private sealed class FloatingGeometryProbe
    {
        [JsonPropertyName("viewportWidth")] public double ViewportWidth { get; set; }
        [JsonPropertyName("viewportHeight")] public double ViewportHeight { get; set; }
        [JsonPropertyName("toolbar")] public DocumentEditorRectProbe Toolbar { get; set; } = new();
        [JsonPropertyName("sidePanel")] public DocumentEditorRectProbe? SidePanel { get; set; }
    }
}
