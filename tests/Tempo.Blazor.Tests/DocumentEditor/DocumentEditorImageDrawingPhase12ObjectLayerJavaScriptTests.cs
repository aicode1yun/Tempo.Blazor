using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase12ObjectLayerJavaScriptTests
{
    [Fact]
    public void Phase12_CssUnselectedObjectOverlaysDoNotInterceptImageClicks()
    {
        var css = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Tempo.Blazor",
            "wwwroot",
            "css",
            "components",
            "_document-editor.css"));

        Regex.IsMatch(
            css,
            @"\.tm-wysiwyg-page__layer--selection\s*>\s*\.tm-wysiwyg-object-selection-overlay,\s*\.tm-wysiwyg-page__layer--guides\s*>\s*\.tm-wysiwyg-object-guides-overlay\s*\{\s*pointer-events:\s*none;",
            RegexOptions.Singleline).Should().BeTrue("unselected overlay layers must not intercept image clicks");

        Regex.IsMatch(
            css,
            @"\.tm-wysiwyg-page__layer--selection\s*>\s*\.tm-wysiwyg-object-selection-overlay\.tm-wysiwyg-object--selected,\s*\.tm-wysiwyg-page__layer--guides\s*>\s*\.tm-wysiwyg-object-guides-overlay\.tm-wysiwyg-object--selected\s*\{\s*pointer-events:\s*auto;",
            RegexOptions.Singleline).Should().BeTrue("selected overlays still need pointer events for handles and the layout bubble");
    }

    [Fact]
    public async Task Phase12_WysiwygDrawingRendersInObjectLayerWithTextAnchor()
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
            const model = hooks.importFromCSharpJson(createDocument('Square'));
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');

            assert.ok(objectLayer.includes('data-testid="document-wysiwyg-object-layer-item"'), 'object layer must contain the drawing object');
            assert.ok(objectLayer.includes('data-object-id="phase12-object"'), 'object layer must expose object id');
            assert.ok(objectLayer.includes('data-block-id="phase12-object"'), 'object layer keeps the image object id as data-block-id for existing UI selectors');
            assert.ok(objectLayer.includes('data-model-block-id="p1"'), 'object layer must expose the owning paragraph separately from the object id');
            assert.ok(objectLayer.includes('data-anchor-block-id="p1"'), 'object layer must expose the anchor paragraph separately from the object id');
            assert.ok(textLayer.includes('data-testid="document-wysiwyg-drawing-anchor"'), 'text layer must contain a non-editable drawing anchor');
            assert.ok(textLayer.includes('data-object-anchor-id="phase12-object"'), 'text anchor must map to the object id');
            assert.ok(textLayer.includes('data-flow-reservation="true"'), 'text anchor must reserve flow space for wrapped drawings');
            assert.ok(textLayer.includes('float:left'), 'square wrap must reserve a left-floating exclusion in the text layer');
            assert.ok(textLayer.includes('width:96px'), 'flow reservation must use the drawing width');
            assert.ok(textLayer.includes('height:64px'), 'flow reservation must use the drawing height');
            assert.ok(textLayer.includes('visibility:hidden'), 'flow reservation must not duplicate the visible object layer image');
            assert.ok(html.indexOf('document-wysiwyg-text-layer') < html.indexOf('document-wysiwyg-object-layer'), 'text layer should be rendered before object layer');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "object-layer");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_TextLayerDoesNotContainFocusableImageFigure()
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
            const model = hooks.importFromCSharpJson(createDocument('Inline'));
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');

            assert.strictEqual(textLayer.includes('<figure'), false, 'text layer must not render an image figure');
            assert.strictEqual(textLayer.includes('tm-wysiwyg-inline-drawing'), false, 'text layer must not contain the legacy editable inline drawing widget');
            assert.strictEqual(textLayer.includes('role="img"'), false, 'text layer anchor must not be exposed as an image control');
            assert.strictEqual(textLayer.includes('data-object-id='), false, 'text layer must not expose editable object ids');
            assert.ok(textLayer.includes('contenteditable="false"'), 'anchor still needs to be non-editable inside the text surface');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "text-layer");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_SelectionHandlesRenderInSelectionLayerNotTextOrObjectLayer()
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
            const model = hooks.importFromCSharpJson(createDocument('Square'));
            const selection = {
                blockId: 'p1',
                activeObjectId: 'phase12-object',
                objectId: 'phase12-object',
                isObjectSelection: true,
                selectionMode: 'Object',
                objectSelection: { objectId: 'phase12-object', anchorBlockId: 'p1', blockId: 'p1' }
            };
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');
            const selectionLayer = extractLayer(html, 'document-wysiwyg-selection-layer');
            const guidesLayer = extractLayer(html, 'document-wysiwyg-guides-layer');

            assert.ok(selectionLayer.includes('document-wysiwyg-object-selection-overlay'), 'selection overlay must render for the selected object');
            assert.ok(selectionLayer.includes('document-wysiwyg-object-resize-handle-se'), 'resize handles must live in selection layer');
            assert.ok(guidesLayer.includes('document-wysiwyg-object-layout-bubble'), 'layout bubble must live in guides layer');
            assert.strictEqual(textLayer.includes('document-wysiwyg-object-resize-handle'), false, 'text layer must not contain handles');
            assert.strictEqual(objectLayer.includes('document-wysiwyg-object-resize-handle'), false, 'object layer must not contain handles');
            assert.strictEqual(objectLayer.includes('document-wysiwyg-object-layout-bubble'), false, 'object layer must not contain toolbar bubble');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "selection-layer");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_WysiwygDrawingUsesHiddenFlowReservationInsteadOfVisibleTextLayerFigure()
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
            const model = hooks.importFromCSharpJson(createDocument('Square'));
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');

            assert.strictEqual(textLayer.includes('<figure'), false, 'text layer must not duplicate the visible image figure');
            assert.ok(textLayer.includes('tm-wysiwyg-drawing-anchor--flow'), 'text layer must contain a hidden flow reservation anchor');
            assert.ok(textLayer.includes('float:left'), 'the hidden reservation must participate in browser text flow');
            assert.ok(objectLayer.includes('tm-wysiwyg-object-layer-item--wrap-square'), 'wrap mode should be represented on the object-layer item');
            assert.ok(objectLayer.includes('tm-wysiwyg-image--wrap-square'), 'visible object keeps image wrap classes for existing UI selectors');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "flow-reservation");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_TopBottomDrawingAnchorReservesFullLineBand()
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
            const model = hooks.importFromCSharpJson(createDocument('TopBottom'));
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');

            assert.ok(textLayer.includes('data-flow-reservation="true"'), 'top-bottom wrap must reserve space in the text layer');
            assert.ok(textLayer.includes('data-wrap-mode="TopBottom"'), 'anchor must expose the active wrap mode');
            assert.ok(textLayer.includes('display:block'), 'top-bottom reservation must break the line');
            assert.ok(textLayer.includes('clear:both'), 'top-bottom reservation must clear surrounding floats');
            assert.ok(textLayer.includes('float:none'), 'top-bottom reservation must not float beside text');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "top-bottom-reservation");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_FlowReservationIncludesCaptionHeight()
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
            const model = hooks.importFromCSharpJson(createDocument('Square'));
            model.body.blocks[0].content.runs[1].caption = '1234567890123456789012345678901234567890';
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');

            assert.ok(textLayer.includes('data-flow-reservation="true"'), 'wrapped drawings with captions still reserve text flow');
            assert.ok(textLayer.includes('height:88px'), '64px image plus 24px caption estimate must be reserved');
            assert.ok(textLayer.includes('--tm-wysiwyg-drawing-anchor-height:88px'), 'CSS variable should expose the full reserved footprint');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "caption-reservation");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_BehindTextDrawingAnchorDoesNotReserveTextFlow()
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
            const model = hooks.importFromCSharpJson(createDocument('BehindText'));
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');

            assert.strictEqual(textLayer.includes('data-flow-reservation="true"'), false, 'behind-text drawings must not reserve text flow');
            assert.strictEqual(textLayer.includes('float:left'), false, 'behind-text drawings must not float');
            assert.strictEqual(textLayer.includes('float:right'), false, 'behind-text drawings must not float');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "behind-no-reservation");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_StaticImageBlockRendererStillDisplaysImageFallback()
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
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase12-static',
                Blocks: [{
                    Id: 'image-block',
                    Type: 'Image',
                    Content: {
                        $type: 'image',
                        ObjectId: 'static-object',
                        Url: '/static.png',
                        AltText: 'Static fallback image',
                        Layout: { WrapMode: 1, Width: 160, Height: 90 }
                    }
                }]
            });
            const html = hooks.renderEngineBlockHtmlForTest({ model, selection: null, options: {} }, model.body.blocks[0]);

            assert.ok(html.includes('<figure'), 'static fallback must still render a figure');
            assert.ok(html.includes('tm-wysiwyg-image'), 'static fallback must keep image styling');
            assert.ok(html.includes('/static.png'), 'static fallback must display the image source');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "static-fallback");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase12_UpdateImageLayoutUpdatesDrawingRunObjectNotParagraphText()
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
            const model = hooks.importFromCSharpJson(createDocument('Square'));
            model.body.blocks.push({
                id: 'p2',
                type: 'paragraph',
                content: { type: 'paragraph', runs: [{ id: 'p2-r1', kind: 'text', text: 'Target paragraph' }] }
            });

            const before = hooks.findDrawingRunByObjectId(model, 'phase12-object');
            assert.strictEqual(before.object.anchorBlockId, 'p1');
            assert.strictEqual(before.object.width, 96);

            const operation = hooks.createOperation('UpdateImageLayout', {
                target: { blockId: 'p1', objectId: 'phase12-object' },
                layout: hooks.imageObjectToLayout(Object.assign({}, before.object.layout, {
                    anchorBlockId: 'p2',
                    width: 144,
                    height: 88
                })),
                affectedParagraphIds: ['p1', 'p2']
            }, { source: 'phase12-test' });

            const result = hooks.applyOperation(model, operation);
            assert.strictEqual(result.ok, true, JSON.stringify(result.errors || []));
            const after = hooks.findDrawingRunByObjectId(model, 'phase12-object');
            assert.strictEqual(after.blockId, 'p1', 'the drawing run stays in the inline list until a later reflow phase moves it');
            assert.strictEqual(after.object.anchorBlockId, 'p2', 'layout anchor updates to the target paragraph');
            assert.strictEqual(after.object.width, 144);
            assert.strictEqual(after.object.height, 88);
            assert.strictEqual(result.nextSelection.objectSelection.objectId, 'phase12-object');
            assert.strictEqual(result.nextSelection.objectSelection.anchorBlockId, 'p2');

            console.log('OK');
            """;

        var testResult = await RunNodeAsync(scriptPath, nodeScript, "update-drawing-layout");
        testResult.ExitCode.Should().Be(0, testResult.StandardError);
        testResult.StandardOutput.Trim().Should().Be("OK");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase12-{scenario}-{Guid.NewGuid():N}.js");
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

        function extractLayer(html, testId) {
            const marker = `data-testid="${testId}"`;
            const start = html.indexOf(marker);
            if (start < 0) return '';
            const next = html.indexOf('tm-wysiwyg-page__layer', start + marker.length);
            return next < 0 ? html.slice(start) : html.slice(start, next);
        }

        function createDocument(wrapMode) {
            const modes = {
                Inline: 0,
                Square: 1,
                Tight: 2,
                Through: 3,
                TopBottom: 4,
                BehindText: 5,
                InFrontOfText: 6
            };
            const mode = modes[wrapMode] ?? 1;
            const kind = wrapMode === 'Inline' ? 0 : 1;
            return {
                DocumentId: 'image-drawing-phase12',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: {
                        $type: 'paragraph',
                        Inlines: [
                            { $type: 'text', Id: 'r-before', Text: 'Before ' },
                            {
                                $type: 'drawing',
                                Id: 'phase12-run',
                                ObjectId: 'phase12-object',
                                Kind: 0,
                                Source: 0,
                                Url: '/phase12.png',
                                AltText: 'Phase 12 object',
                                Size: { Width: 96, Height: 64 },
                                Layout: {
                                    Kind: kind,
                                    Wrap: { Mode: mode, DistanceLeft: 8, DistanceRight: 8 },
                                    Anchor: { BlockId: 'p1', Offset: 7, InlineIndex: 1, MoveWithText: true },
                                    Position: {
                                        HorizontalRelativeTo: 2,
                                        HorizontalAlignment: 0,
                                        VerticalRelativeTo: 3,
                                        VerticalAlignment: 1,
                                        X: 0,
                                        Y: 0
                                    },
                                    Transform: { Width: 96, Height: 64 }
                                }
                            },
                            { $type: 'text', Id: 'r-after', Text: ' after' }
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
