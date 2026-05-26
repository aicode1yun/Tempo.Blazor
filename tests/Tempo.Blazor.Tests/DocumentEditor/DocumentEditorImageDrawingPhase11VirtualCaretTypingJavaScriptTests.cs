using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase11VirtualCaretTypingJavaScriptTests
{
    [Fact]
    public async Task Phase11_InsertTextAfterVirtualCaret_KeepsDrawingRunAndCreatesTextRunInSameParagraph()
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
            const model = hooks.importFromCSharpJson(createDrawingOnlyDocument('left'));
            const result = hooks.applyOperation(model, hooks.createOperation('InsertText', {
                target: {
                    blockId: 'p1',
                    offset: 0,
                    affinity: 'after',
                    virtualCaret: true,
                    layoutIntervalId: 'p1-line-right'
                },
                text: 'Text vedle'
            }, { source: 'phase11' }));

            assert.strictEqual(result.ok, true, JSON.stringify(result.errors || []));
            const block = model.body.blocks[0];
            const runs = block.content.runs;
            assert.strictEqual(runs.length, 2);
            assert.strictEqual(runs[0].kind, 'drawing');
            assert.strictEqual(runs[0].objectId, 'phase11-left-object');
            assert.strictEqual(runs[0].layout.Anchor.BlockId, 'p1');
            assert.strictEqual(runs[1].kind, 'text');
            assert.strictEqual(runs[1].text, 'Text vedle');
            assert.strictEqual(runs[1].objectId, undefined);
            assert.strictEqual(hooks.getBlockText(block), 'Text vedle');
            assert.strictEqual(model.body.blocks.filter(item => item.type === 'image').length, 0);
            assert.strictEqual(hooks.findDrawingRunByObjectId(model, 'phase11-left-object').blockId, 'p1');
            assert.strictEqual(result.nextSelection.blockId, 'p1');
            assert.strictEqual(result.nextSelection.offset, 'Text vedle'.length);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "after");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_InsertTextBeforeVirtualCaret_UsesParagraphStyleAndDoesNotInheritImageComments()
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
            const model = hooks.importFromCSharpJson(createDrawingOnlyDocument('right'));
            const result = hooks.applyOperation(model, hooks.createOperation('InsertText', {
                target: {
                    blockId: 'p1',
                    offset: 0,
                    affinity: 'before',
                    virtualCaret: true,
                    layoutIntervalId: 'p1-line-left'
                },
                text: 'Text vlevo'
            }, { source: 'phase11' }));

            assert.strictEqual(result.ok, true, JSON.stringify(result.errors || []));
            const runs = model.body.blocks[0].content.runs;
            assert.strictEqual(runs.length, 2);
            assert.strictEqual(runs[0].kind, 'text');
            assert.strictEqual(runs[0].text, 'Text vlevo');
            assert.strictEqual(runs[0].style.fontFamily, 'Georgia');
            assert.strictEqual(runs[0].style.fontSize, 18);
            assert.strictEqual((runs[0].commentIds || []).length, 0);
            assert.strictEqual(runs[1].kind, 'drawing');
            assert.strictEqual(runs[1].objectId, 'phase11-right-object');
            assert.strictEqual(runs[1].commentIds[0], 'image-comment');
            assert.strictEqual(hooks.getBlockText(model.body.blocks[0]), 'Text vlevo');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "before");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_PointerHitTest_CarriesVirtualCaretAffinityForEmptyWrappedIntervals()
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
            const leftModel = hooks.importFromCSharpJson(createDrawingOnlyDocument('left'));
            const leftSelection = hooks.createSelectionEngine(createRootForImage({ x: 80, y: 120, width: 96, height: 64 }, 'phase11-left-object'), leftModel);
            const leftSnapshot = leftSelection.buildLayoutSnapshot();
            const leftRightInterval = leftSnapshot.lineIntervals.find(item => item.blockId === 'p1' && item.x >= 176);
            assert.ok(leftRightInterval, 'right-side interval must exist beside a left square image');
            assert.strictEqual(leftRightInterval.empty, true);
            assert.strictEqual(leftRightInterval.virtualCaret, true);
            assert.strictEqual(leftRightInterval.affinity, 'after');
            const leftHit = leftSelection.hitTest(leftRightInterval.x + 8, 150);
            assert.strictEqual(leftHit.type, 'text');
            assert.strictEqual(leftHit.position.blockId, 'p1');
            assert.strictEqual(leftHit.position.offset, 0);
            assert.strictEqual(leftHit.position.affinity, 'after');
            assert.strictEqual(leftHit.position.virtualCaret, true);
            assert.strictEqual(leftHit.position.layoutIntervalId, leftRightInterval.id);

            const rightModel = hooks.importFromCSharpJson(createDrawingOnlyDocument('right'));
            const rightSelection = hooks.createSelectionEngine(createRootForImage({ x: 304, y: 120, width: 96, height: 64 }, 'phase11-right-object'), rightModel);
            const rightSnapshot = rightSelection.buildLayoutSnapshot();
            const rightLeftInterval = rightSnapshot.lineIntervals.find(item => item.blockId === 'p1' && item.x < 304 && item.x + item.width <= 304);
            assert.ok(rightLeftInterval, 'left-side interval must exist beside a right square image');
            assert.strictEqual(rightLeftInterval.empty, true);
            assert.strictEqual(rightLeftInterval.virtualCaret, true);
            assert.strictEqual(rightLeftInterval.affinity, 'before');
            const rightHit = rightSelection.hitTest(rightLeftInterval.x + 8, 150);
            assert.strictEqual(rightHit.type, 'text');
            assert.strictEqual(rightHit.position.blockId, 'p1');
            assert.strictEqual(rightHit.position.offset, 0);
            assert.strictEqual(rightHit.position.affinity, 'before');
            assert.strictEqual(rightHit.position.virtualCaret, true);
            assert.strictEqual(rightHit.position.layoutIntervalId, rightLeftInterval.id);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "hit-test");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_TypingBesideDrawingOnlyParagraph_IsUndoableWithoutRemovingTheDrawing()
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
            const model = hooks.importFromCSharpJson(createDrawingOnlyDocument('left'));
            const history = hooks.createHistoryController(model, {
                selection: { blockId: 'p1', offset: 0, affinity: 'after', virtualCaret: true, isCollapsed: true }
            });

            history.commitOperation(hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 0, affinity: 'after', virtualCaret: true },
                text: 'Text vedle'
            }, { source: 'typing', timestamp: 1000 }), {
                transactionType: 'typing',
                label: 'Typing beside image',
                beforeSelection: { blockId: 'p1', offset: 0, affinity: 'after', virtualCaret: true, isCollapsed: true }
            });

            assert.strictEqual(hooks.getBlockText(model.body.blocks[0]), 'Text vedle');
            assert.strictEqual(hooks.findDrawingRunByObjectId(model, 'phase11-left-object').blockId, 'p1');
            assert.strictEqual(model.body.blocks[0].content.runs[0].kind, 'drawing');
            assert.strictEqual(model.body.blocks[0].content.runs[1].text, 'Text vedle');

            const undo = history.undo();
            assert.strictEqual(undo.selection.blockId, 'p1');
            assert.strictEqual(undo.selection.offset, 0);
            assert.strictEqual(hooks.getBlockText(model.body.blocks[0]), '');
            assert.strictEqual(model.body.blocks[0].content.runs.length, 1);
            assert.strictEqual(model.body.blocks[0].content.runs[0].kind, 'drawing');
            assert.strictEqual(model.body.blocks[0].content.runs[0].objectId, 'phase11-left-object');

            history.redo();
            assert.strictEqual(hooks.getBlockText(model.body.blocks[0]), 'Text vedle');
            assert.strictEqual(hooks.findDrawingRunByObjectId(model, 'phase11-left-object').blockId, 'p1');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "undo");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase11-{scenario}-{Guid.NewGuid():N}.js");
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

        function createRootForImage(imageRect, objectId) {
            const paragraphRect = { x: 40, y: 120, width: 420, height: 96 };
            const imageNode = {
                getBoundingClientRect() { return imageRect; }
            };
            const paragraphNode = {
                getBoundingClientRect() { return paragraphRect; },
                querySelector(selector) {
                    return selector.includes(objectId) ? imageNode : null;
                }
            };
            return {
                querySelector(selector) {
                    return selector.includes('data-block-id="p1"') ? paragraphNode : null;
                }
            };
        }

        function createDrawingOnlyDocument(side) {
            const right = side === 'right';
            const objectId = right ? 'phase11-right-object' : 'phase11-left-object';
            const align = right ? 2 : 0;
            return {
                DocumentId: 'image-drawing-phase11-' + side,
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: {
                        $type: 'paragraph',
                        Style: {
                            fontFamily: 'Georgia',
                            fontSize: 18
                        },
                        Inlines: [{
                            $type: 'drawing',
                            Id: objectId + '-run',
                            ObjectId: objectId,
                            Kind: 0,
                            Source: 0,
                            Url: '/' + objectId + '.png',
                            AltText: 'Phase 11 ' + side + ' square image',
                            CommentIds: ['image-comment'],
                            Size: { Width: 96, Height: 64 },
                            Layout: {
                                Kind: 1,
                                Wrap: { Mode: 1, DistanceLeft: 8, DistanceRight: 8 },
                                Anchor: { BlockId: 'p1', Offset: 0, InlineIndex: 0, MoveWithText: true },
                                Position: {
                                    HorizontalRelativeTo: 2,
                                    HorizontalAlignment: align,
                                    VerticalRelativeTo: 3,
                                    VerticalAlignment: 1,
                                    X: 0,
                                    Y: 0
                                },
                                Transform: { Width: 96, Height: 64 }
                            }
                        }]
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
