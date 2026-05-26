using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase24RuntimeJavaScriptTests
{
    [Fact]
    public async Task Phase24_InsertImageNode_InsertsDrawingRunAtCaretWithoutImageBlock()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = SharedSandboxScript +
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const instanceId = engine.create(createRoot(), { InstanceId: 'phase24-insert' }, null);
            engine.loadDocument(instanceId, createTextDocument('HelloWorld'));
            engine.restoreSelection(instanceId, { region: 'Body', blockId: 'p1', offset: 5, isCollapsed: true });

            const insert = engine.insertImageNode(instanceId, {
                Id: 'phase24-object',
                Type: 'Image',
                Content: {
                    $type: 'image',
                    Url: '/phase24.png',
                    AltText: 'Phase 24 image',
                    Caption: 'Inserted as drawing',
                    Size: { Width: 64, Height: 32 }
                }
            });

            assert.strictEqual(insert.ok, true, JSON.stringify(insert));
            assert.strictEqual(insert.objectId, 'phase24-object');

            const snapshot = engine.getDocumentSnapshot(instanceId).csharpDocument;
            assert.strictEqual(snapshot.Blocks.filter(block => block.Content && block.Content.$type === 'image').length, 0);
            assert.strictEqual(snapshot.Blocks.length, 1);
            const inlines = snapshot.Blocks[0].Content.Inlines;
            assert.strictEqual(inlines.length, 3);
            assert.strictEqual(inlines[0].Text, 'Hello');
            assert.strictEqual(inlines[1].$type, 'drawing');
            assert.strictEqual(inlines[1].ObjectId, 'phase24-object');
            assert.strictEqual(inlines[1].AltText, 'Phase 24 image');
            assert.strictEqual(inlines[2].Text, 'World');

            const undoState = engine.getUndoState(instanceId);
            assert.strictEqual(undoState.CanUndo, true);
            assert.strictEqual(undoState.UndoDepth, 1);
            engine.applyCommand(instanceId, 'undo', {});
            const afterUndo = engine.getDocumentSnapshot(instanceId).csharpDocument;
            assert.strictEqual(afterUndo.Blocks.filter(block => block.Content && block.Content.$type === 'image').length, 0);
            assert.strictEqual(afterUndo.Blocks[0].Content.Inlines.length, 1);
            assert.strictEqual(afterUndo.Blocks[0].Content.Inlines[0].Text, 'HelloWorld');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "insert-node");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase24_ImageCommands_RequireDrawingObjectIdInsteadOfImageBlockId()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = SharedSandboxScript +
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const instanceId = engine.create(createRoot(), { InstanceId: 'phase24-command' }, null);
            engine.loadDocument(instanceId, createLegacyImageDocument());
            engine.restoreSelection(instanceId, {
                region: 'Image',
                selectionMode: 'Object',
                anchorBlockId: 'legacy-img',
                focusBlockId: 'legacy-img',
                activeImageBlockId: 'legacy-img',
                isCollapsed: false
            });

            const result = engine.applyCommand(instanceId, 'setImageSize', { Width: 320, Height: 180 });
            assert.strictEqual(result.ok, false, JSON.stringify(result));
            assert.strictEqual(result.error.code, 'active-image-not-found');

            const snapshot = engine.getDocumentSnapshot(instanceId).csharpDocument;
            const imageBlocks = snapshot.Blocks.filter(block => block.Content && block.Content.$type === 'image');
            assert.strictEqual(imageBlocks.length, 1);
            assert.strictEqual(imageBlocks[0].Content.Size.Width, 100);
            assert.strictEqual(imageBlocks[0].Content.Size.Height, 50);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "command-object-id");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase24_InsertImageBlockCommandAlias_IsRemovedFromRuntimeMapper()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = SharedSandboxScript +
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
            const operation = hooks.createInsertImageOperationFromCommand(model, 'insertImageBlock', {
                Block: {
                    Id: 'legacy-command-object',
                    Type: 'Image',
                    Content: { $type: 'image', Url: '/legacy-command.png', AltText: 'Legacy command image' }
                },
                Selection: { region: 'Body', blockId: 'p1', offset: 3, isCollapsed: true }
            }, { region: 'Body', blockId: 'p1', offset: 0, isCollapsed: true });

            assert.strictEqual(operation, null);
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "removed-command-alias");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase24_RuntimeDiagnostics_ReportDrawingObjectAsImageSourceOfTruth()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = SharedSandboxScript +
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const instanceId = engine.create(createRoot(), { InstanceId: 'phase24-diagnostics' }, null);
            engine.loadDocument(instanceId, createDrawingDocument());
            engine.restoreSelection(instanceId, {
                region: 'Body',
                selectionMode: 'Object',
                objectId: 'drawing-1',
                activeObjectId: 'drawing-1',
                objectSelection: {
                    region: 'Body',
                    kind: 'image',
                    objectId: 'drawing-1',
                    blockId: 'p1',
                    anchorBlockId: 'p1',
                    anchorInlineIndex: 1,
                    inlineIndex: 1
                }
            });

            const debug = engine.getDebugSnapshot(instanceId);
            assert.strictEqual(debug.imageRuntime.sourceOfTruth, 'drawing-object-id');
            assert.strictEqual(debug.imageRuntime.activeObjectId, 'drawing-1');
            assert.strictEqual(debug.imageRuntime.imageBlockIsSourceOfTruth, false);
            assert.strictEqual(debug.imageRuntime.topLevelImageBlockCount, 0);
            assert.strictEqual(debug.imageRuntime.drawingObjectCount, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "diagnostics");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase24_ImageEditSnapshot_PersistsDrawingRunInsteadOfImageBlock()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = SharedSandboxScript +
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const instanceId = engine.create(createRoot(), { InstanceId: 'phase24-save' }, null);
            engine.loadDocument(instanceId, createDrawingDocument());
            engine.restoreSelection(instanceId, {
                region: 'Body',
                selectionMode: 'Object',
                objectId: 'drawing-1',
                activeObjectId: 'drawing-1',
                objectSelection: {
                    region: 'Body',
                    kind: 'image',
                    objectId: 'drawing-1',
                    blockId: 'p1',
                    anchorBlockId: 'p1',
                    anchorInlineIndex: 1,
                    inlineIndex: 1
                }
            });

            const result = engine.applyCommand(instanceId, 'setImageSize', { Width: 240, Height: 160 });
            assert.strictEqual(result.ok, true, JSON.stringify(result));

            const snapshot = engine.getDocumentSnapshot(instanceId).csharpDocument;
            assert.strictEqual(snapshot.Blocks.filter(block => block.Content && block.Content.$type === 'image').length, 0);
            const drawing = snapshot.Blocks[0].Content.Inlines[1];
            assert.strictEqual(drawing.$type, 'drawing');
            assert.strictEqual(drawing.ObjectId, 'drawing-1');
            const size = drawing.Size || drawing.size || {};
            const layout = drawing.Layout || drawing.layout || {};
            const transform = layout.Transform || layout.transform || {};
            assert.strictEqual(size.Width ?? size.width, 240);
            assert.strictEqual(size.Height ?? size.height, 160);
            assert.strictEqual(transform.Width ?? transform.width, 240);
            assert.strictEqual(transform.Height ?? transform.height, 160);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "edit-save");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase24-{scenario}-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
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
                requestAnimationFrame: cb => setTimeout(cb, 0),
                URL,
                JSON,
                Date,
                Math,
                Promise
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.requestAnimationFrame = sandbox.requestAnimationFrame;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            sandbox.window.performance = { now: () => Date.now() };
            return sandbox;
        }

        function createRoot() {
            return {
                innerHTML: '',
                attributes: {},
                classList: { add() {}, remove() {}, toggle() {}, contains() { return false; } },
                setAttribute(name, value) { this.attributes[name] = String(value); },
                removeAttribute(name) { delete this.attributes[name]; },
                addEventListener() {},
                removeEventListener() {},
                querySelector() { return null; },
                querySelectorAll() { return []; },
                contains() { return true; }
            };
        }

        function createTextDocument(text) {
            return {
                DocumentId: 'phase24-text',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [{ $type: 'text', Id: 'p1-text', Text: text }]
                        }
                    }
                ]
            };
        }

        function createLegacyImageDocument() {
            return {
                DocumentId: 'phase24-legacy',
                Blocks: [
                    {
                        Id: 'legacy-img',
                        Type: 'Image',
                        Content: {
                            $type: 'image',
                            Url: '/legacy.png',
                            AltText: 'Legacy',
                            Size: { Width: 100, Height: 50 },
                            Layout: { Transform: { Width: 100, Height: 50 }, Wrap: { Mode: 0 } }
                        }
                    }
                ]
            };
        }

        function createDrawingDocument() {
            return {
                DocumentId: 'phase24-drawing',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'before', Text: 'Before ' },
                                {
                                    $type: 'drawing',
                                    Id: 'drawing-run-1',
                                    ObjectId: 'drawing-1',
                                    Kind: 0,
                                    Url: '/drawing.png',
                                    AltText: 'Drawing',
                                    Size: { Width: 120, Height: 80 },
                                    Layout: {
                                        Kind: 0,
                                        Anchor: { BlockId: 'p1', Offset: 7, InlineIndex: 1 },
                                        Wrap: { Mode: 0 },
                                        Transform: { Width: 120, Height: 80 }
                                    }
                                },
                                { $type: 'text', Id: 'after', Text: ' after' }
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
