using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict browser tests for document editor UX polish contracts.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase23E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Engine_UxPolishContractsWorkInBrowserDom()
    {
        var page = await OpenDocumentEditorAsync(1280, 900);
        var demoTopLevelImageBlocks = await ReadDocumentEditorTopLevelImageBlockCountAsync(page);
        var demoDrawingRuns = await ReadDocumentEditorDrawingRunCountAsync(page);

        demoTopLevelImageBlocks.Should().Be(0, "demo data must exercise the drawing run/object image model");
        demoDrawingRuns.Should().BeGreaterThan(0, "demo data must contain drawing runs for image workflows");

        var setup = await page.EvaluateAsync<Phase23SetupProbe>(
            """
            () => {
                const shell = document.createElement('div');
                shell.setAttribute('data-testid', 'phase23-ux-host');
                shell.className = 'tm-document-editor';
                shell.style.cssText = 'position:fixed;left:340px;top:24px;width:760px;min-height:620px;background:white;z-index:20000;pointer-events:auto;';
                const host = document.createElement('div');
                host.setAttribute('data-testid', 'document-wysiwyg-host');
                shell.appendChild(host);
                document.body.appendChild(shell);

                const engine = window.tmDocumentEditorEngine;
                const instanceId = engine.create(host, { InstanceId: 'phase23-e2e' }, null);
                const dataUrl = 'data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22160%22 height=%2290%22 viewBox=%220 0 160 90%22%3E%3Crect width=%22160%22 height=%2290%22 rx=%226%22 fill=%22%232563eb%22/%3E%3Ctext x=%2280%22 y=%2252%22 text-anchor=%22middle%22 font-size=%2218%22 fill=%22white%22%3EIMG%3C/text%3E%3C/svg%3E';
                engine.loadDocument(instanceId, {
                    Document: {
                        DocumentId: 'phase23-e2e-doc',
                        Blocks: [
                            {
                                Id: 'p1',
                                Type: 'Paragraph',
                                Content: {
                                    Type: 'Paragraph',
                                    Inlines: [
                                        { Id: 'r1', Text: 'Hello wrapped drawing text ', Marks: [{ Type: 'Bold' }] },
                                        {
                                            $type: 'drawing',
                                            Id: 'drawing-run-1',
                                            ObjectId: 'phase23-object',
                                            Kind: 0,
                                            Source: 0,
                                            Url: dataUrl,
                                            AltText: 'Browser drawing image',
                                            Caption: 'Browser drawing caption',
                                            Size: { Width: 160, Height: 90 },
                                            Layout: {
                                                Kind: 1,
                                                Anchor: { BlockId: 'p1', Offset: 5, InlineIndex: 1, Region: 'Body', MoveWithText: true, FixedOnPage: false },
                                                Position: { HorizontalRelativeTo: 2, VerticalRelativeTo: 3, HorizontalAlignment: 0, X: 0, Y: 0 },
                                                Wrap: { Mode: 1, DistanceLeft: 0, DistanceRight: 14, DistanceTop: 0, DistanceBottom: 6 },
                                                Transform: { Width: 160, Height: 90, LockAspectRatio: true },
                                                Stacking: { ZIndex: 0, AllowOverlap: false }
                                            }
                                        },
                                        { Id: 'r2', Text: 'This paragraph has enough readable text to wrap around the selected drawing object, then reflow when the user changes wrapping through the visible layout bubble.' }
                                    ]
                                }
                            }
                        ],
                        Comments: [{ Id: 'comment1', Range: { BlockId: 'p1', Start: 0, End: 5 } }],
                        Revisions: [{ Id: 'rev1', Type: 'Insertion', Status: 'Pending', AffectedRange: { BlockId: 'p1', Start: 0, End: 5 } }]
                    }
                });

                const tracker = engine.uxPolish.createVisualStabilityTracker({ maxToolbarDelta: 1 });
                const stable = tracker.record(
                    { paragraphKey: 'p1', pageKey: 'page1', toolbarTop: 80, selectionRelevant: true, floatingOpen: true, commandValue: true },
                    { paragraphKey: 'p1', pageKey: 'page1', toolbarTop: 80, selectionRelevant: true, floatingOpen: true, commandValue: true },
                    'typing');
                const text = engine.uxPolish.previewImmediateTextEdit({
                    text: 'HelloWorld',
                    selection: { blockId: 'p1', offset: 5 },
                    inputType: 'insertText',
                    data: ' '
                });
                const chrome = engine.uxPolish.createObjectChromeModel({
                    objectRect: { X: 520, Y: 160, Width: 220, Height: 124 },
                    captionRect: { X: 520, Y: 290, Width: 220, Height: 24 },
                    toolbarSize: { Width: 288, Height: 34 },
                    viewport: { X: 0, Y: 0, Width: 1280, Height: 900 },
                    sidePanelRect: { X: 900, Y: 0, Width: 320, Height: 900 }
                });

                engine.restoreSelection(instanceId, { blockId: 'p1', offset: 2, isCollapsed: true });
                const paragraphPanel = engine.getSidePanelSyncState(instanceId);

                return {
                    instanceId,
                    objectId: 'phase23-object',
                    stableOk: stable.ok === true,
                    spaceVisible: text.spaceVisibleImmediately === true && text.visibleText === 'Hello World',
                    chromeReadable: chrome.selectionOutline.clean === true && chrome.allHandlesLargeEnough === true && chrome.handlesAvoidCaption === true,
                    chromeAvoidsSidePanel: chrome.toolbar.avoidsSidePanel === true,
                    paragraphBold: paragraphPanel.properties?.formatting?.bold === true,
                    activeRevisionId: paragraphPanel.revision?.activeRevisionIds?.[0] || '',
                    activeCommentId: paragraphPanel.comments?.activeCommentIds?.[0] || ''
                };
            }
            """);

        setup.StableOk.Should().BeTrue();
        setup.SpaceVisible.Should().BeTrue();
        setup.ChromeReadable.Should().BeTrue();
        setup.ChromeAvoidsSidePanel.Should().BeTrue();
        setup.ParagraphBold.Should().BeTrue();
        setup.ActiveRevisionId.Should().Be("rev1");
        setup.ActiveCommentId.Should().Be("comment1");

        const string sandboxSelector = "[data-testid='phase23-ux-host'] [data-testid='document-wysiwyg-host']";
        var drawingSelector = $"{sandboxSelector} [data-object-id='{setup.ObjectId}']";
        await Assertions.Expect(page.Locator(drawingSelector).First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        var beforeClick = await ReadDocumentEditorImageDiagnosticsAsync(page, setup.ObjectId, sandboxSelector);
        beforeClick.TopLevelImageBlockCount.Should().Be(0, beforeClick.Debug);
        beforeClick.DrawingRunCount.Should().Be(1, beforeClick.Debug);
        beforeClick.LineIntervals.Should().HaveCountGreaterThan(1, "square wrapped text should create multiple real text intervals beside the drawing");

        var clickPoint = await ReadPhase23ObjectClickPointAsync(page, sandboxSelector, setup.ObjectId);
        clickPoint.Found.Should().BeTrue(clickPoint.Debug);
        await page.Mouse.ClickAsync((float)clickPoint.X, (float)clickPoint.Y);
        await Assertions.Expect(page.Locator($"{sandboxSelector} [data-testid='document-wysiwyg-object-layout-bubble']").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        var afterRealClick = await ReadDocumentEditorImageDiagnosticsAsync(page, setup.ObjectId, sandboxSelector);
        afterRealClick.SelectionMode.Should().Be("Object", afterRealClick.Debug);
        afterRealClick.ActiveImageId.Should().Be(setup.ObjectId, afterRealClick.Debug);
        afterRealClick.ImageToolbarVisible.Should().BeFalse("image object selection should use the layout bubble, not the old image toolbar");

        var beforeWrapFootprint = await ReadPhase23WrapFootprintAsync(page, sandboxSelector, setup.ObjectId);
        beforeWrapFootprint.WrapMode.Should().Be("Square");

        await page.Locator($"{sandboxSelector} [data-testid='document-wysiwyg-layout-bubble-break']").ClickAsync();
        await page.WaitForTimeoutAsync(100);

        var afterWrapFootprint = await ReadPhase23WrapFootprintAsync(page, sandboxSelector, setup.ObjectId);
        afterWrapFootprint.WrapMode.Should().Be("TopBottom");
        afterWrapFootprint.TextLineFingerprint.Should().NotBe(
            beforeWrapFootprint.TextLineFingerprint,
            "changing wrap through the visible bubble must produce a real DOM text-line reflow");

        var beforeResizeState = await ReadPhase23DrawingStateAsync(page, setup.InstanceId, setup.ObjectId);
        beforeResizeState.WrapMode.Should().Be("TopBottom");

        clickPoint = await ReadPhase23ObjectClickPointAsync(page, sandboxSelector, setup.ObjectId);
        clickPoint.Found.Should().BeTrue(clickPoint.Debug);
        await page.Mouse.ClickAsync((float)clickPoint.X, (float)clickPoint.Y);
        await Assertions.Expect(page.Locator($"{sandboxSelector} [data-testid='document-wysiwyg-object-resize-handle-se']").First)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        var resize = await page.EvaluateAsync<Phase23ResizeProbe>(
            """
            ({ instanceId, objectId, width, height }) => {
                const engine = window.tmDocumentEditorEngine;
                const result = engine.applyCommand(instanceId, 'setImageSize', {
                    objectId,
                    width,
                    height
                });
                const state = readDrawingState(engine.getDocumentSnapshot(instanceId).csharpDocument, objectId, engine.getDebugSnapshot(instanceId));
                return {
                    ok: result?.ok !== false,
                    width: state.width,
                    height: state.height,
                    undoDepth: state.undoDepth
                };

                function readDrawingState(document, id, debug) {
                    const drawing = findDrawing(document, id);
                    const layout = drawing?.Layout || drawing?.layout || {};
                    const transform = layout.Transform || layout.transform || {};
                    return {
                        width: Number(transform.Width ?? transform.width ?? drawing?.Size?.Width ?? drawing?.size?.width ?? 0) || 0,
                        height: Number(transform.Height ?? transform.height ?? drawing?.Size?.Height ?? drawing?.size?.height ?? 0) || 0,
                        undoDepth: Number(debug?.undoDepth || 0) || 0
                    };
                }

                function findDrawing(document, id) {
                    const blocks = document?.Blocks || document?.blocks || [];
                    for (const block of Array.isArray(blocks) ? blocks : []) {
                        const content = block.Content || block.content || {};
                        const inlines = content.Inlines || content.inlines || [];
                        for (const inline of Array.isArray(inlines) ? inlines : []) {
                            if (String(inline.ObjectId || inline.objectId || '') === id) return inline;
                        }
                    }
                    return null;
                }
            }
            """,
            new
            {
                instanceId = setup.InstanceId,
                objectId = setup.ObjectId,
                width = beforeResizeState.Width + 42,
                height = beforeResizeState.Height + 20
            });
        resize.Ok.Should().BeTrue();
        resize.Width.Should().BeGreaterThan(beforeResizeState.Width);
        resize.UndoDepth.Should().BeGreaterThan(beforeResizeState.UndoDepth);
        var afterResizeState = await ReadPhase23DrawingStateAsync(page, setup.InstanceId, setup.ObjectId);
        afterResizeState.Width.Should().BeGreaterThan(beforeResizeState.Width);
        afterResizeState.UndoDepth.Should().BeGreaterThan(beforeResizeState.UndoDepth);

        var undo = await page.EvaluateAsync<Phase23UndoProbe>(
            """
            ({ instanceId, objectId }) => {
                const engine = window.tmDocumentEditorEngine;
                const result = engine.applyCommand(instanceId, 'undo', {});
                const state = readDrawingState(engine.getDocumentSnapshot(instanceId).csharpDocument, objectId, engine.getDebugSnapshot(instanceId));
                return {
                    ok: result?.ok !== false,
                    transactionType: result?.transaction?.type || '',
                    width: state.width,
                    undoDepth: state.undoDepth
                };

                function readDrawingState(document, id, debug) {
                    const drawing = findDrawing(document, id);
                    const layout = drawing?.Layout || drawing?.layout || {};
                    const transform = layout.Transform || layout.transform || {};
                    return {
                        width: Number(transform.Width ?? transform.width ?? drawing?.Size?.Width ?? drawing?.size?.width ?? 0) || 0,
                        undoDepth: Number(debug?.undoDepth || 0) || 0
                    };
                }

                function findDrawing(document, id) {
                    const blocks = [];
                    appendBlocks(blocks, document?.Blocks || document?.blocks);
                    for (const block of blocks) {
                        const content = block.Content || block.content || {};
                        const inlines = content.Inlines || content.inlines || [];
                        for (const inline of inlines) {
                            if (String(inline.ObjectId || inline.objectId || '') === id) return inline;
                        }
                    }
                    return null;
                }

                function appendBlocks(target, blocks) {
                    for (const block of Array.isArray(blocks) ? blocks : []) target.push(block);
                }
            }
            """,
            new { instanceId = setup.InstanceId, objectId = setup.ObjectId });
        undo.Ok.Should().BeTrue();
        undo.Width.Should().BeApproximately(beforeResizeState.Width, 0.5);
        undo.UndoDepth.Should().Be(beforeResizeState.UndoDepth);

        var reload = await page.EvaluateAsync<Phase23ReloadProbe>(
            """
            ({ instanceId, objectId }) => {
                const engine = window.tmDocumentEditorEngine;
                const snapshot = engine.getDocumentSnapshot(instanceId).csharpDocument;
                engine.loadDocument(instanceId, { Document: snapshot });
                const reloaded = engine.getDocumentSnapshot(instanceId).csharpDocument;
                const state = readDrawingState(reloaded, objectId, engine.getDebugSnapshot(instanceId));
                const dispose = engine.dispose(instanceId);
                document.querySelector('[data-testid="phase23-ux-host"]')?.remove();
                return {
                    topLevelImageBlockCount: countImageBlocks(reloaded),
                    drawingRunCount: countDrawingRuns(reloaded),
                    objectId: state.objectId,
                    anchorBlockId: state.anchorBlockId,
                    anchorInlineIndex: state.anchorInlineIndex,
                    wrapMode: state.wrapMode,
                    width: state.width,
                    disposed: dispose?.ok === true
                };

                function readDrawingState(document, id) {
                    const drawing = findDrawing(document, id);
                    const layout = drawing?.Layout || drawing?.layout || {};
                    const anchor = layout.Anchor || layout.anchor || {};
                    const wrap = layout.Wrap || layout.wrap || {};
                    const transform = layout.Transform || layout.transform || {};
                    return {
                        objectId: String(drawing?.ObjectId || drawing?.objectId || ''),
                        anchorBlockId: String(anchor.BlockId || anchor.blockId || ''),
                        anchorInlineIndex: Number(anchor.InlineIndex ?? anchor.inlineIndex ?? -1),
                        wrapMode: wrapModeName(wrap.Mode ?? wrap.mode),
                        width: Number(transform.Width ?? transform.width ?? drawing?.Size?.Width ?? drawing?.size?.width ?? 0) || 0
                    };
                }

                function findDrawing(document, id) {
                    const blocks = collectBlocks(document);
                    for (const block of blocks) {
                        const content = block.Content || block.content || {};
                        const inlines = content.Inlines || content.inlines || [];
                        for (const inline of Array.isArray(inlines) ? inlines : []) {
                            if (String(inline.ObjectId || inline.objectId || '') === id) return inline;
                        }
                    }
                    return null;
                }

                function countDrawingRuns(document) {
                    let count = 0;
                    for (const block of collectBlocks(document)) {
                        const content = block.Content || block.content || {};
                        for (const inline of Array.isArray(content.Inlines || content.inlines) ? (content.Inlines || content.inlines) : []) {
                            const type = String(inline.$type || inline.Type || inline.type || '').toLowerCase();
                            if (type === 'drawing' || inline.ObjectId || inline.objectId) count++;
                        }
                    }
                    return count;
                }

                function countImageBlocks(document) {
                    return collectBlocks(document).filter(block => {
                        const type = block.Type ?? block.type;
                        const contentType = block.Content?.$type ?? block.content?.$type;
                        return type === 5 || String(type).toLowerCase() === 'image' || String(contentType).toLowerCase() === 'image';
                    }).length;
                }

                function collectBlocks(document) {
                    const blocks = [];
                    appendBlocks(blocks, document?.Blocks || document?.blocks);
                    return blocks;
                }

                function appendBlocks(target, blocks) {
                    for (const block of Array.isArray(blocks) ? blocks : []) target.push(block);
                }

                function wrapModeName(value) {
                    const names = ['Inline', 'Square', 'Tight', 'Through', 'TopBottom', 'BehindText', 'InFrontOfText'];
                    return typeof value === 'number' ? names[value] || String(value) : String(value || '');
                }
            }
            """,
            new { instanceId = setup.InstanceId, objectId = setup.ObjectId });
        reload.TopLevelImageBlockCount.Should().Be(0);
        reload.DrawingRunCount.Should().Be(1);
        reload.ObjectId.Should().Be(setup.ObjectId);
        reload.AnchorBlockId.Should().Be("p1");
        reload.AnchorInlineIndex.Should().Be(1);
        reload.WrapMode.Should().Be("TopBottom");
        reload.Width.Should().BeApproximately(beforeResizeState.Width, 0.5);
        reload.Disposed.Should().BeTrue();
    }

    private static Task<Phase23ClickPointProbe> ReadPhase23ObjectClickPointAsync(IPage page, string hostSelector, string objectId)
        => page.EvaluateAsync<Phase23ClickPointProbe>(
            """
            ({ hostSelector, objectId }) => {
                const host = document.querySelector(hostSelector);
                const escaped = CSS.escape(objectId);
                const target = host?.querySelector(`[data-testid="document-wysiwyg-object-layer-item"][data-object-id="${escaped}"], [data-testid="document-wysiwyg-anchored-drawing"][data-object-id="${escaped}"], [data-object-id="${escaped}"]`);
                const rect = target?.getBoundingClientRect();
                const points = [];
                if (rect) {
                    for (const px of [0.2, 0.35, 0.5, 0.65, 0.8]) {
                        for (const py of [0.2, 0.35, 0.5, 0.65, 0.8]) {
                            points.push({ x: rect.left + rect.width * px, y: rect.top + rect.height * py });
                        }
                    }
                }

                for (const point of points) {
                    const top = document.elementFromPoint(point.x, point.y);
                    const object = top?.closest?.(`[data-object-id="${escaped}"]`);
                    if (object && host?.contains(object)) {
                        return {
                            found: true,
                            x: point.x,
                            y: point.y,
                            debug: JSON.stringify({ rect: toRect(rect), top: describe(top), object: describe(object) })
                        };
                    }
                }

                return {
                    found: false,
                    x: rect ? rect.left + rect.width / 2 : 0,
                    y: rect ? rect.top + rect.height / 2 : 0,
                    debug: JSON.stringify({
                        rect: toRect(rect),
                        topAtCenter: describe(rect ? document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2) : null),
                        target: describe(target)
                    })
                };

                function describe(node) {
                    if (!node) return '';
                    return `${node.tagName || ''}.${String(node.className || '').replace(/\s+/g, '.')}`;
                }

                function toRect(value) {
                    return value
                        ? { x: value.x, y: value.y, width: value.width, height: value.height }
                        : null;
                }
            }
            """,
            new { hostSelector, objectId });

    private static Task<Phase23WrapFootprintProbe> ReadPhase23WrapFootprintAsync(IPage page, string hostSelector, string objectId)
        => page.EvaluateAsync<Phase23WrapFootprintProbe>(
            """
            ({ hostSelector, objectId }) => {
                const host = document.querySelector(hostSelector);
                const object = host?.querySelector(`[data-object-id="${CSS.escape(objectId)}"]`);
                const wrapMode = object?.getAttribute('data-wrap-mode') || '';
                const rects = [];
                const walker = document.createTreeWalker(host, NodeFilter.SHOW_TEXT, {
                    acceptNode(node) {
                        if (!node.nodeValue?.trim()) return NodeFilter.FILTER_REJECT;
                        const parent = node.parentElement;
                        if (!parent || parent.closest('figure, [data-testid*="toolbar"], .tm-wysiwyg-page__layer--object, .tm-wysiwyg-page__layer--selection')) {
                            return NodeFilter.FILTER_REJECT;
                        }
                        return NodeFilter.FILTER_ACCEPT;
                    }
                });
                for (let node = walker.nextNode(); node; node = walker.nextNode()) {
                    const range = document.createRange();
                    range.selectNodeContents(node);
                    for (const rect of Array.from(range.getClientRects())) {
                        if (rect.width > 0.5 && rect.height > 0.5) {
                            rects.push([
                                Math.round(rect.x),
                                Math.round(rect.y),
                                Math.round(rect.width),
                                Math.round(rect.height)
                            ]);
                        }
                    }
                }
                return {
                    wrapMode,
                    textLineFingerprint: JSON.stringify(rects)
                };
            }
            """,
            new { hostSelector, objectId });

    private static Task<Phase23DrawingStateProbe> ReadPhase23DrawingStateAsync(IPage page, string instanceId, string objectId)
        => page.EvaluateAsync<Phase23DrawingStateProbe>(
            """
            ({ instanceId, objectId }) => {
                const engine = window.tmDocumentEditorEngine;
                const snapshot = engine.getDocumentSnapshot(instanceId).csharpDocument;
                const debug = engine.getDebugSnapshot(instanceId);
                const drawing = findDrawing(snapshot, objectId);
                const layout = drawing?.Layout || drawing?.layout || {};
                const anchor = layout.Anchor || layout.anchor || {};
                const wrap = layout.Wrap || layout.wrap || {};
                const transform = layout.Transform || layout.transform || {};
                return {
                    objectId: String(drawing?.ObjectId || drawing?.objectId || ''),
                    anchorBlockId: String(anchor.BlockId || anchor.blockId || ''),
                    anchorInlineIndex: Number(anchor.InlineIndex ?? anchor.inlineIndex ?? -1),
                    wrapMode: wrapModeName(wrap.Mode ?? wrap.mode),
                    width: Number(transform.Width ?? transform.width ?? drawing?.Size?.Width ?? drawing?.size?.width ?? 0) || 0,
                    height: Number(transform.Height ?? transform.height ?? drawing?.Size?.Height ?? drawing?.size?.height ?? 0) || 0,
                    undoDepth: Number(debug?.undoDepth || 0) || 0
                };

                function findDrawing(document, id) {
                    const blocks = document?.Blocks || document?.blocks || [];
                    for (const block of Array.isArray(blocks) ? blocks : []) {
                        const content = block.Content || block.content || {};
                        const inlines = content.Inlines || content.inlines || [];
                        for (const inline of Array.isArray(inlines) ? inlines : []) {
                            if (String(inline.ObjectId || inline.objectId || '') === id) return inline;
                        }
                    }
                    return null;
                }

                function wrapModeName(value) {
                    const names = ['Inline', 'Square', 'Tight', 'Through', 'TopBottom', 'BehindText', 'InFrontOfText'];
                    return typeof value === 'number' ? names[value] || String(value) : String(value || '');
                }
            }
            """,
            new { instanceId, objectId });

    private sealed class Phase23ClickPointProbe
    {
        [JsonPropertyName("found")] public bool Found { get; set; }
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("debug")] public string Debug { get; set; } = string.Empty;
    }

    private sealed class Phase23WrapFootprintProbe
    {
        [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
        [JsonPropertyName("textLineFingerprint")] public string TextLineFingerprint { get; set; } = string.Empty;
    }

    private sealed class Phase23SetupProbe
    {
        [JsonPropertyName("instanceId")] public string InstanceId { get; set; } = string.Empty;
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("stableOk")] public bool StableOk { get; set; }
        [JsonPropertyName("spaceVisible")] public bool SpaceVisible { get; set; }
        [JsonPropertyName("chromeReadable")] public bool ChromeReadable { get; set; }
        [JsonPropertyName("chromeAvoidsSidePanel")] public bool ChromeAvoidsSidePanel { get; set; }
        [JsonPropertyName("paragraphBold")] public bool ParagraphBold { get; set; }
        [JsonPropertyName("activeRevisionId")] public string ActiveRevisionId { get; set; } = string.Empty;
        [JsonPropertyName("activeCommentId")] public string ActiveCommentId { get; set; } = string.Empty;
    }

    private sealed class Phase23DrawingStateProbe
    {
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
        [JsonPropertyName("anchorInlineIndex")] public int AnchorInlineIndex { get; set; }
        [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
    }

    private sealed class Phase23UndoProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("transactionType")] public string TransactionType { get; set; } = string.Empty;
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
    }

    private sealed class Phase23ResizeProbe
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
        [JsonPropertyName("undoDepth")] public int UndoDepth { get; set; }
    }

    private sealed class Phase23ReloadProbe
    {
        [JsonPropertyName("topLevelImageBlockCount")] public int TopLevelImageBlockCount { get; set; }
        [JsonPropertyName("drawingRunCount")] public int DrawingRunCount { get; set; }
        [JsonPropertyName("objectId")] public string ObjectId { get; set; } = string.Empty;
        [JsonPropertyName("anchorBlockId")] public string AnchorBlockId { get; set; } = string.Empty;
        [JsonPropertyName("anchorInlineIndex")] public int AnchorInlineIndex { get; set; }
        [JsonPropertyName("wrapMode")] public string WrapMode { get; set; } = string.Empty;
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("disposed")] public bool Disposed { get; set; }
    }
}
