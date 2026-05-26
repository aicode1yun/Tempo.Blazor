using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase8AnchoredLayoutJavaScriptTests
{
    [Fact]
    public async Task Phase8_DocumentLayoutPublishesAnchoredSquareDrawingObjectAndExclusion()
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
            const model = hooks.importFromCSharpJson(createDocument('Square', 'phase8-square-object', { width: 80, height: 60 }));
            const block = model.body.blocks[0];
            const engine = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 12 });
            const layout = engine.layoutDocument(model, {
                width: 400,
                height: 300,
                marginTop: 40,
                marginRight: 40,
                marginBottom: 40,
                marginLeft: 40,
                blockGap: 0,
                lineGap: 0,
                minReadableWidth: 12
            });

            const object = layout.objects.find(item => item.objectId === 'phase8-square-object');
            assert.ok(object, 'anchored drawing run must be published as a page object');
            assert.strictEqual(object.inlineObject, false);
            assert.strictEqual(object.isInline, false);
            assert.strictEqual(object.wrapMode, 'Square');
            assert.strictEqual(object.layer, 'object');
            assert.strictEqual(object.anchorBlockId, 'p1');
            assert.strictEqual(object.anchorInlineIndex, 1);
            assert.strictEqual(object.rect.width, 80);
            assert.strictEqual(object.rect.height, 60);
            assert.strictEqual(object.rect.x, 40);
            assert.strictEqual(object.rect.y, 40);
            assert.strictEqual(object.createsTextExclusion, true);

            const page = layout.pages[0];
            const exclusion = page.exclusions.find(item => item.objectId === 'phase8-square-object');
            assert.ok(exclusion, 'square anchored drawing must create an exclusion zone');
            assert.strictEqual(exclusion.kind, 'rectangular');
            assert.strictEqual(exclusion.wrapMode, 'Square');

            const paragraph = layout.blocks.find(item => item.blockId === 'p1');
            assert.ok(paragraph, 'paragraph layout must still be produced');
            assert.strictEqual(paragraph.inlineObjects.length, 0, 'anchored drawings must not be reported as inline objects');
            assert.strictEqual(paragraph.segments.some(segment => segment.kind === 'drawing'), false, 'anchored drawings must not become text-flow segments');
            assert.ok(paragraph.lines[0].availableIntervals[0].x > page.bodyFrame.x, 'first line interval should start after the square exclusion');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "square-layout");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase8_DocumentLayoutTopBottomDrawingBlocksFullLineRange()
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
            const model = hooks.importFromCSharpJson(createDocument('TopBottom', 'phase8-top-bottom-object', { width: 100, height: 50 }));
            const engine = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 12 });
            const layout = engine.layoutDocument(model, {
                width: 400,
                height: 300,
                marginTop: 40,
                marginRight: 40,
                marginBottom: 40,
                marginLeft: 40,
                blockGap: 0,
                lineGap: 0,
                minReadableWidth: 12
            });

            const page = layout.pages[0];
            const object = layout.objects.find(item => item.objectId === 'phase8-top-bottom-object');
            const exclusion = page.exclusions.find(item => item.objectId === 'phase8-top-bottom-object');
            assert.ok(object, 'top-bottom drawing run must be published as an object');
            assert.ok(exclusion, 'top-bottom drawing run must create an exclusion');
            assert.strictEqual(exclusion.kind, 'fullWidth');
            assert.strictEqual(exclusion.rect.x, page.bodyFrame.x);
            assert.strictEqual(exclusion.rect.width, page.bodyFrame.width);

            const paragraph = layout.blocks.find(item => item.blockId === 'p1');
            assert.ok(paragraph.lines[0].rect.y >= object.rect.y + object.rect.height, 'top-bottom exclusion should move the first editable line below the object');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "top-bottom-layout");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase8_DocumentLayoutBehindAndInFrontDrawingsDoNotCreateExclusions()
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
            const model = hooks.importFromCSharpJson(createLayerDocument());
            const engine = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 12 });
            const layout = engine.layoutDocument(model, {
                width: 400,
                height: 300,
                marginTop: 40,
                marginRight: 40,
                marginBottom: 40,
                marginLeft: 40,
                blockGap: 0,
                lineGap: 0,
                minReadableWidth: 12
            });

            const behind = layout.objects.find(item => item.objectId === 'phase8-behind-object');
            const front = layout.objects.find(item => item.objectId === 'phase8-front-object');
            assert.ok(behind, 'behind-text drawing must be published as object');
            assert.ok(front, 'in-front drawing must be published as object');
            assert.strictEqual(behind.layer, 'behind-text');
            assert.strictEqual(front.layer, 'in-front-of-text');
            assert.strictEqual(layout.pages[0].exclusions.length, 0, 'behind/front drawings must not block text flow');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "paint-layers");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase8_RenderParagraphRunsRendersAnchoredDrawingAsFloatingObject()
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
            const model = hooks.importFromCSharpJson(createDocument('Square', 'phase8-render-object', { width: 72, height: 44 }));
            const block = model.body.blocks[0];
            const html = hooks.renderParagraphRunsHtmlForTest({
                model,
                selection: {
                    selectionMode: 'Object',
                    activeObjectId: 'phase8-render-object',
                    objectSelection: { objectId: 'phase8-render-object', anchorBlockId: 'p1' }
                }
            }, block, 1, 1);

            assert.ok(html.indexOf('Alpha ') >= 0, 'text before anchored drawing must be rendered');
            assert.ok(html.indexOf(' omega') >= 0, 'text after anchored drawing must be rendered');
            assert.ok(html.indexOf('document-wysiwyg-anchored-drawing') >= 0, 'anchored drawing must render as floating object');
            assert.strictEqual(html.indexOf('document-wysiwyg-inline-drawing'), -1, 'anchored drawing must not render through the inline drawing element');
            assert.ok(html.indexOf('tm-wysiwyg-anchored-drawing') >= 0, 'anchored drawing class must be present');
            assert.strictEqual(html.indexOf('tm-wysiwyg-image--float-left'), -1, 'square-left anchored drawing must not use legacy float-left CSS');
            assert.strictEqual(html.indexOf('float:left'), -1, 'square-left anchored drawing must not use browser float layout');
            assert.ok(html.indexOf('position:absolute') >= 0, 'anchored drawing visual position should come from the object layer geometry');
            assert.ok(html.indexOf('data-object-id="phase8-render-object"') >= 0, 'rendered object id must be exposed');
            assert.ok(html.indexOf('data-anchor-block-id="p1"') >= 0, 'anchor block id must be exposed');
            assert.ok(html.indexOf('data-wrap-mode="Square"') >= 0, 'wrap mode must be exposed');
            assert.ok(html.indexOf('tm-wysiwyg-object--selected') >= 0, 'selected anchored drawing must expose object selection styling');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "render");
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
            process?.WaitForExit(2000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase8-{scenario}-{Guid.NewGuid():N}.js");
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

        function wrapModeValue(name) {
            if (name === 'Square') return 1;
            if (name === 'TopBottom') return 4;
            if (name === 'BehindText') return 5;
            if (name === 'InFrontOfText') return 6;
            return 0;
        }

        function drawingRun(id, objectId, wrapMode, size) {
            return {
                $type: 'drawing',
                Id: id,
                ObjectId: objectId,
                Kind: 0,
                Source: 0,
                Url: '/' + objectId + '.png',
                AltText: objectId,
                Size: { Width: size.width, Height: size.height },
                Layout: {
                    Kind: 1,
                    Wrap: { Mode: wrapModeValue(wrapMode) },
                    Anchor: { BlockId: 'p1', Offset: 6, InlineIndex: 1 },
                    Position: {
                        HorizontalRelativeTo: 2,
                        HorizontalAlignment: 0,
                        VerticalRelativeTo: 3,
                        VerticalAlignment: 1,
                        X: 0,
                        Y: 0
                    },
                    Transform: { Width: size.width, Height: size.height }
                }
            };
        }

        function createDocument(wrapMode, objectId, size) {
            return {
                DocumentId: 'image-drawing-phase8',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'before', Text: 'Alpha ' },
                                drawingRun('drawing-run', objectId, wrapMode, size),
                                { $type: 'text', Id: 'after', Text: ' omega text that stays editable after the anchored object' }
                            ]
                        }
                    }
                ]
            };
        }

        function createLayerDocument() {
            const behind = drawingRun('behind-run', 'phase8-behind-object', 'BehindText', { width: 80, height: 40 });
            const front = drawingRun('front-run', 'phase8-front-object', 'InFrontOfText', { width: 80, height: 40 });
            return {
                DocumentId: 'image-drawing-phase8-layers',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'before', Text: 'Alpha ' },
                                behind,
                                { $type: 'text', Id: 'middle', Text: ' middle ' },
                                front,
                                { $type: 'text', Id: 'after', Text: ' omega' }
                            ]
                        }
                    }
                ]
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
