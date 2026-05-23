using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for marker layering and overlay geometry.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase9E2ETests : DocumentEditorE2ETestBase
{
    private const string CommentMarkerSelector = "[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor[data-comment-id='recovery-comment-visible']";
    private const string RevisionMarkerSelector = "[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision[data-revision-id='recovery-revision-insertion']";
    private const string SelectionBlockId = "recovery-comment-paragraph";
    private const string SelectionPhrase = "This paragraph";

    [TestMethod]
    public async Task RecoveryOverlayLayering_UsesStableZIndexTokensAndNonTextOverlayNodes()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var layering = await ReadLayeringProbeAsync(page);

        Assert.IsTrue(layering.Text < layering.Revision, "Revision decorations must render above document text.");
        Assert.IsTrue(layering.Revision < layering.Comment, "Comment highlights must render above revision decorations.");
        Assert.IsTrue(layering.Comment < layering.Search, "Search highlights must win the combined marker color priority.");
        Assert.IsTrue(layering.Search < layering.Selection, "Native/engine selection highlights must render above passive markers.");
        Assert.IsTrue(layering.Selection < layering.ObjectOverlay, "Object handles must render above text selection overlays.");
        Assert.IsTrue(layering.ObjectOverlay < layering.FloatingUi, "Floating UI must stay on the top decoration layer.");
        Assert.IsTrue(layering.OverlayTextContentLength == 0, "Render overlay nodes must never add textContent to document text probes.");
        Assert.IsTrue(layering.OverlayNodesAreIgnoredByTextProbes, "Render overlay nodes must be explicitly marked as non-text probe content.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryOverlayLayering_UsesStableZIndexTokensAndNonTextOverlayNodes));
    }

    [TestMethod]
    public async Task RecoveryCommentMarker_RectStaysInsideTextLine()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var probe = await ReadMarkerLineProbeAsync(page, CommentMarkerSelector);

        Assert.IsTrue(probe.Found, "The recovery comment marker must be rendered.");
        Assert.IsTrue(probe.MarkerWidth > 1, "The comment marker must have visible inline geometry.");
        Assert.IsTrue(probe.MarkerInsideLine, $"Comment marker {FormatRect(probe.Marker)} must stay inside line rect {FormatRect(probe.Line)}.");
        Assert.IsTrue(probe.MarkerHeight <= probe.LineHeight * 1.6 + 2,
            $"Comment marker height {probe.MarkerHeight:0.##} must not inflate line height {probe.LineHeight:0.##}.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryCommentMarker_RectStaysInsideTextLine));
    }

    [TestMethod]
    public async Task RecoveryRevisionMarker_RectStaysInsideTextLine()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var probe = await ReadMarkerLineProbeAsync(page, RevisionMarkerSelector);

        Assert.IsTrue(probe.Found, "The recovery revision marker must be rendered.");
        Assert.IsTrue(probe.MarkerWidth > 1, "The revision marker must have visible inline geometry.");
        Assert.IsTrue(probe.MarkerInsideLine, $"Revision marker {FormatRect(probe.Marker)} must stay inside line rect {FormatRect(probe.Line)}.");
        Assert.IsTrue(probe.MarkerHeight <= probe.LineHeight * 1.6 + 2,
            $"Revision marker height {probe.MarkerHeight:0.##} must not inflate line height {probe.LineHeight:0.##}.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryRevisionMarker_RectStaysInsideTextLine));
    }

    [TestMethod]
    public async Task RecoveryMarkerDecoration_DoesNotShiftAdjacentText()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var probe = await ReadMarkerShiftProbeAsync(page, CommentMarkerSelector);

        Assert.IsTrue(probe.Found, "The recovery comment marker must be rendered.");
        Assert.IsTrue(probe.FoundAdjacentText, "The recovery paragraph must contain adjacent text around the comment marker.");
        Assert.IsTrue(probe.AdjacentDeltaX <= 1, $"Marker decoration shifted adjacent text horizontally by {probe.AdjacentDeltaX:0.##} px.");
        Assert.IsTrue(probe.AdjacentDeltaY <= 1, $"Marker decoration shifted adjacent text vertically by {probe.AdjacentDeltaY:0.##} px.");
        Assert.IsTrue(probe.BlockDeltaHeight <= 1, $"Marker decoration changed block height by {probe.BlockDeltaHeight:0.##} px.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryMarkerDecoration_DoesNotShiftAdjacentText));
    }

    [TestMethod]
    public async Task RecoveryFloatingToolbar_DoesNotCoverSelectedText()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await SelectPhraseWithNativeRangeAsync(page, SelectionBlockId, SelectionPhrase);
        await Assertions.Expect(page.GetByTestId("document-mini-toolbar")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var probe = await ReadFloatingToolbarOverlapProbeAsync(page);

        Assert.IsTrue(probe.SelectionRectCount > 0, "The browser selection must expose readable text rects.");
        Assert.IsTrue(probe.Toolbar.Width > 1, "The floating toolbar must have visible geometry.");
        Assert.IsTrue(probe.MaxSelectionOverlapArea <= 1,
            $"Floating toolbar {FormatRect(probe.Toolbar)} must not cover selected text; overlap area was {probe.MaxSelectionOverlapArea:0.##} px.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryFloatingToolbar_DoesNotCoverSelectedText));
    }

    [TestMethod]
    public async Task RecoveryImageToolbar_StaysOutsideReadableText()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var figure = page.Locator("[data-testid='document-wysiwyg-host'] figure.tm-wysiwyg-image[data-block-id]").First;
        await Assertions.Expect(figure).ToBeVisibleAsync(new() { Timeout = 5000 });
        await figure.ScrollIntoViewIfNeededAsync();
        await figure.ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-image-wrap-panel")).ToBeVisibleAsync(new() { Timeout = 5000 });

        var probe = await ReadImageToolbarTextOverlapProbeAsync(page);

        Assert.IsTrue(probe.Toolbar.Width > 1, "The image toolbar must have visible geometry.");
        Assert.IsTrue(probe.TextRectCount > 0, "The page must expose readable text rects for collision detection.");
        Assert.IsTrue(probe.MaxTextOverlapArea <= 1,
            $"Image toolbar {FormatRect(probe.Toolbar)} must stay outside readable text; overlap area was {probe.MaxTextOverlapArea:0.##} px.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryImageToolbar_StaysOutsideReadableText));
    }

    private static Task<LayeringProbe> ReadLayeringProbeAsync(IPage page)
        => page.EvaluateAsync<LayeringProbe>(
            """
            () => {
                const editor = document.querySelector('.tm-document-editor') || document.documentElement;
                const style = getComputedStyle(editor);
                const layer = name => Number.parseInt(style.getPropertyValue(name), 10) || 0;
                const overlays = Array.from(document.querySelectorAll('[data-render-overlay]'));
                return {
                    text: layer('--tm-document-z-text'),
                    revision: layer('--tm-document-z-revision'),
                    comment: layer('--tm-document-z-comment'),
                    search: layer('--tm-document-z-search'),
                    selection: layer('--tm-document-z-selection'),
                    objectOverlay: layer('--tm-document-z-object-overlay'),
                    floatingUi: layer('--tm-document-z-floating-ui') || 1000,
                    overlayTextContentLength: overlays.reduce((total, node) => total + (node.textContent || '').length, 0),
                    overlayNodesAreIgnoredByTextProbes: overlays.every(node =>
                        node.getAttribute('aria-hidden') === 'true'
                        && node.getAttribute('data-text-probe-ignore') === 'true'
                        && Array.from(node.querySelectorAll('*')).every(child =>
                            child.getAttribute('aria-hidden') === 'true'
                            && child.getAttribute('data-text-probe-ignore') === 'true'))
                };
            }
            """);

    private static Task<MarkerLineProbe> ReadMarkerLineProbeAsync(IPage page, string selector)
        => page.EvaluateAsync<MarkerLineProbe>(
            """
            (selector) => {
                const marker = document.querySelector(selector);
                if (!marker) return { found: false };
                marker.scrollIntoView({ block: 'center', inline: 'nearest' });
                const markerRect = toRect(marker.getBoundingClientRect());
                const block = marker.closest('.tm-wysiwyg-block, [data-block-id]');
                const blockStyle = getComputedStyle(block || marker);
                const markerStyle = getComputedStyle(marker);
                const parsedLineHeight = Number.parseFloat(markerStyle.lineHeight) || Number.parseFloat(blockStyle.lineHeight) || markerRect.height;
                const range = document.createRange();
                range.selectNodeContents(marker);
                const lineRect = Array.from(range.getClientRects())
                    .filter(rect => rect.width > 0.5 && rect.height > 0.5)
                    .map(toRect)[0] || markerRect;
                return {
                    found: true,
                    marker: markerRect,
                    line: lineRect,
                    lineHeight: parsedLineHeight,
                    markerWidth: markerRect.width,
                    markerHeight: markerRect.height,
                    markerInsideLine: markerRect.x >= lineRect.x - 2
                        && markerRect.y >= lineRect.y - 2
                        && markerRect.x + markerRect.width <= lineRect.x + lineRect.width + 2
                        && markerRect.y + markerRect.height <= lineRect.y + lineRect.height + 2
                };

                function toRect(rect) {
                    return {
                        x: Number(rect.x || rect.left || 0),
                        y: Number(rect.y || rect.top || 0),
                        width: Number(rect.width || 0),
                        height: Number(rect.height || 0)
                    };
                }
            }
            """,
            selector);

    private static Task<MarkerShiftProbe> ReadMarkerShiftProbeAsync(IPage page, string selector)
        => page.EvaluateAsync<MarkerShiftProbe>(
            """
            (selector) => {
                const marker = document.querySelector(selector);
                if (!marker) return { found: false };
                marker.scrollIntoView({ block: 'center', inline: 'nearest' });
                const block = marker.closest('.tm-wysiwyg-block, [data-block-id]');
                const adjacentBefore = readAdjacentTextRect(marker, block);
                const blockBefore = toRect(block.getBoundingClientRect());
                const hadSelected = marker.classList.contains('tm-document-inline--comment-anchor--selected');
                const hadActive = marker.classList.contains('tm-wysiwyg-marker--comment-active');
                marker.classList.add('tm-document-inline--comment-anchor--selected', 'tm-wysiwyg-marker--comment-active');
                marker.getBoundingClientRect();
                const adjacentAfter = readAdjacentTextRect(marker, block);
                const blockAfter = toRect(block.getBoundingClientRect());
                if (!hadSelected) marker.classList.remove('tm-document-inline--comment-anchor--selected');
                if (!hadActive) marker.classList.remove('tm-wysiwyg-marker--comment-active');
                return {
                    found: true,
                    foundAdjacentText: !!adjacentBefore && !!adjacentAfter,
                    adjacentDeltaX: adjacentBefore && adjacentAfter ? Math.abs(adjacentAfter.x - adjacentBefore.x) : 999,
                    adjacentDeltaY: adjacentBefore && adjacentAfter ? Math.abs(adjacentAfter.y - adjacentBefore.y) : 999,
                    blockDeltaHeight: Math.abs(blockAfter.height - blockBefore.height)
                };

                function readAdjacentTextRect(target, scope) {
                    const walker = document.createTreeWalker(scope, NodeFilter.SHOW_TEXT, {
                        acceptNode(node) {
                            return node.nodeValue && node.nodeValue.trim().length > 0
                                ? NodeFilter.FILTER_ACCEPT
                                : NodeFilter.FILTER_REJECT;
                        }
                    });
                    const before = [];
                    let seenMarker = false;
                    while (walker.nextNode()) {
                        const node = walker.currentNode;
                        if (target.contains(node)) {
                            seenMarker = true;
                            continue;
                        }
                        if (seenMarker) return firstCharacterRect(node);
                        before.push(node);
                    }
                    return before.length ? lastCharacterRect(before[before.length - 1]) : null;
                }

                function firstCharacterRect(node) {
                    const index = Math.max(0, node.nodeValue.search(/\S/));
                    const range = document.createRange();
                    range.setStart(node, index);
                    range.setEnd(node, Math.min(node.nodeValue.length, index + 1));
                    return Array.from(range.getClientRects()).map(toRect).find(rect => rect.width > 0.5 && rect.height > 0.5) || null;
                }

                function lastCharacterRect(node) {
                    const text = node.nodeValue || '';
                    const index = Math.max(0, text.search(/\S\s*$/));
                    const range = document.createRange();
                    range.setStart(node, index);
                    range.setEnd(node, Math.min(text.length, index + 1));
                    return Array.from(range.getClientRects()).map(toRect).find(rect => rect.width > 0.5 && rect.height > 0.5) || null;
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
            """,
            selector);

    private static Task SelectPhraseWithNativeRangeAsync(IPage page, string blockId, string phrase)
        => page.EvaluateAsync(
            """
            ({ blockId, phrase }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const escaped = window.CSS?.escape ? window.CSS.escape(blockId) : String(blockId).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
                const block = Array.from(host?.querySelectorAll(`[data-block-id="${escaped}"], [data-render-block-id="${escaped}"]`) || [])
                    .find(node => {
                        const rect = node.getBoundingClientRect();
                        const style = getComputedStyle(node);
                        return rect.width > 1 && rect.height > 1 && style.visibility !== 'hidden' && style.display !== 'none' && !node.closest('.tm-wysiwyg-page--virtual');
                    });
                if (!block) throw new Error(`Could not find block '${blockId}'.`);
                block.scrollIntoView({ block: 'center', inline: 'nearest' });

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
                const range = document.createRange();
                const startPosition = positionAt(start);
                const endPosition = positionAt(end);
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
            }
            """,
            new { blockId, phrase });

    private static Task<FloatingToolbarOverlapProbe> ReadFloatingToolbarOverlapProbeAsync(IPage page)
        => page.EvaluateAsync<FloatingToolbarOverlapProbe>(
            """
            () => {
                const selection = window.getSelection();
                const rects = selection && selection.rangeCount
                    ? Array.from(selection.getRangeAt(0).getClientRects()).filter(rect => rect.width > 0.5 && rect.height > 0.5).map(toRect)
                    : [];
                const toolbar = document.querySelector('[data-testid="document-mini-toolbar"]');
                const toolbarRect = toolbar ? toRect(toolbar.getBoundingClientRect()) : zeroRect();
                return {
                    toolbar: toolbarRect,
                    selectionRectCount: rects.length,
                    maxSelectionOverlapArea: Math.max(0, ...rects.map(rect => overlapArea(rect, toolbarRect)))
                };

                function overlapArea(a, b) {
                    const width = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
                    const height = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                    return width * height;
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
            }
            """);

    private static Task<ImageToolbarTextOverlapProbe> ReadImageToolbarTextOverlapProbeAsync(IPage page)
        => page.EvaluateAsync<ImageToolbarTextOverlapProbe>(
            """
            () => {
                const toolbar = document.querySelector('[data-testid="document-image-wrap-panel"], [data-testid="document-wysiwyg-image-toolbar"], .tm-wysiwyg-image-toolbar');
                const toolbarRect = toolbar ? toRect(toolbar.getBoundingClientRect()) : zeroRect();
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const textRects = [];
                const walker = document.createTreeWalker(host, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        const parent = node.parentElement;
                        if (!node.nodeValue || !node.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
                        if (!parent || parent.closest('figure, [role="menu"], [data-testid*="toolbar"], .tm-document-editor__floating-root, [data-testid="document-side-panel"]')) {
                            return NodeFilter.FILTER_REJECT;
                        }
                        return NodeFilter.FILTER_ACCEPT;
                    }
                });
                while (walker.nextNode()) {
                    const range = document.createRange();
                    range.selectNodeContents(walker.currentNode);
                    Array.from(range.getClientRects()).forEach(rect => {
                        if (rect.width > 0.5 && rect.height > 0.5) textRects.push(toRect(rect));
                    });
                }
                return {
                    toolbar: toolbarRect,
                    textRectCount: textRects.length,
                    maxTextOverlapArea: Math.max(0, ...textRects.map(rect => overlapArea(rect, toolbarRect)))
                };

                function overlapArea(a, b) {
                    const width = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x));
                    const height = Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
                    return width * height;
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
            }
            """);

    private static string FormatRect(DocumentEditorRectProbe rect)
        => $"x={rect.X:0.##}, y={rect.Y:0.##}, w={rect.Width:0.##}, h={rect.Height:0.##}";

    private sealed class LayeringProbe
    {
        [JsonPropertyName("text")] public int Text { get; set; }
        [JsonPropertyName("revision")] public int Revision { get; set; }
        [JsonPropertyName("comment")] public int Comment { get; set; }
        [JsonPropertyName("search")] public int Search { get; set; }
        [JsonPropertyName("selection")] public int Selection { get; set; }
        [JsonPropertyName("objectOverlay")] public int ObjectOverlay { get; set; }
        [JsonPropertyName("floatingUi")] public int FloatingUi { get; set; }
        [JsonPropertyName("overlayTextContentLength")] public int OverlayTextContentLength { get; set; }
        [JsonPropertyName("overlayNodesAreIgnoredByTextProbes")] public bool OverlayNodesAreIgnoredByTextProbes { get; set; }
    }

    private sealed class MarkerLineProbe
    {
        [JsonPropertyName("found")] public bool Found { get; set; }
        [JsonPropertyName("marker")] public DocumentEditorRectProbe Marker { get; set; } = new();
        [JsonPropertyName("line")] public DocumentEditorRectProbe Line { get; set; } = new();
        [JsonPropertyName("lineHeight")] public double LineHeight { get; set; }
        [JsonPropertyName("markerWidth")] public double MarkerWidth { get; set; }
        [JsonPropertyName("markerHeight")] public double MarkerHeight { get; set; }
        [JsonPropertyName("markerInsideLine")] public bool MarkerInsideLine { get; set; }
    }

    private sealed class MarkerShiftProbe
    {
        [JsonPropertyName("found")] public bool Found { get; set; }
        [JsonPropertyName("foundAdjacentText")] public bool FoundAdjacentText { get; set; }
        [JsonPropertyName("adjacentDeltaX")] public double AdjacentDeltaX { get; set; }
        [JsonPropertyName("adjacentDeltaY")] public double AdjacentDeltaY { get; set; }
        [JsonPropertyName("blockDeltaHeight")] public double BlockDeltaHeight { get; set; }
    }

    private sealed class FloatingToolbarOverlapProbe
    {
        [JsonPropertyName("toolbar")] public DocumentEditorRectProbe Toolbar { get; set; } = new();
        [JsonPropertyName("selectionRectCount")] public int SelectionRectCount { get; set; }
        [JsonPropertyName("maxSelectionOverlapArea")] public double MaxSelectionOverlapArea { get; set; }
    }

    private sealed class ImageToolbarTextOverlapProbe
    {
        [JsonPropertyName("toolbar")] public DocumentEditorRectProbe Toolbar { get; set; } = new();
        [JsonPropertyName("textRectCount")] public int TextRectCount { get; set; }
        [JsonPropertyName("maxTextOverlapArea")] public double MaxTextOverlapArea { get; set; }
    }
}
