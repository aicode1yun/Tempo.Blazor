using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase10CaretHitTestingJavaScriptTests
{
    [Fact]
    public async Task Phase10_HitTestGeometry_UsesWrappedCaretIntervalsAndKeepsObjectBodySelectable()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hitTest = sandbox.window.tmDocumentEditorEngine.__testHooks.hitTestLayoutGeometry;
            const base = {
                RootRect: { X: 0, Y: 0, Width: 640, Height: 760 },
                PageRects: [{ PageIndex: 0, Rect: { X: 0, Y: 0, Width: 620, Height: 740 } }],
                BodyRects: [{ PageIndex: 0, Rect: { X: 40, Y: 80, Width: 520, Height: 600 } }],
                HeaderFooters: [],
                TableCells: [],
                Controls: [],
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'phase10-square-object',
                    BlockId: 'p1',
                    WrapMode: 'Square',
                    Layer: 'object',
                    ZIndex: 1,
                    Rect: { X: 120, Y: 120, Width: 60, Height: 72 },
                    VisualRects: [{ X: 120, Y: 120, Width: 60, Height: 72 }],
                    Selectable: true
                }],
                Lines: [{
                    Id: 'p1-line-0',
                    BlockId: 'p1',
                    VisualLineIndex: 0,
                    Rect: { X: 40, Y: 120, Width: 360, Height: 18 },
                    AvailableIntervals: [
                        { Id: 'p1-line-0-left', BlockId: 'p1', LineId: 'p1-line-0', X: 40, Y: 120, Width: 80, Height: 18, StartOffset: 0, EndOffset: 8 },
                        { Id: 'p1-line-0-right', BlockId: 'p1', LineId: 'p1-line-0', X: 190, Y: 120, Width: 210, Height: 18, StartOffset: 8, EndOffset: 20 }
                    ],
                    Segments: [{
                        Id: 'p1-segment-0',
                        LineId: 'p1-line-0',
                        BlockId: 'p1',
                        StartOffset: 0,
                        TextLength: 20,
                        Rect: { X: 40, Y: 120, Width: 360, Height: 18 }
                    }]
                }, {
                    Id: 'p1-line-empty',
                    BlockId: 'p1',
                    VisualLineIndex: 1,
                    Rect: { X: 190, Y: 160, Width: 210, Height: 18 },
                    AvailableIntervals: [{
                        Id: 'p1-line-empty-right',
                        BlockId: 'p1',
                        LineId: 'p1-line-empty',
                        X: 190,
                        Y: 160,
                        Width: 210,
                        Height: 18,
                        StartOffset: 8,
                        EndOffset: 8,
                        CollapsedOffset: 8,
                        Empty: true
                    }],
                    Segments: []
                }]
            };

            const at = (x, y, overrides = {}) => hitTest({ ...base, ...overrides, X: x, Y: y });

            const left = at(60, 128);
            assert.strictEqual(left.Kind, 'TextCaret');
            assert.strictEqual(left.BlockId, 'p1');
            assert.strictEqual(left.LayoutLineId, 'p1-line-0');
            assert.strictEqual(left.LayoutIntervalId, 'p1-line-0-left');
            assert.strictEqual(left.Offset, 2);

            const right = at(295, 128);
            assert.strictEqual(right.Kind, 'TextCaret');
            assert.strictEqual(right.LayoutLineId, 'p1-line-0');
            assert.strictEqual(right.LayoutIntervalId, 'p1-line-0-right');
            assert.strictEqual(right.Offset, 14);

            const empty = at(260, 168);
            assert.strictEqual(empty.Kind, 'TextCaret');
            assert.strictEqual(empty.LayoutLineId, 'p1-line-empty');
            assert.strictEqual(empty.LayoutIntervalId, 'p1-line-empty-right');
            assert.strictEqual(empty.Offset, 8);

            const object = at(130, 128);
            assert.strictEqual(object.Kind, 'ImageObject');
            assert.strictEqual(object.ActiveObjectId, 'phase10-square-object');

            const forbiddenTopBottomGap = at(260, 220, {
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'phase10-top-bottom-object',
                    BlockId: 'p2',
                    WrapMode: 'TopBottom',
                    Layer: 'object',
                    Rect: { X: 100, Y: 200, Width: 80, Height: 60 },
                    VisualRects: [{ X: 100, Y: 200, Width: 80, Height: 60 }],
                    Selectable: true
                }],
                Lines: []
            });
            assert.notStrictEqual(forbiddenTopBottomGap.Kind, 'TextCaret');

            const topBottomObject = at(120, 220, {
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'phase10-top-bottom-object',
                    BlockId: 'p2',
                    WrapMode: 'TopBottom',
                    Layer: 'object',
                    Rect: { X: 100, Y: 200, Width: 80, Height: 60 },
                    VisualRects: [{ X: 100, Y: 200, Width: 80, Height: 60 }],
                    Selectable: true
                }],
                Lines: []
            });
            assert.strictEqual(topBottomObject.Kind, 'ImageObject');
            assert.strictEqual(topBottomObject.ActiveObjectId, 'phase10-top-bottom-object');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "geometry");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase10_HitTestGeometry_DoesNotFallbackCaretIntoObjectWrapExclusion()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hitTest = sandbox.window.tmDocumentEditorEngine.__testHooks.hitTestLayoutGeometry;
            const base = {
                RootRect: { X: 0, Y: 0, Width: 640, Height: 760 },
                PageRects: [{ PageIndex: 0, Rect: { X: 0, Y: 0, Width: 620, Height: 740 } }],
                BodyRects: [{ PageIndex: 0, Rect: { X: 40, Y: 80, Width: 520, Height: 600 } }],
                HeaderFooters: [],
                TableCells: [],
                Controls: [],
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'phase10-square-object',
                    BlockId: 'p1',
                    WrapMode: 'Square',
                    Layer: 'object',
                    ZIndex: 1,
                    Rect: { X: 150, Y: 118, Width: 80, Height: 72 },
                    VisualRects: [{ X: 150, Y: 118, Width: 80, Height: 72 }],
                    WrapRect: { X: 130, Y: 108, Width: 120, Height: 92 },
                    Selectable: true
                }],
                Lines: [{
                    Id: 'p1-line-0',
                    BlockId: 'p1',
                    VisualLineIndex: 0,
                    Rect: { X: 40, Y: 126, Width: 360, Height: 18 },
                    Segments: [{
                        Id: 'p1-segment-0',
                        LineId: 'p1-line-0',
                        BlockId: 'p1',
                        StartOffset: 0,
                        TextLength: 30,
                        Rect: { X: 40, Y: 126, Width: 360, Height: 18 }
                    }]
                }]
            };

            const visibleObject = hitTest({ ...base, X: 170, Y: 134 });
            assert.strictEqual(visibleObject.Kind, 'ImageObject');
            assert.strictEqual(visibleObject.ActiveObjectId, 'phase10-square-object');

            const leftWrapGap = hitTest({ ...base, X: 140, Y: 134 });
            assert.notStrictEqual(leftWrapGap.Kind, 'TextCaret', 'fallback line hit testing must not place a caret inside the left wrap exclusion gap');
            assert.notStrictEqual(leftWrapGap.Kind, 'ImageObject', 'the invisible wrap gap must not select the image');
            assert.strictEqual(leftWrapGap.Kind, 'Body');

            const rightWrapGap = hitTest({ ...base, X: 240, Y: 134 });
            assert.notStrictEqual(rightWrapGap.Kind, 'TextCaret', 'fallback line hit testing must not place a caret inside the right wrap exclusion gap');
            assert.notStrictEqual(rightWrapGap.Kind, 'ImageObject', 'the invisible wrap gap must not select the image');
            assert.strictEqual(rightWrapGap.Kind, 'Body');

            const realText = hitTest({ ...base, X: 72, Y: 134 });
            assert.strictEqual(realText.Kind, 'TextCaret');
            assert.strictEqual(realText.BlockId, 'p1');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "wrap-exclusion-fallback");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase10_RuntimePointerHitTest_UsesPublishedLineIntervalsAroundAnchoredDrawing()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const hooks = engine.__testHooks;
            const model = hooks.importFromCSharpJson(createDocument());
            const layout = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 12 }).layoutDocument(model, {
                width: 420,
                height: 300,
                marginTop: 40,
                marginRight: 40,
                marginBottom: 40,
                marginLeft: 40,
                blockGap: 0,
                lineGap: 0,
                minReadableWidth: 12
            });

            const object = layout.objects.find(item => item.objectId === 'phase10-runtime-object');
            assert.ok(object, 'anchored drawing must be present in runtime object map');
            assert.ok(Array.isArray(layout.lineIntervals), 'layout must publish a runtime line interval map');
            assert.ok(layout.lineIntervals.length > 0, 'line interval map must not be empty');
            assert.ok(layout.lineIntervals.every(item => item.blockId && item.lineId), 'each interval must carry block and line identity');
            assert.ok(layout.lineIntervals.every(item => Number.isFinite(item.start) && Number.isFinite(item.end)), 'each interval must carry offset bounds');

            const beside = layout.lineIntervals.find(item =>
                item.blockId === 'p1'
                && item.y < object.rect.y + object.rect.height
                && item.y + item.height > object.rect.y
                && (item.x >= object.rect.x + object.rect.width || item.x + item.width <= object.rect.x));
            assert.ok(beside, 'square wrapped image must leave a clickable text interval beside the object');

            const textHit = engine.selection.pointerHitTest(model, layout, beside.x + 6, beside.y + Math.min(8, beside.height / 2));
            assert.strictEqual(textHit.type, 'text');
            assert.strictEqual(textHit.position.blockId, 'p1');
            assert.strictEqual(textHit.lineId, beside.lineId);

            const objectHit = engine.selection.pointerHitTest(model, layout, object.rect.x + object.rect.width / 2, object.rect.y + object.rect.height / 2);
            assert.strictEqual(objectHit.type, 'object');
            assert.strictEqual(objectHit.objectId, 'phase10-runtime-object');
            assert.strictEqual(objectHit.position.blockId, 'p1');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "runtime");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase10_DomLayoutSnapshot_CarriesAnchoredDrawingObjectsAndClickableIntervals()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson(createDocument());
            const imageRect = { x: 120, y: 124, width: 84, height: 58 };
            const paragraphRect = { x: 40, y: 120, width: 340, height: 82 };
            const imageNode = {
                getBoundingClientRect() { return imageRect; }
            };
            const paragraphNode = {
                getBoundingClientRect() { return paragraphRect; },
                querySelector(selector) {
                    return selector.includes('phase10-runtime-object') ? imageNode : null;
                }
            };
            const root = {
                querySelector(selector) {
                    return selector.includes('data-block-id="p1"') ? paragraphNode : null;
                }
            };

            const selection = hooks.createSelectionEngine(root, model);
            const snapshot = selection.buildLayoutSnapshot();
            const object = snapshot.objects.find(item => item.objectId === 'phase10-runtime-object');
            assert.ok(object, 'DOM snapshot must publish anchored drawing as hit-testable object');
            assert.strictEqual(object.blockId, 'p1');
            assert.strictEqual(object.rect.x, imageRect.x);
            assert.strictEqual(object.rect.width, imageRect.width);

            const right = snapshot.lineIntervals.find(item =>
                item.blockId === 'p1'
                && item.x >= imageRect.x + imageRect.width
                && item.y < imageRect.y + imageRect.height
                && item.y + item.height > imageRect.y);
            assert.ok(right, 'DOM snapshot must carve a right-side caret interval next to the anchored image');
            assert.strictEqual(right.lineId, 'p1-line-0');
            assert.ok(right.y <= imageRect.y + imageRect.height / 2 && right.y + right.height >= imageRect.y + imageRect.height / 2,
                'empty side interval should cover the image-height wrapped area, not only the first text baseline');
            assert.ok(Number.isFinite(right.start));
            assert.ok(Number.isFinite(right.end));

            const textHit = selection.hitTest(right.x + 8, imageRect.y + imageRect.height / 2);
            assert.strictEqual(textHit.type, 'text');
            assert.strictEqual(textHit.position.blockId, 'p1');

            const objectHit = selection.hitTest(imageRect.x + 12, imageRect.y + 12);
            assert.strictEqual(objectHit.type, 'object');
            assert.strictEqual(objectHit.objectId, 'phase10-runtime-object');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "dom-snapshot");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static string GetWysiwygScriptPath()
        => Path.Combine(FindRepositoryRoot(), "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");

    private static bool IsNodeAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript,
        string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase10-{scenario}-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, SharedSandboxScript + nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { tempFile, scriptPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, stdout, stderr);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private const string SharedSandboxScript =
        """
        function createSandbox() {
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON,
                Date,
                Math,
                Number,
                String,
                Promise
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            sandbox.window.performance = { now: () => Date.now() };
            return sandbox;
        }

        function createDocument() {
            return {
                DocumentId: 'image-drawing-phase10',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: {
                        $type: 'paragraph',
                        Inlines: [
                            { $type: 'text', Id: 'before', Text: 'Alpha ' },
                            {
                                $type: 'drawing',
                                Id: 'drawing-run',
                                ObjectId: 'phase10-runtime-object',
                                Kind: 0,
                                Source: 0,
                                Url: '/phase10-runtime-object.png',
                                AltText: 'Phase 10 runtime object',
                                Size: { Width: 84, Height: 58 },
                                Layout: {
                                    Kind: 1,
                                    Wrap: { Mode: 1 },
                                    Anchor: { BlockId: 'p1', Offset: 6, InlineIndex: 1 },
                                    Position: {
                                        HorizontalRelativeTo: 2,
                                        HorizontalAlignment: 0,
                                        VerticalRelativeTo: 3,
                                        VerticalAlignment: 1,
                                        X: 0,
                                        Y: 0
                                    },
                                    Transform: { Width: 84, Height: 58 }
                                }
                            },
                            { $type: 'text', Id: 'after', Text: ' omega text remains editable next to the square drawing object.' }
                        ]
                    }
                }]
            };
        }

        """;

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TempoBlazor.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
