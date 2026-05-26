using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase3RuntimeJavaScriptTests
{
    [Fact]
    public async Task Phase3_Runtime_ImportsDrawingRunsAndBuildsObjectIndexes()
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
            const p1 = model.body.blocks.find(block => block.id === 'p1');
            const p2 = model.body.blocks.find(block => block.id === 'p2');

            assert.strictEqual(p1.type, 'paragraph');
            assert.strictEqual(p1.content.runs[1].kind, 'drawing');
            assert.strictEqual(p1.content.runs[1].objectId, 'drawing-inline');
            assert.strictEqual(p2.content.runs[1].kind, 'drawing');
            assert.strictEqual(p2.content.runs[1].objectId, 'drawing-square');

            assert.ok(model.indexes.drawingObjectsById['drawing-inline']);
            assert.ok(model.indexes.drawingObjectsById['drawing-square']);
            assert.strictEqual(model.indexes.drawingRunsByBlockId.p1.length, 1);
            assert.strictEqual(model.indexes.drawingRunsByBlockId.p2.length, 1);
            assert.strictEqual(model.indexes.objects['drawing-square'].objectId, 'drawing-square');

            const diagnostics = hooks.getDrawingRuntimeDiagnostics(model);
            assertJsonEqual(diagnostics.drawingObjectIds, ['drawing-inline', 'drawing-square']);
            assert.strictEqual(diagnostics.drawingObjectsById['drawing-inline'].layoutKind, 'Inline');
            assert.strictEqual(diagnostics.drawingObjectsById['drawing-inline'].isInline, true);
            assert.strictEqual(diagnostics.drawingObjectsById['drawing-square'].layoutKind, 'Anchored');
            assert.strictEqual(diagnostics.drawingObjectsById['drawing-square'].isAnchored, true);
            assert.strictEqual(diagnostics.drawingObjectsById['drawing-square'].anchorBlockId, 'p2');
            assert.strictEqual(diagnostics.drawingObjectsById['drawing-square'].anchorOffset, 4);
            assert.strictEqual(diagnostics.drawingRunsByBlockId.p2[0].objectId, 'drawing-square');

            const found = hooks.findDrawingRunByObjectId(model, 'drawing-square');
            assert.strictEqual(found.run.objectId, 'drawing-square');
            assert.strictEqual(found.blockId, 'p2');
            assert.strictEqual(found.inlineIndex, 1);

            const layout = hooks.getDrawingObjectLayoutSnapshot(model, 'drawing-square');
            assert.strictEqual(layout.objectId, 'drawing-square');
            assert.strictEqual(layout.wrapMode, 'Square');
            assert.strictEqual(layout.width, 150);
            assert.strictEqual(layout.height, 90);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_Runtime_TreatsDrawingRunsAsZeroLengthForTextOffsets()
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
            const block = model.body.blocks.find(item => item.id === 'p1');

            assert.strictEqual(hooks.getBlockText(block), 'Hello world');
            assert.strictEqual(hooks.getBlockText(block).length, 11);

            const boundaryAfterDrawing = hooks.resolveTextOffsetToInlineIndex(block, 6);
            assert.strictEqual(boundaryAfterDrawing.inlineIndex, 2);
            assert.strictEqual(boundaryAfterDrawing.runId, 'p1-after');
            assert.strictEqual(boundaryAfterDrawing.localOffset, 0);
            assertJsonEqual(boundaryAfterDrawing.skippedDrawingObjectIds, ['drawing-inline']);

            const insideTextAfterDrawing = hooks.resolveTextOffsetToInlineIndex(block, 8);
            assert.strictEqual(insideTextAfterDrawing.inlineIndex, 2);
            assert.strictEqual(insideTextAfterDrawing.runId, 'p1-after');
            assert.strictEqual(insideTextAfterDrawing.localOffset, 2);

            const beforeAffinity = hooks.resolveTextOffsetToInlineIndex(block, 6, 'before');
            assert.strictEqual(beforeAffinity.inlineIndex, 0);
            assert.strictEqual(beforeAffinity.runId, 'p1-before');
            assert.strictEqual(beforeAffinity.localOffset, 6);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_Runtime_ExportsDrawingRunsAndSchemaAllowsParagraphDrawing()
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
            const schema = hooks.createDefaultSchemaRegistry();
            assert.strictEqual(schema.checkChild('paragraph', 'drawing'), true);
            assert.strictEqual(schema.getDefinition('drawing').isInline, true);
            assert.strictEqual(schema.getDefinition('drawing').isObject, true);

            const model = hooks.importFromCSharpJson(createDocument());
            const validation = hooks.validateModel(model);
            assert.strictEqual(validation.ok, true, JSON.stringify(validation.errors));
            assert.strictEqual(validation.counts.drawingObjects, 2);

            const exported = hooks.exportToCSharpJson(model);
            assert.strictEqual(exported.Blocks.some(block => block.Type === 5), false);
            const exportedRuns = exported.Blocks.find(block => block.Id === 'p1').Content.Inlines;
            assert.strictEqual(exportedRuns[1].$type, 'drawing');
            assert.strictEqual(exportedRuns[1].ObjectId, 'drawing-inline');
            assert.strictEqual(exportedRuns[1].Layout.Wrap.Mode, 0);
            assert.strictEqual(exportedRuns[1].Layout.Transform.Width, 120);

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase3-{Guid.NewGuid():N}.js");
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

        function assertJsonEqual(actual, expected) {
            assert.deepStrictEqual(JSON.parse(JSON.stringify(actual)), expected);
        }

        function createDocument() {
            return {
                DocumentId: 'image-drawing-phase3',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [
                                { $type: 'text', Id: 'p1-before', Text: 'Hello ' },
                                {
                                    $type: 'drawing',
                                    Id: 'p1-drawing',
                                    ObjectId: 'drawing-inline',
                                    Kind: 0,
                                    Source: 0,
                                    Url: '/inline.png',
                                    AltText: 'Inline image',
                                    Caption: 'Inline caption',
                                    Size: { Width: 120, Height: 64 },
                                    NaturalSize: { Width: 240, Height: 128 },
                                    Layout: {
                                        Kind: 0,
                                        Anchor: { BlockId: 'p1', InlineIndex: 1, Offset: 6 },
                                        Wrap: { Mode: 0 },
                                        Transform: { Width: 120, Height: 64, NaturalWidth: 240, NaturalHeight: 128 }
                                    }
                                },
                                { $type: 'text', Id: 'p1-after', Text: 'world' }
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
                                    },
                                    Metadata: { importer: 'phase3-test' }
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
