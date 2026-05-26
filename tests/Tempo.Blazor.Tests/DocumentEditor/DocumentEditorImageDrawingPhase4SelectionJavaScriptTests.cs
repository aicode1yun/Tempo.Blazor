using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase4SelectionJavaScriptTests
{
    [Fact]
    public async Task Phase4_Runtime_CreatesTextAndObjectSelectionSnapshots()
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

            const text = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 3 });
            assert.strictEqual(text.mode, 'Text');
            assert.strictEqual(text.selectionMode, 'Text');
            assert.strictEqual(text.isObjectSelection, false);
            assert.strictEqual(text.textSelection.mode, 'Text');
            assert.strictEqual(text.textSelection.blockId, 'p1');
            assert.strictEqual(text.textSelection.offset, 3);
            assert.strictEqual(text.objectSelection, null);

            const previousCaret = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p2', offset: 2 });
            const object = hooks.createObjectSelectionSnapshot(model, 'drawing-square', previousCaret);
            assert.strictEqual(object.mode, 'Object');
            assert.strictEqual(object.selectionMode, 'Object');
            assert.strictEqual(object.isObjectSelection, true);
            assert.strictEqual(object.activeObjectId, 'drawing-square');
            assert.strictEqual(object.activeImageBlockId, 'p2');
            assert.strictEqual(object.objectSelection.objectId, 'drawing-square');
            assert.strictEqual(object.objectSelection.anchorBlockId, 'p2');
            assert.strictEqual(object.objectSelection.anchorOffset, 4);
            assert.strictEqual(object.objectSelection.anchorInlineIndex, 1);
            assert.strictEqual(object.textSelection.blockId, 'p2');
            assert.strictEqual(object.textSelection.offset, 2);
            assert.strictEqual(object.textSelection.anchor.blockId, 'p2');
            assert.strictEqual(object.textSelection.anchor.offset, 2);
            assert.strictEqual(object.textSelection.focus.blockId, 'p2');
            assert.strictEqual(object.textSelection.focus.offset, 2);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase4_Runtime_RestoresTextSelectionFromObjectSelection()
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
            const previousCaret = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p2', offset: 2 });
            const object = hooks.createObjectSelectionSnapshot(model, 'drawing-square', previousCaret);

            const restored = hooks.restoreTextSelectionFromObjectSelection(object);
            assert.strictEqual(restored.mode, 'Text');
            assert.strictEqual(restored.selectionMode, 'Text');
            assert.strictEqual(restored.isObjectSelection, false);
            assert.strictEqual(restored.blockId, 'p2');
            assert.strictEqual(restored.offset, 4);
            assert.strictEqual(restored.activeObjectId, null);
            assert.strictEqual(restored.activeImageBlockId, null);
            assert.strictEqual(restored.objectSelection, null);

            const fallbackObject = hooks.createObjectSelectionSnapshot(model, { objectId: 'drawing-square' });
            const fallbackRestored = hooks.restoreTextSelectionFromObjectSelection(fallbackObject);
            assert.strictEqual(fallbackRestored.mode, 'Text');
            assert.strictEqual(fallbackRestored.blockId, 'p2');
            assert.strictEqual(fallbackRestored.offset, 4);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase4_Runtime_DeletesObjectSelectionButNotAdjacentTextSelection()
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
            const previousCaret = hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p2', offset: 2 });
            const object = hooks.createObjectSelectionSnapshot(model, 'drawing-square', previousCaret);
            const deleted = hooks.deleteObjectSelection(model, object);

            assert.strictEqual(deleted.ok, true);
            assert.strictEqual(deleted.deletedObjectId, 'drawing-square');
            assert.strictEqual(hooks.findDrawingRunByObjectId(model, 'drawing-square'), null);
            assert.strictEqual(hooks.getBlockText(model.body.blocks.find(block => block.id === 'p2')), 'Wrap text');
            assert.strictEqual(deleted.selection.mode, 'Text');
            assert.strictEqual(deleted.selection.blockId, 'p2');
            assert.strictEqual(deleted.selection.offset, 4);

            const deleteModel = hooks.importFromCSharpJson(createDocument());
            const deletePipeline = hooks.createInputPipeline({
                model: deleteModel,
                selection: hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p2', offset: 4 })
            });
            const deleteResult = deletePipeline.handleBeforeInput({ inputType: 'deleteContentForward' });
            assert.strictEqual(deleteResult.ok, true);
            assert.ok(hooks.findDrawingRunByObjectId(deleteModel, 'drawing-square'));

            const backspaceModel = hooks.importFromCSharpJson(createDocument());
            const backspacePipeline = hooks.createInputPipeline({
                model: backspaceModel,
                selection: hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p2', offset: 4 })
            });
            const backspaceResult = backspacePipeline.handleBeforeInput({ inputType: 'deleteContentBackward' });
            assert.strictEqual(backspaceResult.ok, true);
            assert.ok(hooks.findDrawingRunByObjectId(backspaceModel, 'drawing-square'));

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase4-{Guid.NewGuid():N}.js");
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

        function createDocument() {
            return {
                DocumentId: 'image-drawing-phase4',
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
