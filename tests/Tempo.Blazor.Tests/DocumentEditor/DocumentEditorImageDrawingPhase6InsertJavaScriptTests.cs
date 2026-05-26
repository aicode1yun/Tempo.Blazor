using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase6InsertJavaScriptTests
{
    [Fact]
    public async Task Phase6_InsertImageSplitsTextRunAndSelectsNewDrawingRun()
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
            const model = hooks.importFromCSharpJson(createTextDocument('HelloWorld'));
            const operation = hooks.createOperation('InsertImage', {
                target: { blockId: 'p1', offset: 5, region: 'Body' },
                blockId: 'inserted-object',
                image: {
                    Url: '/images/inserted.png',
                    AltText: 'Inserted image',
                    Caption: 'Inserted caption',
                    Size: { Width: 40, Height: 20 },
                    Metadata: { Provider: 'phase6-test' }
                }
            }, { source: 'phase6-test', timestamp: 1000 });

            const result = hooks.applyOperation(model, operation, {
                selection: hooks.createSelectionSnapshot({ region: 'Body', blockId: 'p1', offset: 5 })
            });

            assert.strictEqual(result.ok, true, JSON.stringify(result.errors || []));
            assert.strictEqual(model.body.blocks.filter(block => block.type === 'image').length, 0);
            const runs = model.body.blocks[0].content.runs;
            assert.strictEqual(runs.length, 3);
            assert.strictEqual(runs[0].text, 'Hello');
            assert.strictEqual(runs[1].kind, 'drawing');
            assert.strictEqual(runs[1].objectId, 'inserted-object');
            assert.strictEqual(runs[1].altText, 'Inserted image');
            assert.strictEqual(runs[1].caption, 'Inserted caption');
            assert.strictEqual(runs[1].metadata.Provider, 'phase6-test');
            assert.strictEqual(runs[2].text, 'World');
            assert.strictEqual(runs[1].layout.Anchor.BlockId, 'p1');
            assert.strictEqual(runs[1].layout.Anchor.Offset, 5);
            assert.strictEqual(runs[1].layout.Anchor.InlineIndex, 1);
            assert.strictEqual(runs[1].layout.Kind, 0);
            assert.strictEqual(runs[1].layout.Wrap.Mode, 0);
            assert.strictEqual(runs[1].layout.Transform.Width, 40);
            assert.strictEqual(runs[1].layout.Transform.Height, 20);
            assert.strictEqual(result.nextSelection.selectionMode, 'Object');
            assert.strictEqual(result.nextSelection.activeObjectId, 'inserted-object');
            assert.strictEqual(result.nextSelection.objectSelection.anchorBlockId, 'p1');
            assert.strictEqual(result.nextSelection.objectSelection.anchorInlineIndex, 1);
            assert.strictEqual(result.nextSelection.objectSelection.textSelection.offset, 5);
            assert.strictEqual(result.nextSelection.objectSelection.textSelection.affinity, 'after');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "split");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_InsertImageSupportsEmptyParagraphAndHeadingContent()
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
            const emptyModel = hooks.importFromCSharpJson({
                DocumentId: 'phase6-empty',
                Blocks: [{ Id: 'empty', Type: 'Paragraph', Content: { Inlines: [{ Id: 'empty-run', Text: '' }] } }]
            });
            const emptyResult = hooks.applyOperation(emptyModel, hooks.createOperation('InsertImage', {
                target: { blockId: 'empty', offset: 0 },
                blockId: 'empty-object',
                image: { Url: '/empty.png', AltText: 'Empty insert' }
            }, { source: 'phase6-test', timestamp: 1000 }));

            assert.strictEqual(emptyResult.ok, true, JSON.stringify(emptyResult.errors || []));
            assert.strictEqual(emptyModel.body.blocks[0].content.runs.length, 1);
            assert.strictEqual(emptyModel.body.blocks[0].content.runs[0].kind, 'drawing');
            assert.strictEqual(emptyModel.body.blocks[0].content.runs[0].layout.Anchor.BlockId, 'empty');
            assert.strictEqual(emptyModel.body.blocks[0].content.runs[0].layout.Anchor.Offset, 0);

            const headingModel = hooks.importFromCSharpJson({
                DocumentId: 'phase6-heading',
                Blocks: [{ Id: 'h1', Type: 'Heading', Content: { $type: 'heading', Inlines: [{ Id: 'h-run', Text: 'Title' }] } }]
            });
            const headingResult = hooks.applyOperation(headingModel, hooks.createOperation('InsertImage', {
                target: { blockId: 'h1', offset: 5 },
                blockId: 'heading-object',
                image: { Url: '/heading.png', AltText: 'Heading insert' }
            }, { source: 'phase6-test', timestamp: 1100 }));

            assert.strictEqual(headingResult.ok, true, JSON.stringify(headingResult.errors || []));
            const headingRuns = headingModel.body.blocks[0].content.runs;
            assert.strictEqual(headingRuns[0].text, 'Title');
            assert.strictEqual(headingRuns[1].kind, 'drawing');
            assert.strictEqual(headingRuns[1].objectId, 'heading-object');
            assert.strictEqual(headingRuns[1].layout.Anchor.BlockId, 'h1');
            assert.strictEqual(headingRuns[1].layout.Anchor.Offset, 5);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "empty-heading");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_InsertImageUndoRedoAndExportPreserveDrawingRun()
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
            const model = hooks.importFromCSharpJson(createTextDocument('HelloWorld'));
            const history = hooks.createHistoryController(model, {
                selection: { region: 'Body', blockId: 'p1', offset: 5, isCollapsed: true }
            });

            const commit = history.commitOperation(hooks.createOperation('InsertImage', {
                target: { blockId: 'p1', offset: 5, region: 'Body' },
                blockId: 'history-object',
                image: { Url: '/history.png', AltText: 'History image', Size: { Width: 90, Height: 60 } }
            }, { source: 'phase6-test', timestamp: 1000 }), {
                label: 'Insert image',
                beforeSelection: { region: 'Body', blockId: 'p1', offset: 5, isCollapsed: true }
            });

            assert.strictEqual(commit.ok, true, JSON.stringify(commit.errors || []));
            assert.strictEqual(countDrawingRuns(model), 1);
            const exported = hooks.exportToCSharpJson(model);
            assert.strictEqual(exported.Blocks[0].Content.Inlines.some(run => run.$type === 'drawing' && run.ObjectId === 'history-object'), true);
            const reimported = hooks.importFromCSharpJson(exported);
            assert.strictEqual(countDrawingRuns(reimported), 1);

            const undo = history.undo();
            assert.strictEqual(undo.ok, true, JSON.stringify(undo.errors || []));
            assert.strictEqual(hooks.getBlockText(model.body.blocks[0]), 'HelloWorld');
            assert.strictEqual(countDrawingRuns(model), 0);
            assert.strictEqual(undo.selection.blockId, 'p1');
            assert.strictEqual(undo.selection.offset, 5);

            const redo = history.redo();
            assert.strictEqual(redo.ok, true, JSON.stringify(redo.errors || []));
            assert.strictEqual(hooks.getBlockText(model.body.blocks[0]), 'HelloWorld');
            assert.strictEqual(countDrawingRuns(model), 1);
            assert.strictEqual(redo.selection.selectionMode, 'Object');
            assert.strictEqual(redo.selection.activeObjectId, 'history-object');

            console.log('OK');

            function countDrawingRuns(doc) {
                return doc.body.blocks
                    .flatMap(block => block.content?.runs || [])
                    .filter(run => run.kind === 'drawing').length;
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "undo-redo");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_InsertImageObjectCommandMapsToolbarPayloadToCaretOperation()
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
            const model = hooks.importFromCSharpJson(createTextDocument('abcdef'));
            const operation = hooks.createInsertImageOperationFromCommand(model, 'insertImageObject', {
                Image: {
                    $type: 'drawing',
                    ObjectId: 'command-object',
                    Url: '/command.png',
                    AltText: 'Command image',
                    Caption: 'Command caption'
                },
                ObjectId: 'command-object',
                Selection: { region: 'Body', blockId: 'p1', offset: 3, isCollapsed: true }
            }, { region: 'Body', blockId: 'p1', offset: 0, isCollapsed: true });

            assert.strictEqual(operation.type, 'InsertImage');
            assert.strictEqual(operation.target.blockId, 'p1');
            assert.strictEqual(operation.target.offset, 3);
            assert.strictEqual(operation.blockId, 'command-object');
            assert.strictEqual(operation.image.Url, '/command.png');

            const result = hooks.applyOperation(model, operation);
            assert.strictEqual(result.ok, true, JSON.stringify(result.errors || []));
            const runs = model.body.blocks[0].content.runs;
            assert.strictEqual(runs[0].text, 'abc');
            assert.strictEqual(runs[1].kind, 'drawing');
            assert.strictEqual(runs[1].objectId, 'command-object');
            assert.strictEqual(runs[2].text, 'def');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "command");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase6-{scenario}-{Guid.NewGuid():N}.js");
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

        function createTextDocument(text) {
            return {
                DocumentId: 'image-drawing-phase6',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'p1-text', Text: text }
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
