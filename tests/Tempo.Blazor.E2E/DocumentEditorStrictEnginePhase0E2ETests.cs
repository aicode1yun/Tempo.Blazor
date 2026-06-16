using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Phase 0 strict RED tests for the next document editor engine. These tests are
/// intentionally frame-oriented: they inspect the editor after each user action,
/// before idle reconciliation can hide a broken intermediate layout.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase0E2ETests : DocumentEditorE2ETestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_LiveTypingNeverCreatesTextOverlap()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await InstallEngineTimelineAsync(page);
        var inserted = " phase zero typing should stay visually stable beside the current layout";

        try
        {
            await SetCaretInFirstLongTextBlockAsync(page, offset: 18);
            await ProbeAndAssertCleanAsync(page, "before typing");

            foreach (var ch in inserted)
            {
                await page.Keyboard.InsertTextAsync(ch.ToString());
                await WaitForNextAnimationFrameAsync(page);
                await ProbeAndAssertCleanAsync(page, $"after character '{Printable(ch)}'");
            }

            await page.WaitForTimeoutAsync(200);
            await ProbeAndAssertCleanAsync(page, "after idle reconciliation");
        }
        catch
        {
            await SavePhase0ArtifactsAsync(
                page,
                nameof(DocumentEditor_Strict_Engine_LiveTypingNeverCreatesTextOverlap),
                "Type a deterministic text sequence one character at a time into a visible paragraph.",
                "No text/text, text/image, segment overflow, missing caret, or runtime error is allowed after any animation frame.");
            throw;
        }
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_LiveTypingKeepsCaretLogicalPosition()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await InstallEngineTimelineAsync(page);
        var inserted = " caret stability check";

        try
        {
            await SetCaretInFirstLongTextBlockAsync(page, offset: 12);
            var before = await CaptureFrameProbeAsync(page);
            before.Selection.BlockId.Should().NotBeNullOrWhiteSpace("the setup must place a real caret in a text block");
            before.Selection.IsCollapsed.Should().BeTrue("typing setup must use a collapsed caret");

            var expectedBlockId = before.Selection.BlockId;
            var previousOffset = before.Selection.Offset;

            foreach (var ch in inserted)
            {
                await page.Keyboard.InsertTextAsync(ch.ToString());
                await WaitForNextAnimationFrameAsync(page);
                var after = await CaptureFrameProbeAsync(page);

                after.Selection.BlockId.Should().Be(expectedBlockId, $"caret must stay in the original logical block after '{Printable(ch)}'");
                after.Selection.IsCollapsed.Should().BeTrue($"caret must remain collapsed after '{Printable(ch)}'");
                after.Selection.Offset.Should().BeGreaterThan(previousOffset, $"caret offset must advance after '{Printable(ch)}'");
                after.Selection.CaretRect.Width.Should().BeGreaterThanOrEqualTo(0);
                after.Selection.CaretRect.Height.Should().BeGreaterThan(0, "DOM caret rect must remain measurable after each typed character");
                after.Issues.Should().BeEmpty($"visual state must remain valid after '{Printable(ch)}'");
                previousOffset = after.Selection.Offset;
            }
        }
        catch
        {
            await SavePhase0ArtifactsAsync(
                page,
                nameof(DocumentEditor_Strict_Engine_LiveTypingKeepsCaretLogicalPosition),
                "Type text one character at a time and compare the logical caret snapshot after every frame.",
                "The caret must stay in the same block, move monotonically forward, remain collapsed, and have a visible caret rect.");
            throw;
        }
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_ActiveParagraphReflowsBeforeNextPaint()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await InstallEngineTimelineAsync(page);
        var inserted = " reflow-before-next-paint reflow-before-next-paint reflow-before-next-paint";

        try
        {
            await SetCaretInFirstLongTextBlockAsync(page, offset: 55);
            var before = await CaptureFrameProbeAsync(page);
            before.ActiveBlock.LineCount.Should().BeGreaterThan(0, "the target paragraph must expose line rectangles before typing");

            var typedSoFar = string.Empty;
            foreach (var ch in inserted)
            {
                typedSoFar += ch;
                await page.Keyboard.InsertTextAsync(ch.ToString());
                await WaitForNextAnimationFrameAsync(page);
                var after = await CaptureFrameProbeAsync(page);

                after.Issues.Should().BeEmpty($"active paragraph layout must already be valid in the next frame after '{Printable(ch)}'");
                after.ActiveBlock.BlockId.Should().Be(before.ActiveBlock.BlockId, "typing must keep the active paragraph identity");
                var expectedToken = typedSoFar.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(expectedToken))
                {
                    after.ActiveBlock.Text.Should().Contain(expectedToken);
                }
                after.ActiveBlock.LineCount.Should().BeGreaterThan(0);
            }

            var beforeIdle = await CaptureFrameProbeAsync(page);
            await page.WaitForTimeoutAsync(250);
            var afterIdle = await CaptureFrameProbeAsync(page);
            afterIdle.Selection.BlockId.Should().Be(beforeIdle.Selection.BlockId, "idle reconciliation must not steal the caret from the active paragraph");
            afterIdle.Issues.Should().BeEmpty("idle reconciliation must not be required to fix visible overlap from typing");
        }
        catch
        {
            await SavePhase0ArtifactsAsync(
                page,
                nameof(DocumentEditor_Strict_Engine_ActiveParagraphReflowsBeforeNextPaint),
                "Type a long sequence into a line near wrapping pressure and inspect the next animation frame after every character.",
                "The active paragraph must be reflowed before the next paint; idle work may refine page flow but not fix visible broken text.");
            throw;
        }
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_TypingBesideWrappedImageUsesAvailableIntervals()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 1200);
        await InstallEngineTimelineAsync(page);

        try
        {
            var target = await CaptureWrappedImageLineTargetAsync(page);
            target.ImageRect.Width.Should().BeGreaterThan(0, "the reset demo must contain a measurable image");
            target.Lines.Length.Should().BeGreaterThanOrEqualTo(2, "the reset demo must expose at least two text lines beside a wrapped image");

            var firstClickedLine = await ClickWrappedLineAsync(page, target.Lines[0], xBias: 8);
            await WaitForNextAnimationFrameAsync(page);
            var firstLineProbe = await CaptureFrameProbeAsync(page);
            var firstScrollAfter = await GetWindowScrollYAsync(page);
            firstLineProbe.SelectedImageCount.Should().Be(0, "clicking the text interval beside an image must not select the image");
            firstLineProbe.Selection.CaretRect.Y.Should().BeApproximately(firstClickedLine.Rect.Y - (firstScrollAfter - firstClickedLine.ScrollYBefore), 60, "caret must land in the clicked wrapped text band after browser scroll adjustment");

            var secondClickedLine = await ClickWrappedLineAsync(page, target.Lines[1], xBias: 8);
            await WaitForNextAnimationFrameAsync(page);
            var secondLineProbe = await CaptureFrameProbeAsync(page);
            var secondScrollAfter = await GetWindowScrollYAsync(page);
            secondLineProbe.SelectedImageCount.Should().Be(0, "clicking the second text interval beside an image must not select the image");
            secondLineProbe.Selection.CaretRect.Y.Should().BeApproximately(secondClickedLine.Rect.Y - (secondScrollAfter - secondClickedLine.ScrollYBefore), 60, "caret must land in the second clicked wrapped text band after browser scroll adjustment");

            var token = " side interval typing";
            await page.Keyboard.InsertTextAsync(token);
            await WaitForNextAnimationFrameAsync(page);
            var afterTyping = await CaptureFrameProbeAsync(page);
            afterTyping.DocumentText.Should().Contain(token, "typing beside a wrapped image must insert into the document text flow");
            afterTyping.SelectedImageCount.Should().Be(0, "typing in a text interval beside an image must not activate image selection UI");
            afterTyping.Issues.Should().BeEmpty("typing beside a wrapped image must respect available intervals immediately");
        }
        catch
        {
            await SavePhase0ArtifactsAsync(
                page,
                nameof(DocumentEditor_Strict_Engine_TypingBesideWrappedImageUsesAvailableIntervals),
                "Click the first and second visual text lines beside a wrapped image, then type into the second line.",
                "Text hit-testing must use available intervals, image selection must stay inactive, and typing must not overlap text or image media.");
            throw;
        }
    }

    private static Task WaitForNextAnimationFrameAsync(IPage page)
        => page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => resolve()))");

    private static Task<double> GetWindowScrollYAsync(IPage page)
        => page.EvaluateAsync<double>("() => window.scrollY || window.pageYOffset || 0");

    private static async Task InstallEngineTimelineAsync(IPage page)
    {
        await page.EvaluateAsync(
            """
            () => {
                if (window.__tmStrictEngineTimelineInstalled) {
                    window.__tmStrictEngineTimeline = [];
                    return;
                }

                window.__tmStrictEngineTimelineInstalled = true;
                window.__tmStrictEngineTimeline = [];
                const log = (type, detail) => {
                    window.__tmStrictEngineTimeline.push({
                        type,
                        detail: detail || {},
                        time: performance.now()
                    });
                };

                document.addEventListener('beforeinput', event => log('beforeinput', {
                    inputType: event.inputType,
                    data: event.data || '',
                    target: describe(event.target)
                }), true);

                document.addEventListener('input', event => log('input', {
                    inputType: event.inputType || '',
                    data: event.data || '',
                    target: describe(event.target)
                }), true);

                document.addEventListener('keydown', event => log('keydown', {
                    key: event.key,
                    code: event.code,
                    target: describe(event.target)
                }), true);

                document.addEventListener('selectionchange', () => {
                    const selection = window.getSelection();
                    log('selectionchange', {
                        collapsed: !!selection?.isCollapsed,
                        anchor: describe(selection?.anchorNode),
                        focus: describe(selection?.focusNode)
                    });
                }, true);

                function describe(node) {
                    if (!node) return '';
                    const element = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
                    if (!element) return String(node.nodeName || '');
                    const testId = element.getAttribute?.('data-testid');
                    const blockId = element.closest?.('[data-block-id]')?.getAttribute('data-block-id');
                    const cls = Array.from(element.classList || []).slice(0, 4).join('.');
                    return `${element.tagName?.toLowerCase() || ''}${testId ? `[${testId}]` : ''}${blockId ? `{${blockId}}` : ''}${cls ? '.' + cls : ''}`;
                }
            }
            """);
    }

    private static Task SetCaretInFirstLongTextBlockAsync(IPage page, int offset)
    {
        return page.EvaluateAsync(
            """
            offset => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const blocks = Array.from(host?.querySelectorAll('.tm-wysiwyg-page:not(.tm-wysiwyg-page--virtual) .tm-wysiwyg-block[data-block-id]:not(figure):not(table):not(hr)') || []);
                const block = blocks.find(candidate => (candidate.innerText || candidate.textContent || '').trim().length > Math.max(80, offset + 5));
                if (!block) throw new Error('No long text block found for phase 0 typing setup.');
                block.scrollIntoView({ block: 'center', inline: 'nearest' });

                const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        return node.nodeValue && node.nodeValue.length > 0
                            ? NodeFilter.FILTER_ACCEPT
                            : NodeFilter.FILTER_REJECT;
                    }
                });

                let remaining = Math.max(0, Number(offset) || 0);
                let node = walker.nextNode();
                while (node && remaining > node.nodeValue.length) {
                    remaining -= node.nodeValue.length;
                    node = walker.nextNode();
                }

                if (!node) throw new Error('No text node found for phase 0 typing setup.');
                const range = document.createRange();
                range.setStart(node, Math.min(remaining, node.nodeValue.length));
                range.collapse(true);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                block.focus?.({ preventScroll: true });
                return {
                    blockId: block.getAttribute('data-block-id') || '',
                    offset: remaining
                };
            }
            """,
            offset);
    }

    private static async Task<FrameProbe> CaptureFrameProbeAsync(IPage page)
    {
        var probe = await page.EvaluateAsync<FrameProbe>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const textRects = collectTextRects(host);
                const imageRects = collectImageRects(host);
                const activeBlock = getActiveBlockProbe();
                const selection = getSelectionProbe();
                const issues = [];

                for (let i = 0; i < textRects.length; i++) {
                    for (let j = i + 1; j < textRects.length; j++) {
                        if (textRects[i].sourceId === textRects[j].sourceId) continue;
                        if (textRectsOverlap(textRects[i].rect, textRects[j].rect, 1.5)) {
                            issues.push(`text/text overlap: ${textRects[i].blockId || '?'} <-> ${textRects[j].blockId || '?'}`);
                        }
                    }
                }

                for (const text of textRects) {
                    for (const image of imageRects) {
                        if (intersects(text.rect, image.rect, 1.5)) {
                            issues.push(`text/image overlap: ${text.blockId || '?'} -> ${image.blockId || '?'}`);
                        }
                    }
                }

                const overflowSegments = Array.from(host?.querySelectorAll('[data-segment-id], .tm-wysiwyg-segment') || [])
                    .filter(el => el.scrollWidth > el.clientWidth + 3)
                    .map(el => el.getAttribute('data-segment-id') || el.textContent?.slice(0, 30) || 'segment');
                for (const segment of overflowSegments) {
                    issues.push(`segment overflow: ${segment}`);
                }

                if (!selection.isCollapsed || !selection.blockId) {
                    issues.push('missing collapsed logical caret');
                }

                const runtimeDebug = getRuntimeDebug(instanceId);
                if (runtimeDebug && runtimeDebug.LastErrors && String(runtimeDebug.LastErrors).length > 0) {
                    issues.push(`runtime errors: ${runtimeDebug.LastErrors}`);
                }

                const debugSummary = summarizeRuntimeDebug(runtimeDebug);
                const selectionSummary = {
                    blockId: selection.blockId,
                    offset: selection.offset,
                    collapsed: selection.isCollapsed,
                    caretRect: selection.caretRect
                };
                logTimeline('operation', {
                    logicalSelectionAfter: selectionSummary,
                    invalidatedScopeIds: debugSummary.invalidatedScopeIds,
                    source: 'strict-frame-probe'
                });
                logTimeline('layout', {
                    textRectCount: textRects.length,
                    imageRectCount: imageRects.length,
                    activeBlockId: activeBlock.blockId,
                    activeBlockLineCount: activeBlock.lineCount,
                    invalidatedScopeIds: debugSummary.invalidatedScopeIds
                });
                logTimeline('render', {
                    issueCount: issues.length,
                    metrics: debugSummary.renderMetrics
                });
                logTimeline('selection-restore', {
                    logicalSelectionAfter: selectionSummary
                });
                logTimeline('patch-emit', {
                    metrics: debugSummary.patchMetrics
                });

                window.__tmStrictEngineTimeline?.push?.({
                    type: 'frame-probe',
                    time: performance.now(),
                    detail: {
                        issueCount: issues.length,
                        blockId: selection.blockId,
                        offset: selection.offset,
                        activeBlockId: activeBlock.blockId
                    }
                });

                return {
                    instanceId,
                    documentText: host?.innerText || host?.textContent || '',
                    selectedImageCount: host?.querySelectorAll('figure.tm-wysiwyg-image--selected').length || 0,
                    issues,
                    activeBlock,
                    selection,
                    textRectCount: textRects.length,
                    imageRectCount: imageRects.length,
                    runtimeDebugJson: safeJson(runtimeDebug),
                    timelineJson: safeJson(window.__tmStrictEngineTimeline || [])
                };

                function safeJson(value) {
                    try {
                        return JSON.stringify(value ?? null);
                    } catch (error) {
                        return JSON.stringify({ error: String(error) });
                    }
                }

                function logTimeline(type, detail) {
                    window.__tmStrictEngineTimeline?.push?.({
                        type,
                        time: performance.now(),
                        detail: detail || {}
                    });
                }

                function summarizeRuntimeDebug(debug) {
                    const invalidatedScopeIds =
                        debug?.InvalidatedScopeIds
                        || debug?.invalidatedScopeIds
                        || debug?.LastInvalidatedScopeIds
                        || [];
                    return {
                        invalidatedScopeIds: Array.isArray(invalidatedScopeIds) ? invalidatedScopeIds : [],
                        renderMetrics: {
                            fullRenderCount: debug?.FullRenderCount ?? debug?.fullRenderCount ?? null,
                            incrementalRenderCount: debug?.IncrementalRenderCount ?? debug?.incrementalRenderCount ?? null,
                            layoutPassCount: debug?.LayoutPassCount ?? debug?.layoutPassCount ?? null
                        },
                        patchMetrics: {
                            generatedPatchCount: debug?.GeneratedPatchCount ?? debug?.generatedPatchCount ?? null,
                            pendingPatchCount: debug?.PendingPatchCount ?? debug?.pendingPatchCount ?? null,
                            lastPatchId: debug?.LastPatchId ?? debug?.lastPatchId ?? null
                        }
                    };
                }

                function collectTextRects(root) {
                    const result = [];
                    if (!root) return result;
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            const text = node.nodeValue || '';
                            const parent = node.parentElement;
                            if (!text.trim()) return NodeFilter.FILTER_REJECT;
                            if (!parent) return NodeFilter.FILTER_REJECT;
                            if (parent.closest('figure')) {
                                return NodeFilter.FILTER_REJECT;
                            }
                            if (parent.closest('[data-testid="document-side-panel"], .tm-document-editor__ribbon, [data-testid="document-wysiwyg-object-layout-bubble"], [data-testid="document-wysiwyg-image-context-menu"]')) {
                                return NodeFilter.FILTER_REJECT;
                            }
                            return NodeFilter.FILTER_ACCEPT;
                        }
                    });
                    let index = 0;
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        const block = node.parentElement?.closest('[data-block-id]');
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width <= 0.5 || rect.height <= 0.5) continue;
                            result.push({
                                sourceId: `text-${index}`,
                                blockId: block?.getAttribute('data-block-id') || '',
                                text: node.nodeValue?.trim().slice(0, 50) || '',
                                rect: toRect(rect)
                            });
                        }
                        range.detach?.();
                        index++;
                    }
                    return result;
                }

                function collectImageRects(root) {
                    return Array.from(root?.querySelectorAll('figure.tm-wysiwyg-image img, .tm-wysiwyg-image img') || [])
                        .map(img => {
                            const figure = img.closest('[data-block-id]');
                            return {
                                blockId: figure?.getAttribute('data-block-id') || '',
                                rect: toRect(img.getBoundingClientRect())
                            };
                        })
                        .filter(item => item.rect.width > 0.5 && item.rect.height > 0.5);
                }

                function getActiveBlockProbe() {
                    const selection = window.getSelection();
                    const node = selection?.focusNode || document.activeElement;
                    const element = node?.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
                    const block = element?.closest?.('[data-block-id]');
                    const rects = block ? collectBlockLineRects(block) : [];
                    return {
                        blockId: block?.getAttribute('data-block-id') || '',
                        text: block?.innerText || block?.textContent || '',
                        lineCount: rects.length,
                        lineRects: rects
                    };
                }

                function getSelectionProbe() {
                    const selection = window.getSelection();
                    const empty = {
                        isCollapsed: false,
                        blockId: '',
                        offset: -1,
                        caretRect: { x: 0, y: 0, width: 0, height: 0 }
                    };
                    if (!selection || selection.rangeCount === 0) return empty;
                    const range = selection.getRangeAt(0).cloneRange();
                    const element = selection.focusNode?.nodeType === Node.ELEMENT_NODE
                        ? selection.focusNode
                        : selection.focusNode?.parentElement;
                    const block = element?.closest?.('[data-block-id]');
                    let offset = -1;
                    if (block) {
                        const pre = document.createRange();
                        pre.selectNodeContents(block);
                        pre.setEnd(selection.focusNode, selection.focusOffset);
                        offset = pre.toString().length;
                    }
                    return {
                        isCollapsed: selection.isCollapsed,
                        blockId: block?.getAttribute('data-block-id') || '',
                        offset,
                        caretRect: getCaretRect(range)
                    };
                }

                function collectBlockLineRects(block) {
                    const rects = [];
                    const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            return node.nodeValue?.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                        }
                    });
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width > 0.5 && rect.height > 0.5) rects.push(toRect(rect));
                        }
                    }
                    return rects;
                }

                function getCaretRect(range) {
                    const rect = range.getBoundingClientRect();
                    if (rect && rect.height > 0) return toRect(rect);
                    const marker = document.createElement('span');
                    marker.setAttribute('data-phase0-caret-marker', 'true');
                    marker.textContent = '\u200b';
                    range.insertNode(marker);
                    const markerRect = marker.getBoundingClientRect();
                    const result = toRect(markerRect);
                    marker.remove();
                    return result;
                }

                function getRuntimeDebug(instanceId) {
                    try {
                        return window.tmDocumentEditorDebug?.getRuntimeState?.(instanceId) || null;
                    } catch (error) {
                        return { Error: String(error) };
                    }
                }

                function intersects(a, b, tolerance) {
                    const x = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
                    const y = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                    return x * y > tolerance;
                }

                function textRectsOverlap(a, b, tolerance) {
                    const x = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
                    const y = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                    if (x * y <= tolerance) return false;
                    return y > 2.75;
                }

                function toRect(rect) {
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }
            }
            """);

        return probe;
    }

    private static async Task<WrappedImageLineTarget> CaptureWrappedImageLineTargetAsync(IPage page)
    {
        return await page.EvaluateAsync<WrappedImageLineTarget>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const figures = Array.from(host?.querySelectorAll('figure.tm-wysiwyg-image[data-block-id]') || [])
                    .filter(figure => {
                        const rect = figure.getBoundingClientRect();
                        return rect.width > 20 && rect.height > 20;
                    });
                const figure = figures[0];
                if (!figure) {
                    return { imageBlockId: '', imageRect: zeroRect(), lines: [] };
                }

                figure.scrollIntoView({ block: 'center', inline: 'nearest' });
                const image = figure.querySelector('img') || figure;
                const imageRect = toRect(image.getBoundingClientRect());
                let textLines = collectWrappedLines(imageRect).slice(0, 6);

                const firstLine = textLines[0];
                if (firstLine && (firstLine.rect.y < 48 || firstLine.rect.y + firstLine.rect.height > window.innerHeight - 48)) {
                    const lineBlock = host?.querySelector(`[data-block-id="${cssEscape(firstLine.blockId)}"]`);
                    scrollElementToViewportCenter(lineBlock || figure);
                    const nextImageRect = toRect((figure.querySelector('img') || figure).getBoundingClientRect());
                    textLines = collectWrappedLines(nextImageRect).slice(0, 6);
                }

                return {
                    imageBlockId: figure.getAttribute('data-block-id') || '',
                    imageRect: toRect((figure.querySelector('img') || figure).getBoundingClientRect()),
                    lines: textLines
                };

                function collectWrappedLines(currentImageRect) {
                    return collectTextLineRects(host)
                        .filter(line => line.rect.y + line.rect.height > currentImageRect.y + 2 && line.rect.y < currentImageRect.y + currentImageRect.height - 2)
                        .filter(line => line.rect.x > currentImageRect.x + currentImageRect.width - 2 || line.rect.x + line.rect.width < currentImageRect.x + 2)
                        .sort((a, b) => a.rect.y - b.rect.y || a.rect.x - b.rect.x);
                }

                function collectTextLineRects(root) {
                    const result = [];
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                            const parent = node.parentElement;
                            if (!parent || parent.closest('figure, table, [data-testid="document-side-panel"], .tm-document-editor__ribbon')) {
                                return NodeFilter.FILTER_REJECT;
                            }
                            return NodeFilter.FILTER_ACCEPT;
                        }
                    });
                    let index = 0;
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        const block = node.parentElement?.closest('[data-block-id]');
                        let lineOrdinal = 0;
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width <= 8 || rect.height <= 4) continue;
                            result.push({
                                nodeIndex: index,
                                lineOrdinal: lineOrdinal++,
                                blockId: block?.getAttribute('data-block-id') || '',
                                text: node.nodeValue.trim().slice(0, 80),
                                rect: toRect(rect)
                            });
                        }
                        index++;
                    }
                    return result;
                }

                function zeroRect() {
                    return { x: 0, y: 0, width: 0, height: 0 };
                }

                function toRect(rect) {
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }

                function cssEscape(value) {
                    return window.CSS?.escape ? window.CSS.escape(value || '') : String(value || '').replace(/"/g, '\\"');
                }

                function scrollElementToViewportCenter(element) {
                    if (!element) return;
                    element.scrollIntoView?.({ block: 'center', inline: 'nearest' });
                    const rect = element.getBoundingClientRect();
                    const delta = rect.top + rect.height / 2 - window.innerHeight * 0.48;
                    if (Math.abs(delta) < 4) return;
                    for (let current = element.parentElement; current; current = current.parentElement) {
                        if (current.scrollHeight > current.clientHeight + 4) {
                            current.scrollTop += delta;
                        }
                    }
                    window.scrollBy(0, delta);
                }
            }
            """);
    }

    private static async Task<ClickedLineProbe> ClickWrappedLineAsync(IPage page, WrappedLineProbe line, double xBias)
    {
        var rect = await page.EvaluateAsync<RectProbe>(
            """
            line => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const block = host?.querySelector(`[data-block-id="${cssEscape(line.blockId)}"]`);
                block?.scrollIntoView?.({ block: 'center', inline: 'nearest' });
                let rects = collectBlockLineRects(block);
                let chosen = rects[line.lineOrdinal] || rects[0] || line.rect;
                if (chosen && (chosen.y < 48 || chosen.y + chosen.height > window.innerHeight - 48)) {
                    scrollByDelta(block, chosen.y + chosen.height / 2 - window.innerHeight * 0.5);
                    rects = collectBlockLineRects(block);
                    chosen = rects[line.lineOrdinal] || rects[0] || chosen;
                }
                return chosen;

                function collectBlockLineRects(blockElement) {
                    const result = [];
                    if (!blockElement) return result;
                    const walker = document.createTreeWalker(blockElement, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            return node.nodeValue?.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                        }
                    });
                    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                        const range = document.createRange();
                        range.selectNodeContents(node);
                        for (const rect of Array.from(range.getClientRects())) {
                            if (rect.width <= 8 || rect.height <= 4) continue;
                            result.push(toRect(rect));
                        }
                    }
                    return result.sort((a, b) => a.y - b.y || a.x - b.x);
                }

                function toRect(rect) {
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }

                function cssEscape(value) {
                    return window.CSS?.escape ? window.CSS.escape(value || '') : String(value || '').replace(/"/g, '\\"');
                }

                function scrollByDelta(element, delta) {
                    if (!Number.isFinite(delta) || Math.abs(delta) < 2) return;
                    for (let current = element?.parentElement; current; current = current.parentElement) {
                        if (current.scrollHeight > current.clientHeight + 4) {
                            current.scrollTop += delta;
                        }
                    }
                    window.scrollBy(0, delta);
                }
            }
            """,
            new
            {
                blockId = line.BlockId,
                lineOrdinal = line.LineOrdinal,
                rect = line.Rect
            });
        var scrollYBefore = await GetWindowScrollYAsync(page);
        var x = (float)(rect.X + Math.Min(Math.Max(2, xBias), Math.Max(2, rect.Width - 2)));
        var y = (float)(rect.Y + rect.Height / 2);
        await page.Mouse.ClickAsync(x, y);
        return new ClickedLineProbe(rect, scrollYBefore);
    }

    private async Task ProbeAndAssertCleanAsync(IPage page, string step)
    {
        var probe = await CaptureFrameProbeAsync(page);
        probe.TextRectCount.Should().BeGreaterThan(0, $"{step}: editor must expose measurable text rects");
        probe.Issues.Should().BeEmpty($"{step}: the strict frame probe must stay clean");
    }

    private async Task SavePhase0ArtifactsAsync(IPage page, string testName, string scenario, string expected)
    {
        var directory = TestContext.TestResultsDirectory ?? ".";
        Directory.CreateDirectory(directory);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var safeName = string.Concat(testName.Select(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_'));
        var screenshotPath = Path.Combine(directory, $"{safeName}_{stamp}.png");
        var jsonPath = Path.Combine(directory, $"{safeName}_{stamp}.json");

        await page.ScreenshotAsync(new()
        {
            Path = screenshotPath,
            FullPage = true,
            Type = ScreenshotType.Png
        });
        TestContext.AddResultFile(screenshotPath);

        object? probe = null;
        object? target = null;
        object? timeline = null;
        try { probe = await CaptureFrameProbeAsync(page); } catch (Exception error) { probe = new { error = error.ToString() }; }
        try { target = await CaptureWrappedImageLineTargetAsync(page); } catch (Exception error) { target = new { error = error.ToString() }; }
        try { timeline = await page.EvaluateAsync<string>("() => JSON.stringify(window.__tmStrictEngineTimeline || [])"); } catch (Exception error) { timeline = JsonSerializer.Serialize(new { error = error.ToString() }, JsonOptions); }

        var payload = new
        {
            testName,
            scenario,
            expected,
            capturedAt = DateTimeOffset.Now,
            probe,
            wrappedImageTarget = target,
            timeline
        };

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(payload, JsonOptions));
        TestContext.AddResultFile(jsonPath);
    }

    private static string Printable(char ch)
        => ch == ' ' ? "space" : ch.ToString();

    private sealed class FrameProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("documentText")] public string DocumentText { get; set; } = string.Empty;
        [JsonPropertyName("selectedImageCount")] public int SelectedImageCount { get; set; }
        [JsonPropertyName("issues")] public string[] Issues { get; set; } = [];
        [JsonPropertyName("activeBlock")] public ActiveBlockProbe ActiveBlock { get; set; } = new();
        [JsonPropertyName("selection")] public SelectionFrameProbe Selection { get; set; } = new();
        [JsonPropertyName("textRectCount")] public int TextRectCount { get; set; }
        [JsonPropertyName("imageRectCount")] public int ImageRectCount { get; set; }
        [JsonPropertyName("runtimeDebugJson")] public string RuntimeDebugJson { get; set; } = "null";
        [JsonPropertyName("timelineJson")] public string TimelineJson { get; set; } = "[]";
    }

    private sealed class ActiveBlockProbe
    {
        [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("lineCount")] public int LineCount { get; set; }
        [JsonPropertyName("lineRects")] public RectProbe[] LineRects { get; set; } = [];
    }

    private sealed class SelectionFrameProbe
    {
        [JsonPropertyName("isCollapsed")] public bool IsCollapsed { get; set; }
        [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
        [JsonPropertyName("offset")] public int Offset { get; set; }
        [JsonPropertyName("caretRect")] public RectProbe CaretRect { get; set; } = new();
    }

    private sealed class WrappedImageLineTarget
    {
        [JsonPropertyName("imageBlockId")] public string ImageBlockId { get; set; } = string.Empty;
        [JsonPropertyName("imageRect")] public RectProbe ImageRect { get; set; } = new();
        [JsonPropertyName("lines")] public WrappedLineProbe[] Lines { get; set; } = [];
    }

    private sealed class WrappedLineProbe
    {
        [JsonPropertyName("nodeIndex")] public int NodeIndex { get; set; }
        [JsonPropertyName("lineOrdinal")] public int LineOrdinal { get; set; }
        [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("rect")] public RectProbe Rect { get; set; } = new();
    }

    private sealed record ClickedLineProbe(RectProbe Rect, double ScrollYBefore);

    private sealed class RectProbe
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
    }
}
