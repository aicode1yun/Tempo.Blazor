using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase5FocusPolicyJavaScriptTests
{
    [Fact]
    public async Task Phase5_Runtime_RenderedImageFiguresAreNotFocusableTabStops()
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
            const model = hooks.importFromCSharpJson(createLegacyImageDocument());
            const image = model.body.blocks.find(block => block.id === 'img1');
            const textSelection = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 2 });
            const html = hooks.renderEngineBlockHtmlForTest({ model, selection: textSelection, options: {} }, image);

            assert.ok(html.includes('role="figure"'));
            assert.ok(html.includes('aria-label="Evidence preview"'));
            assert.ok(html.includes('data-object-focus-policy="selection-only"'));
            assert.ok(!/tabindex\s*=/.test(html), html);
            assert.ok(!html.includes('tm-wysiwyg-object--selected'));
            assert.ok(!html.includes('data-object-selected="true"'));

            const objectSelection = hooks.createObjectSelectionSnapshot(model, { blockId: 'img1', objectId: 'img1' }, textSelection);
            const selectedHtml = hooks.renderEngineBlockHtmlForTest({ model, selection: objectSelection, options: {} }, image);

            assert.ok(!/tabindex\s*=\s*["']?0/.test(selectedHtml), selectedHtml);
            assert.ok(!/tabindex\s*=\s*["']?[1-9]/.test(selectedHtml), selectedHtml);
            assert.ok(selectedHtml.includes('tm-wysiwyg-object--selected'));
            assert.ok(selectedHtml.includes('tm-wysiwyg-image--selected'));
            assert.ok(selectedHtml.includes('data-object-selected="true"'));
            assert.ok(selectedHtml.includes('aria-selected="true"'));

            const policy = hooks.createObjectFocusPolicy(true);
            assert.strictEqual(policy.focusPolicy, 'selection-only');
            assert.strictEqual(policy.isTabStop, false);
            assert.strictEqual(policy.selectedClass, 'tm-wysiwyg-object--selected');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase5_Runtime_ArrowUpDownKeepsTextSelectionPolicyAroundObjects()
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
            const model = hooks.importFromCSharpJson(createDrawingDocument());
            const previousCaret = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p2', offset: 2 });
            const object = hooks.createObjectSelectionSnapshot(model, 'drawing-square', previousCaret);

            const restored = hooks.applyArrowFocusPolicy(object, 'ArrowDown');
            assert.strictEqual(restored.changed, true);
            assert.strictEqual(restored.restoredFromObject, true);
            assert.strictEqual(restored.selection.selectionMode, 'Text');
            assert.strictEqual(restored.selection.isObjectSelection, false);
            assert.strictEqual(restored.selection.blockId, 'p2');
            assert.strictEqual(restored.selection.offset, 4);
            assert.strictEqual(restored.selection.activeImageBlockId, null);
            assert.strictEqual(restored.selection.activeObjectId, null);
            assert.strictEqual(restored.selection.objectSelection, null);

            const staleImageText = hooks.createSelectionSnapshot({
                region: 'Body',
                blockId: 'p1',
                offset: 3,
                selectionMode: 'Text',
                activeImageBlockId: 'p2',
                activeObjectId: 'drawing-square'
            });
            const cleared = hooks.applyArrowFocusPolicy(staleImageText, 'ArrowUp');
            assert.strictEqual(cleared.changed, true);
            assert.strictEqual(cleared.restoredFromObject, false);
            assert.strictEqual(cleared.selection.selectionMode, 'Text');
            assert.strictEqual(cleared.selection.isObjectSelection, false);
            assert.strictEqual(cleared.selection.blockId, 'p1');
            assert.strictEqual(cleared.selection.offset, 3);
            assert.strictEqual(cleared.selection.activeImageBlockId, null);
            assert.strictEqual(cleared.selection.activeObjectId, null);

            const normalText = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 1 });
            const unchanged = hooks.applyArrowFocusPolicy(normalText, 'ArrowDown');
            assert.strictEqual(unchanged.changed, false);
            assert.strictEqual(unchanged.selection.selectionMode, 'Text');
            assert.strictEqual(unchanged.selection.blockId, 'p1');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase5-{Guid.NewGuid():N}.js");
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

        function createLegacyImageDocument() {
            return {
                DocumentId: 'image-drawing-phase5-image',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'p1-text', Text: 'Alpha' }
                            ]
                        }
                    },
                    {
                        Id: 'img1',
                        Type: 'Image',
                        Content: {
                            Type: 'Image',
                            Id: 'img1',
                            Url: 'data:image/png;base64,iVBORw0KGgo=',
                            AltText: 'Evidence preview',
                            Caption: 'Evidence caption',
                            Layout: { Wrap: { Mode: 'Square' }, Transform: { Width: 180, Height: 120 } }
                        }
                    }
                ]
            };
        }

        function createDrawingDocument() {
            return {
                DocumentId: 'image-drawing-phase5-drawing',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'p1-text', Text: 'Alpha' }
                            ]
                        }
                    },
                    {
                        Id: 'p2',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'p2-before', Text: 'Wrap ' },
                                {
                                    $type: 'drawing',
                                    Id: 'p2-drawing',
                                    ObjectId: 'drawing-square',
                                    Kind: 0,
                                    Source: 1,
                                    AssetId: 'asset-square',
                                    AltText: 'Square image',
                                    Size: { Width: 150, Height: 90 },
                                    NaturalSize: { Width: 300, Height: 180 },
                                    Layout: {
                                        Kind: 1,
                                        Anchor: { BlockId: 'p2', InlineIndex: 1, Offset: 4 },
                                        Wrap: { Mode: 1, DistanceLeft: 8, DistanceRight: 12, DistanceBottom: 10 },
                                        Position: { HorizontalAlignment: 0, HorizontalRelativeTo: 0, VerticalRelativeTo: 3, X: 24, Y: 10 },
                                        Transform: { Width: 150, Height: 90, NaturalWidth: 300, NaturalHeight: 180 },
                                        Stacking: { ZIndex: 7 }
                                    }
                                },
                                { $type: 'text', Id: 'p2-after', Text: 'text' }
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
