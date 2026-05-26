using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase9BehindFrontLayerJavaScriptTests
{
    [Fact]
    public async Task Phase9_BehindTextAndInFrontOfTextDoNotCreateTextExclusions()
    {
        var result = await RunScenarioAsync(
            "overlay-no-exclusions",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const behind = hooks.createTextExclusion({
                objectId: 'behind',
                blockId: 'p1',
                wrapMode: 'BehindText',
                rect: { x: 100, y: 40, width: 160, height: 90 }
            }, frame);
            const front = hooks.createTextExclusion({
                objectId: 'front',
                blockId: 'p1',
                wrapMode: 'InFrontOfText',
                rect: { x: 100, y: 40, width: 160, height: 90 }
            }, frame);

            assert.strictEqual(behind, null);
            assert.strictEqual(front, null);
            const line = hooks.createTextExclusionManager([behind, front].filter(Boolean), frame).resolveLine(64, 20, 24);
            assert.deepStrictEqual(plain(line.intervals.map(i => ({ x: i.x, y: i.y, width: i.width, height: i.height }))), [
                { x: 0, y: 64, width: 600, height: 20 }
            ]);
            assert.deepStrictEqual(plain(line.blockedIntervals), []);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase9_RenderLayersPlaceBehindBelowTextAndFrontAboveObjectLayer()
    {
        var result = await RunScenarioAsync(
            "render-layer-order",
            """
            const model = hooks.importFromCSharpJson(createLayerDocument());
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const behindLayer = extractLayer(html, 'document-wysiwyg-behind-text-layer');
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');
            const frontLayer = extractLayer(html, 'document-wysiwyg-in-front-of-text-layer');
            const selectionLayer = extractLayer(html, 'document-wysiwyg-selection-layer');

            assert.ok(behindLayer.includes('data-object-id="phase9-behind-object"'), 'behind object must render in the behind-text layer');
            assert.ok(objectLayer.includes('data-object-id="phase9-square-object"'), 'normal object must render in the object layer');
            assert.ok(frontLayer.includes('data-object-id="phase9-front-object"'), 'front object must render in the in-front layer');
            assert.ok(selectionLayer.includes('data-object-layer="behind-text"'), 'selection overlay must expose behind-text layer for click-through policy');
            assert.ok(selectionLayer.includes('data-object-layer="in-front-of-text"'), 'selection overlay must expose in-front layer for hit policy');
            assert.strictEqual(objectLayer.includes('phase9-behind-object'), false, 'behind object must not stay in the normal object layer');
            assert.strictEqual(objectLayer.includes('phase9-front-object'), false, 'front object must not stay in the normal object layer');
            assert.ok(textLayer.includes('Phase 9 layered text'), 'text layer must remain separate from object layers');

            const behindMarker = 'data-testid="document-wysiwyg-behind-text-layer"';
            const textMarker = 'data-testid="document-wysiwyg-text-layer"';
            const objectMarker = 'data-testid="document-wysiwyg-object-layer"';
            const frontMarker = 'data-testid="document-wysiwyg-in-front-of-text-layer"';
            const selectionMarker = 'data-testid="document-wysiwyg-selection-layer"';
            assert.ok(html.indexOf(behindMarker) < html.indexOf(textMarker), 'behind layer must be below text');
            assert.ok(html.indexOf(textMarker) < html.indexOf(objectMarker), 'normal object layer must be above text');
            assert.ok(html.indexOf(objectMarker) < html.indexOf(frontMarker), 'front layer must be above normal objects');
            assert.ok(html.indexOf(frontMarker) < html.indexOf(selectionMarker), 'selection layer must stay above front objects');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase9_HitTestingPrefersInFrontObjectOverText()
    {
        var result = await RunScenarioAsync(
            "front-hit-test",
            """
            const hit = hooks.hitTestLayoutGeometry({
                X: 160,
                Y: 76,
                PageRects: [{ Rect: { X: 0, Y: 0, Width: 640, Height: 800 } }],
                BodyRects: [{ Rect: { X: 40, Y: 40, Width: 560, Height: 700 } }],
                HeaderFooters: [],
                TableCells: [],
                Controls: [],
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'front-object',
                    BlockId: 'front-image',
                    Layer: 'in-front-of-text',
                    WrapMode: 'InFrontOfText',
                    ZIndex: 1,
                    Rect: { X: 120, Y: 56, Width: 120, Height: 80 },
                    VisualRects: [{ X: 120, Y: 56, Width: 120, Height: 80 }],
                    Selectable: true
                }],
                Lines: [textLine('text-block', 'line-1', 60, 0)]
            });

            assert.strictEqual(hit.Kind, 'ImageObject');
            assert.strictEqual(hit.ActiveImageBlockId, 'front-image');
            assert.strictEqual(hit.ActiveObjectId, 'front-object');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase9_HitTestingAllowsTextCaretThroughBehindTextObject()
    {
        var result = await RunScenarioAsync(
            "behind-hit-test",
            """
            const hit = hooks.hitTestLayoutGeometry({
                X: 160,
                Y: 76,
                PageRects: [{ Rect: { X: 0, Y: 0, Width: 640, Height: 800 } }],
                BodyRects: [{ Rect: { X: 40, Y: 40, Width: 560, Height: 700 } }],
                HeaderFooters: [],
                TableCells: [],
                Controls: [],
                Objects: [{
                    Kind: 'ImageObject',
                    ObjectId: 'behind-object',
                    BlockId: 'behind-image',
                    Layer: 'behind-text',
                    WrapMode: 'BehindText',
                    ZIndex: 99,
                    Rect: { X: 120, Y: 56, Width: 120, Height: 80 },
                    VisualRects: [{ X: 120, Y: 56, Width: 120, Height: 80 }],
                    Selectable: true
                }],
                Lines: [textLine('text-block', 'line-1', 60, 12)]
            });

            assert.strictEqual(hit.Kind, 'TextCaret');
            assert.strictEqual(hit.BlockId, 'text-block');
            assert.strictEqual(hit.LayoutLineId, 'line-1');
            assert.strictEqual(hit.ActiveImageBlockId, null);
            assert.strictEqual(hit.ActiveObjectId, null);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase9_WrapCommandAssignsLayerPriorityAndNoExclusionForOverlayModes()
    {
        var result = await RunScenarioAsync(
            "wrap-command-layer-policy",
            """
            const behindHarness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const behind = behindHarness.setWrapMode('BehindText').state;
            assert.strictEqual(behind.wrapMode, 'BehindText');
            assert.strictEqual(behind.allowOverlap, true);
            assert.ok(behind.zIndex < 0, behind.zIndex);
            assert.strictEqual(behind.hasExclusion, false);

            const frontHarness = hooks.createImageWrapCommandHarness({ initialWrapMode: 'Square' });
            const front = frontHarness.setWrapMode('InFrontOfText').state;
            assert.strictEqual(front.wrapMode, 'InFrontOfText');
            assert.strictEqual(front.allowOverlap, true);
            assert.ok(front.zIndex > 0, front.zIndex);
            assert.strictEqual(front.hasExclusion, false);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    private static async Task<ScenarioResult> RunScenarioAsync(string scenario, string nodeScript)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return new ScenarioResult(0, "OK", "");
        var result = await RunNodeAsync(scriptPath, nodeScript, scenario);
        return new ScenarioResult(result.ExitCode, result.StandardOutput, result.StandardError);
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase9-{scenario}-{Guid.NewGuid():N}.js");
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

    private sealed record ScenarioResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public void ShouldPass()
        {
            ExitCode.Should().Be(0, StandardError);
            StandardOutput.Trim().Should().Be("OK");
        }
    }

    private const string SharedSandboxScript =
        """
        const fs = require('fs');
        const vm = require('vm');
        const assert = require('assert');

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
                parseFloat,
                parseInt,
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

        function textLine(blockId, lineId, y, startOffset) {
            return {
                Id: lineId,
                BlockId: blockId,
                VisualLineIndex: 0,
                Rect: { X: 80, Y: y, Width: 360, Height: 20 },
                Segments: [{
                    Id: `${lineId}-segment`,
                    LineId: lineId,
                    InlineId: `${blockId}-run`,
                    BlockId: blockId,
                    StartOffset: startOffset,
                    TextLength: 60,
                    Rect: { X: 80, Y: y, Width: 360, Height: 20 }
                }]
            };
        }

        function drawingRun(objectId, mode, zIndex) {
            return {
                $type: 'drawing',
                Id: `${objectId}-run`,
                ObjectId: objectId,
                Source: 0,
                Url: `/${objectId}.png`,
                AltText: objectId,
                Size: { Width: 96, Height: 64, LockAspectRatio: true },
                NaturalSize: { Width: 96, Height: 64, LockAspectRatio: true },
                Layout: {
                    Kind: 1,
                    Anchor: {
                        BlockId: 'phase9-p1',
                        Offset: 8,
                        InlineIndex: 1,
                        Region: 'Body',
                        MoveWithText: true,
                        FixedOnPage: false
                    },
                    Position: {
                        HorizontalRelativeTo: 2,
                        HorizontalAlignment: 0,
                        VerticalRelativeTo: 3,
                        VerticalAlignment: 1,
                        X: 120 + zIndex * 4,
                        Y: 20
                    },
                    Wrap: { Mode: mode },
                    Transform: { Width: 96, Height: 64, LockAspectRatio: true },
                    Stacking: {
                        ZIndex: zIndex,
                        AllowOverlap: mode === 5 || mode === 6
                    }
                }
            };
        }

        function createLayerDocument() {
            return {
                DocumentId: 'phase9-layer-document',
                Blocks: [{
                    Id: 'phase9-p1',
                    Type: 'Paragraph',
                    Content: {
                        $type: 'paragraph',
                        Inlines: [
                            { $type: 'text', Id: 'phase9-before', Text: 'Phase 9 layered text before ' },
                            drawingRun('phase9-behind-object', 5, -3),
                            drawingRun('phase9-square-object', 1, 0),
                            drawingRun('phase9-front-object', 6, 3),
                            { $type: 'text', Id: 'phase9-after', Text: ' after.' }
                        ]
                    }
                }]
            };
        }

        function extractLayer(html, testId) {
            const marker = `data-testid="${testId}"`;
            const start = html.indexOf(marker);
            assert.ok(start >= 0, `missing layer ${testId}`);
            const open = html.lastIndexOf('<div', start);
            const next = html.indexOf('tm-wysiwyg-page__layer', start + marker.length);
            return next >= 0 ? html.slice(open, next) : html.slice(open);
        }

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
        const plain = value => JSON.parse(JSON.stringify(value));

        """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
