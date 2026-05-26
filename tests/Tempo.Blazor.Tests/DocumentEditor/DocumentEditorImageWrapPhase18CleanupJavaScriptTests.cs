using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase18CleanupJavaScriptTests
{
    [Fact]
    public async Task Phase18_WysiwygAnchorsUseOnlyIntervalLayoutAndKeepInlineImageFlow()
    {
        var result = await RunScenarioAsync(
            "anchor-cleanup",
            """
            const model = hooks.importFromCSharpJson(createDrawingDocument());
            const html = hooks.renderWysiwygBodyLayersHtmlForTest({
                id: 'phase18-cleanup',
                model,
                selection: hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 2 }),
                options: {}
            }, model.body.blocks);

            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const objectLayer = extractLayer(html, 'document-wysiwyg-object-layer');
            const inlineAnchor = elementWithAttribute(textLayer, 'data-object-anchor-id', 'phase18-inline');
            const floatingAnchor = elementWithAttribute(textLayer, 'data-object-anchor-id', 'phase18-floating');

            assert.ok(inlineAnchor, textLayer);
            assert.ok(floatingAnchor, textLayer);
            assert.ok(inlineAnchor.includes('tm-wysiwyg-drawing-anchor--inline'), inlineAnchor);
            assert.ok(inlineAnchor.includes('display:inline-block'), inlineAnchor);
            assert.ok(inlineAnchor.includes('width:48px'), inlineAnchor);
            assert.ok(inlineAnchor.includes('height:32px'), inlineAnchor);
            assert.ok(floatingAnchor.includes('tm-wysiwyg-drawing-anchor--anchored'), floatingAnchor);
            assert.ok(floatingAnchor.includes('width:0px'), floatingAnchor);
            assert.ok(floatingAnchor.includes('height:0px'), floatingAnchor);
            assert.strictEqual(floatingAnchor.includes('data-flow-reservation="true"'), false, floatingAnchor);
            assert.strictEqual(floatingAnchor.includes('float:'), false, floatingAnchor);
            assert.strictEqual(floatingAnchor.includes('shape-outside'), false, floatingAnchor);
            assert.strictEqual(floatingAnchor.includes('display:block'), false, floatingAnchor);
            assert.strictEqual(floatingAnchor.includes('clear:both'), false, floatingAnchor);

            assert.ok(objectLayer.includes('data-object-id="phase18-floating"'), objectLayer);
            assert.ok(objectLayer.includes('role="img"'), objectLayer);
            assert.strictEqual(objectLayer.includes('float:'), false, objectLayer);
            assert.strictEqual(objectLayer.includes('shape-outside'), false, objectLayer);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase18_InlineDrawingAnchorReservesCaptionFlowHeight()
    {
        var result = await RunScenarioAsync(
            "inline-caption-flow",
            """
            const model = hooks.importFromCSharpJson(createDrawingDocument());
            const inlineRun = model.body.blocks[0].content.runs.find(run => run.objectId === 'phase18-inline');
            inlineRun.caption = 'Inline caption wraps below the image';

            const html = hooks.renderWysiwygBodyLayersHtmlForTest({
                id: 'phase18-inline-caption',
                model,
                selection: null,
                options: {}
            }, model.body.blocks);
            const textLayer = extractLayer(html, 'document-wysiwyg-text-layer');
            const inlineAnchor = elementWithAttribute(textLayer, 'data-object-anchor-id', 'phase18-inline');
            const heightMatch = /;height:(\d+(?:\.\d+)?)px/.exec(inlineAnchor);

            assert.ok(inlineAnchor.includes('--tm-wysiwyg-drawing-caption-reserve-height:'), inlineAnchor);
            assert.ok(heightMatch, inlineAnchor);
            assert.ok(Number(heightMatch[1]) > 32, inlineAnchor);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public void Phase18_StaticCleanupRemovesWysiwygFlowFallbacksAndDemoImageBlockConversion()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js"));
        var componentCss = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css"));
        var bundledCss = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "tempo-blazor.bundled.css"));
        var apiSeed = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor.Demo.Api", "Services", "DemoDocumentEditorStore.cs"));
        var sharedSeed = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor.Demo.SharedUI", "Services", "DemoDocumentEditorProvider.cs"));

        script.Should().NotContain("data-flow-reservation");
        script.Should().NotContain("tm-wysiwyg-drawing-anchor--flow");
        script.Should().NotContain("full-band");

        foreach (var css in new[] { componentCss, bundledCss })
        {
            css.Should().NotContain(".tm-wysiwyg-image--float-left");
            css.Should().NotContain(".tm-wysiwyg-image--float-right");
            css.Should().NotContain(".tm-wysiwyg-drawing-anchor--flow");
            css.Should().Contain(".tm-wysiwyg-drawing-anchor--inline");
            CssRuleContains(css, @".tm-wysiwyg[^,{]*image", "float").Should().BeFalse("floating WYSIWYG images are positioned by layout layers, not CSS floats");
            CssRuleContains(css, @".tm-wysiwyg[^,{]*image", "shape-outside").Should().BeFalse("text exclusions are interval data, not browser shape-outside");
        }

        foreach (var seed in new[] { apiSeed, sharedSeed })
        {
            seed.Should().NotContain("ConvertImageBlocksToDrawingRuns(");
            seed.Should().Contain("CreateImageDrawingParagraph(");
            seed.Should().NotContain("Type = DocumentBlockType.Image");
            seed.Should().NotContain("Content = new ImageBlockContent");
        }
    }

    private static bool CssRuleContains(string css, string selectorPattern, string property)
    {
        var pattern = selectorPattern + @"[^{}]*\{[^}]*\b" + Regex.Escape(property) + @"\s*:";
        return Regex.IsMatch(css, pattern, RegexOptions.Singleline);
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase18-{scenario}-{Guid.NewGuid():N}.js");
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
                Promise,
                Node: { ELEMENT_NODE: 1 }
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.innerWidth = 1280;
            sandbox.window.innerHeight = 800;
            sandbox.window.performance = { now: () => Date.now() };
            sandbox.window.Node = sandbox.Node;
            sandbox.window.getSelection = function () { return null; };
            return sandbox;
        }

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;

        function extractLayer(html, testId) {
            const marker = 'data-testid="' + testId + '"';
            const start = html.indexOf(marker);
            assert.ok(start >= 0, 'missing layer ' + testId + ': ' + html);
            const open = html.lastIndexOf('<div', start);
            const next = html.indexOf('<div class="tm-wysiwyg-page__layer ', start + marker.length);
            return html.slice(open, next >= 0 ? next : html.length);
        }

        function elementWithAttribute(html, attribute, value) {
            const marker = attribute + '="' + value + '"';
            const index = html.indexOf(marker);
            if (index < 0) return '';
            const start = html.lastIndexOf('<', index);
            const end = html.indexOf('>', index);
            return html.slice(start, end + 1);
        }

        function createDrawingDocument() {
            return {
                DocumentId: 'phase18-image-wrap-cleanup',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: { Inlines: [
                        { Id: 'before', Text: 'Before ' },
                        {
                            $type: 'drawing',
                            Id: 'draw-inline',
                            ObjectId: 'phase18-inline',
                            Kind: 0,
                            Source: 0,
                            Url: '/inline.png',
                            AltText: 'Inline image',
                            Size: { Width: 48, Height: 32, LockAspectRatio: true },
                            Layout: {
                                Kind: 0,
                                Anchor: { BlockId: 'p1', Offset: 7, InlineIndex: 1, Region: 'Body' },
                                Wrap: { Mode: 0 },
                                Transform: { Width: 48, Height: 32, LockAspectRatio: true }
                            }
                        },
                        { Id: 'middle', Text: ' middle text around ' },
                        {
                            $type: 'drawing',
                            Id: 'draw-floating',
                            ObjectId: 'phase18-floating',
                            Kind: 0,
                            Source: 0,
                            Url: '/floating.png',
                            AltText: 'Floating image',
                            Caption: 'Floating caption',
                            Size: { Width: 120, Height: 72, LockAspectRatio: true },
                            Layout: {
                                Kind: 1,
                                Anchor: { BlockId: 'p1', Offset: 26, InlineIndex: 3, Region: 'Body', MoveWithText: true },
                                Position: { HorizontalRelativeTo: 2, VerticalRelativeTo: 3, X: 210, Y: 36 },
                                Wrap: { Mode: 1, DistanceLeft: 8, DistanceRight: 8, DistanceTop: 4, DistanceBottom: 4 },
                                Transform: { Width: 120, Height: 72, LockAspectRatio: true },
                                Stacking: { ZIndex: 0, AllowOverlap: false }
                            }
                        },
                        { Id: 'after', Text: ' after.' }
                    ] }
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
