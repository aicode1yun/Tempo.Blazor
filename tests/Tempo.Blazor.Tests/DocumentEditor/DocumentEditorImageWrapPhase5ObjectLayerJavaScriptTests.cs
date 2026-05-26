using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase5ObjectLayerJavaScriptTests
{
    [Fact]
    public async Task Phase5_FloatingTextAnchorsAreZeroFootprintForEveryWrapMode()
    {
        var result = await RunScenarioAsync(
            "zero-footprint-anchors",
            """
            for (const mode of ['Square', 'Tight', 'Through', 'TopBottom']) {
                const model = hooks.importFromCSharpJson(createDocument(mode));
                const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
                const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');

                assert.ok(textLayer.includes('data-testid="document-wysiwyg-drawing-anchor"'), mode + ' anchor is missing');
                assert.strictEqual(textLayer.includes('data-flow-reservation="true"'), false, mode + ' must not reserve browser flow');
                assert.strictEqual(textLayer.includes('float:left'), false, mode + ' must not float left');
                assert.strictEqual(textLayer.includes('float:right'), false, mode + ' must not float right');
                assert.strictEqual(textLayer.includes('float:none'), false, mode + ' must not emit any float fallback');
                assert.strictEqual(textLayer.includes('shape-outside'), false, mode + ' must not use CSS shape-outside');
                assert.strictEqual(textLayer.includes('clear:both'), false, mode + ' must not clear browser floats');
                assert.strictEqual(textLayer.includes('display:block'), false, mode + ' must not create a full-line browser band');
                assert.ok(textLayer.includes('width:0px'), mode + ' anchor must be zero width');
                assert.ok(textLayer.includes('height:0px'), mode + ' anchor must be zero height');
            }

            const inlineModel = hooks.importFromCSharpJson(createDocument('Inline'));
            const inlineHtml = hooks.renderWysiwygBodyLayersHtmlForTest({ model: inlineModel, selection: null, options: {} }, inlineModel.body.blocks);
            const inlineTextLayer = extractLayer(inlineHtml, 'document-wysiwyg-text-layer');
            assert.ok(inlineTextLayer.includes('tm-wysiwyg-drawing-anchor--inline'), 'inline anchor keeps inline reservation semantics');
            assert.ok(inlineTextLayer.includes('display:inline-block'), 'inline anchor participates in inline layout');
            assert.ok(inlineTextLayer.includes('width:96px'), 'inline anchor reserves object width');
            assert.ok(inlineTextLayer.includes('height:64px'), 'inline anchor reserves object height');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase5_ObjectLayerUsesExplicitObjectRectAndIsOnlyVisibleImage()
    {
        var result = await RunScenarioAsync(
            "explicit-object-rect",
            """
            const model = hooks.importFromCSharpJson(createDocument('Square'));
            model.body.blocks[0].content.runs[1].rect = { x: 123, y: 45, width: 111, height: 77 };
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, selection: null, options: {} }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');

            assert.strictEqual(textLayer.includes('<figure'), false, 'text layer must not duplicate the visible image');
            assert.strictEqual(textLayer.includes('role="img"'), false, 'text anchor must not be the accessible image');
            assert.ok(objectLayer.includes('data-testid="document-wysiwyg-object-layer-item"'), 'object layer item is missing');
            assert.ok(objectLayer.includes('role="img"'), 'object layer owns the accessible visual image');
            assert.ok(objectLayer.includes('data-object-position-source="layout-rect"'), 'explicit rect must be marked as authoritative');
            assert.ok(objectLayer.includes('left:123px'), 'object layer left must come from object rect');
            assert.ok(objectLayer.includes('top:45px'), 'object layer top must come from object rect');
            assert.ok(objectLayer.includes('width:111px'), 'object layer width must come from object rect');
            assert.ok(objectLayer.includes('height:77px'), 'object layer height must come from object rect');
            assert.strictEqual(objectLayer.includes('tm-wysiwyg-image--float-left'), false, 'visible object must not carry legacy float-left class');
            assert.strictEqual(objectLayer.includes('float:left'), false, 'visible object style must not float');

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase5_ObjectLayerCanConsumeLayoutSnapshotRect()
    {
        var result = await RunScenarioAsync(
            "layout-snapshot-rect",
            """
            const model = hooks.importFromCSharpJson(createDocument('Square', 120, 12));
            const layout = hooks.createParagraphLayoutEngine(null, { minReadableWidth: 8 }).layoutDocument(model, pageOptions());
            const object = layout.objects.find(item => item.objectId === 'phase5-object');
            assert.ok(object, 'layout object is missing');

            const html = hooks.renderWysiwygBodyLayersHtmlForTest({ model, layout, selection: null, options: {} }, model.body.blocks);
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');

            assert.ok(objectLayer.includes('data-object-position-source="layout-rect"'), 'layout snapshot rect must be authoritative');
            assert.ok(objectLayer.includes('left:' + Number(object.rect.x) + 'px'), JSON.stringify(object.rect));
            assert.ok(objectLayer.includes('top:' + Number(object.rect.y) + 'px'), JSON.stringify(object.rect));
            assert.ok(objectLayer.includes('width:' + Number(object.rect.width) + 'px'), JSON.stringify(object.rect));
            assert.ok(objectLayer.includes('height:' + Number(object.rect.height) + 'px'), JSON.stringify(object.rect));

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public void Phase5_WysiwygFloatUtilitiesDoNotControlTextFlow()
    {
        var root = FindRepositoryRoot();
        var componentCss = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css"));
        var bundledCss = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "tempo-blazor.bundled.css"));

        foreach (var css in new[] { componentCss, bundledCss })
        {
            Regex.IsMatch(css, @"\.tm-wysiwyg-image--float-left\s*\{[^}]*float\s*:\s*(left|right)", RegexOptions.Singleline)
                .Should().BeFalse("legacy WYSIWYG float-left utility must not move text");
            Regex.IsMatch(css, @"\.tm-wysiwyg-image--float-right\s*\{[^}]*float\s*:\s*(left|right)", RegexOptions.Singleline)
                .Should().BeFalse("legacy WYSIWYG float-right utility must not move text");
            Regex.IsMatch(css, @"\.tm-wysiwyg-image--wrap-topbottom[^{}]*\{[^}]*clear\s*:\s*both", RegexOptions.Singleline)
                .Should().BeFalse("top-bottom wrapping is resolved by interval layout, not browser clear");
        }
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase5-{scenario}-{Guid.NewGuid():N}.js");
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

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;

        function extractLayer(html, testId) {
            const marker = `data-testid="${testId}"`;
            const start = html.indexOf(marker);
            if (start < 0) return '';
            const next = html.indexOf('tm-wysiwyg-page__layer', start + marker.length);
            return next < 0 ? html.slice(start) : html.slice(start, next);
        }

        function pageOptions() {
            return {
                width: 420,
                height: 320,
                marginTop: 40,
                marginRight: 40,
                marginBottom: 40,
                marginLeft: 40,
                blockGap: 0,
                lineGap: 0,
                minReadableWidth: 8
            };
        }

        function wrapModeValue(name) {
            if (name === 'Inline') return 0;
            if (name === 'Square') return 1;
            if (name === 'Tight') return 2;
            if (name === 'Through') return 3;
            if (name === 'TopBottom') return 4;
            return 1;
        }

        function createDocument(wrapMode, x, y) {
            const inline = wrapMode === 'Inline';
            return {
                DocumentId: 'image-wrap-phase5',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: {
                        $type: 'paragraph',
                        Inlines: [
                            { $type: 'text', Id: 'before', Text: 'Alpha ' },
                            {
                                $type: 'drawing',
                                Id: 'phase5-run',
                                ObjectId: 'phase5-object',
                                Kind: 0,
                                Source: 0,
                                Url: '/phase5.png',
                                AltText: 'Phase 5 object',
                                Size: { Width: 96, Height: 64 },
                                Layout: {
                                    Kind: inline ? 0 : 1,
                                    Wrap: { Mode: wrapModeValue(wrapMode), DistanceLeft: 8, DistanceRight: 8 },
                                    Anchor: { BlockId: 'p1', Offset: 6, InlineIndex: 1, MoveWithText: true },
                                    Position: {
                                        HorizontalRelativeTo: 2,
                                        HorizontalAlignment: 0,
                                        VerticalRelativeTo: 3,
                                        VerticalAlignment: 1,
                                        X: x || 0,
                                        Y: y || 0
                                    },
                                    Transform: { Width: 96, Height: 64 }
                                }
                            },
                            { $type: 'text', Id: 'after', Text: ' beta gamma delta epsilon zeta eta theta iota.' }
                        ]
                    }
                }]
            };
        }

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
