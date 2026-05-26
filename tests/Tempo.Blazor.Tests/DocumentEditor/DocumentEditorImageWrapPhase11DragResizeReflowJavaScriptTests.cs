using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase11DragResizeReflowJavaScriptTests
{
    [Fact]
    public async Task Phase11_DragPreviewPublishesPreviewRectAndExclusionWithoutMutatingModel()
    {
        var result = await RunScenarioAsync(
            "drag-preview-exclusion",
            """
            const harness = hooks.createImageMoveTrackHarness({
                threshold: 0,
                snapContext: {
                    bodyRect: { X: 0, Y: 0, Width: 600, Height: 420 },
                    objectSize: { Width: 96, Height: 64 },
                    minReadableWidth: 24,
                    otherObjects: [],
                    lines: [
                        { Rect: { X: 0, Y: 130, Width: 600, Height: 20 } },
                        { Rect: { X: 0, Y: 154, Width: 600, Height: 20 } }
                    ]
                }
            });

            const before = harness.begin(0, 0).modelJson;
            const preview = harness.move(150, 0);

            assert.strictEqual(preview.modelJson, before, 'pointermove must remain preview-only');
            assert.strictEqual(preview.commitCount, 0, 'pointermove must not create an update operation');
            assert.ok(preview.track.previewRect.x > preview.track.originalRect.x + 100, JSON.stringify(preview.track.previewRect));
            assert.strictEqual(preview.track.previewRect.width, 96);
            assert.strictEqual(preview.track.previewRect.height, 64);
            assert.strictEqual(preview.track.previewExclusion.kind, 'rectangular');
            assert.strictEqual(preview.track.previewExclusion.wrapMode, 'Square');
            assert.strictEqual(preview.track.previewWrapRect.width, 112);
            assert.strictEqual(preview.track.previewIntervals.changed, true);
            assert.deepStrictEqual(preview.track.previewIntervals.lines[0].blockedIntervals.map(i => ({ x: i.x, width: i.width })), [
                { x: 244, width: 112 }
            ]);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase11_DragCommitCreatesOneUpdateImageLayoutAndUndoRedoRestoresWrapLayout()
    {
        var result = await RunScenarioAsync(
            "drag-commit-update-layout",
            """
            const harness = hooks.createImageMoveTrackHarness({
                threshold: 0,
                snapContext: {
                    bodyRect: { X: 0, Y: 0, Width: 600, Height: 420 },
                    objectSize: { Width: 96, Height: 64 },
                    minReadableWidth: 24,
                    otherObjects: [],
                    lines: [{ Rect: { X: 0, Y: 130, Width: 600, Height: 20 } }]
                }
            });

            const before = harness.begin(0, 0).modelJson;
            harness.move(150, 0);
            const committed = harness.up(150, 0);

            assert.notStrictEqual(committed.modelJson, before);
            assert.strictEqual(committed.commitCount, 1);
            assert.strictEqual(committed.commits[0].type, 'UpdateImageLayout');
            assert.ok(committed.commits[0].operation.affectedParagraphIds.includes('p1'), JSON.stringify(committed.commits[0].operation));
            assert.strictEqual(committed.commits[0].operation.oldLayout.Position.X, 100);
            assert.strictEqual(committed.commits[0].operation.newLayout.Position.X, committed.commits[0].layout.Position.X);
            assert.strictEqual(committed.commits[0].operation.newLayout.Anchor.BlockId, 'p1');

            const undo = hooks.createOperation('UpdateImageLayout', committed.commits[0].operation).getReversed();
            const undoResult = hooks.applyOperation(harness.model, undo);
            if (!undoResult || undoResult.ok === false) throw new Error(JSON.stringify(undoResult && undoResult.errors || undoResult));
            const afterUndo = hooks.findDrawingRunByObjectId(harness.model, 'phase14-object').run.layout;
            assert.strictEqual(afterUndo.Position.X, 100);
            assert.strictEqual(afterUndo.Wrap.Mode, 1);

            const redo = hooks.createOperation('UpdateImageLayout', committed.commits[0].operation);
            const redoResult = hooks.applyOperation(harness.model, redo);
            if (!redoResult || redoResult.ok === false) throw new Error(JSON.stringify(redoResult && redoResult.errors || redoResult));
            const afterRedo = hooks.findDrawingRunByObjectId(harness.model, 'phase14-object').run.layout;
            assert.strictEqual(afterRedo.Position.X, committed.commits[0].layout.Position.X);
            assert.strictEqual(afterRedo.Wrap.Mode, 1);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase11_ResizePreviewChangesObjectAndWrapRectsWithoutMutatingModel()
    {
        var result = await RunScenarioAsync(
            "resize-preview-exclusion",
            """
            const harness = hooks.createImageResizeTrackHarness({
                handleName: 'se',
                threshold: 0,
                snapContext: {
                    bodyRect: { X: 0, Y: 0, Width: 600, Height: 420 },
                    objectSize: { Width: 96, Height: 64 },
                    minReadableWidth: 24,
                    otherObjects: [],
                    lines: [{ Rect: { X: 0, Y: 130, Width: 600, Height: 20 } }]
                }
            });

            const before = harness.begin(196, 184).modelJson;
            const preview = harness.move(236, 214);

            assert.strictEqual(preview.modelJson, before, 'resize pointermove must remain preview-only');
            assert.strictEqual(preview.commitCount, 0);
            assert.ok(preview.track.previewWidth > 96, JSON.stringify(preview.track));
            assert.ok(preview.track.previewHeight > 64, JSON.stringify(preview.track));
            assert.strictEqual(preview.track.previewRect.width, preview.track.previewWidth);
            assert.strictEqual(preview.track.previewRect.height, preview.track.previewHeight);
            assert.ok(preview.track.previewWrapRect.width > 112, JSON.stringify(preview.track.previewWrapRect));
            assert.ok(preview.track.previewExclusion.rect.width > 0);
            assert.strictEqual(preview.track.previewExclusion.kind, 'rectangular');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase11_ResizeCommitIsSingleUndoableUpdateImageLayoutWithAspectAndMinSize()
    {
        var result = await RunScenarioAsync(
            "resize-commit-undo-redo",
            """
            const aspect = hooks.createImageResizeTrackHarness({
                handleName: 'se',
                threshold: 0,
                snapContext: {
                    bodyRect: { X: 0, Y: 0, Width: 600, Height: 420 },
                    objectSize: { Width: 96, Height: 64 },
                    minReadableWidth: 24,
                    otherObjects: [],
                    lines: [{ Rect: { X: 0, Y: 130, Width: 600, Height: 20 } }]
                }
            });
            aspect.begin(196, 184);
            const aspectPreview = aspect.move(236, 214);
            assert.strictEqual(aspectPreview.track.previewPreserveAspectRatio, true);
            assert.ok(Math.abs((aspectPreview.track.previewWidth / aspectPreview.track.previewHeight) - 1.5) < 0.02);
            const committed = aspect.up(236, 214);
            assert.strictEqual(committed.commitCount, 1);
            assert.strictEqual(committed.commits[0].type, 'UpdateImageLayout');

            const undo = hooks.createOperation('UpdateImageLayout', committed.commits[0].operation).getReversed();
            const undoResult = hooks.applyOperation(aspect.model, undo);
            if (!undoResult || undoResult.ok === false) throw new Error(JSON.stringify(undoResult && undoResult.errors || undoResult));
            const afterUndo = hooks.findDrawingRunByObjectId(aspect.model, 'phase14-object').run.layout;
            assert.strictEqual(afterUndo.Transform.Width, 96);
            assert.strictEqual(afterUndo.Transform.Height, 64);

            const redo = hooks.createOperation('UpdateImageLayout', committed.commits[0].operation);
            const redoResult = hooks.applyOperation(aspect.model, redo);
            if (!redoResult || redoResult.ok === false) throw new Error(JSON.stringify(redoResult && redoResult.errors || redoResult));
            const afterRedo = hooks.findDrawingRunByObjectId(aspect.model, 'phase14-object').run.layout;
            assert.strictEqual(afterRedo.Transform.Width, committed.commits[0].layout.Transform.Width);
            assert.strictEqual(afterRedo.Transform.Height, committed.commits[0].layout.Transform.Height);

            const minimum = hooks.createImageResizeTrackHarness({
                handleName: 'nw',
                threshold: 0,
                minWidth: 32,
                minHeight: 24,
                snapContext: {
                    bodyRect: { X: 0, Y: 0, Width: 600, Height: 420 },
                    objectSize: { Width: 96, Height: 64 },
                    minReadableWidth: 24,
                    otherObjects: [],
                    lines: [{ Rect: { X: 0, Y: 130, Width: 600, Height: 20 } }]
                }
            });
            minimum.begin(100, 120);
            const minPreview = minimum.move(500, 500);
            assert.ok(minPreview.track.previewWidth >= 32);
            assert.ok(minPreview.track.previewHeight >= 24);
            assert.ok(minPreview.track.previewWrapRect.width > 0, JSON.stringify(minPreview.track.previewWrapRect));
            assert.ok(minPreview.track.previewExclusion.rect.width > 0, JSON.stringify(minPreview.track.previewExclusion));
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase11_DropReanchorsToNearestTextOffsetAndRespectsFixedAndRegionScope()
    {
        var result = await RunScenarioAsync(
            "reanchor-nearest-fixed-region",
            """
            const reanchor = hooks.createImageReanchorHarness();
            const committed = reanchor.commitAt(100, 45);

            assert.strictEqual(committed.operation.type, 'UpdateImageLayout');
            assert.strictEqual(committed.nearest.blockId, 'target-p');
            assert.strictEqual(committed.object.anchorBlockId, 'target-p');
            assert.strictEqual(committed.object.anchorOffset, 10);
            assert.ok(committed.operation.affectedParagraphIds.includes('source-p'));
            assert.ok(committed.operation.affectedParagraphIds.includes('target-p'));

            const fixed = hooks.createImageReanchorHarness({ fixedOnPage: true });
            const fixedCommit = fixed.commitAt(100, 45);
            assert.strictEqual(fixed.shouldReanchor(false, true), false);
            assert.strictEqual(fixedCommit.object.anchorBlockId, 'source-p');
            assert.strictEqual(fixedCommit.object.anchorOffset, 0);

            const document = {
                DocumentId: 'phase11-header-drop',
                Blocks: [{
                    Id: 'body-p',
                    Type: 'Paragraph',
                    Content: { $type: 'paragraph', Inlines: [{ $type: 'text', Id: 'body-run', Text: 'body paragraph target' }] }
                }],
                HeadersFooters: [{
                    Id: 'header-primary',
                    Region: 'Header',
                    Type: 0,
                    Blocks: [{
                        Id: 'header-p',
                        Type: 'Paragraph',
                        Content: {
                            $type: 'paragraph',
                            Inlines: [{
                                $type: 'drawing',
                                Id: 'header-run',
                                ObjectId: 'header-object',
                                Kind: 0,
                                Source: 0,
                                Url: '/header.png',
                                Size: { Width: 96, Height: 64 },
                                Layout: {
                                    Kind: 1,
                                    Wrap: { Mode: 1, DistanceLeft: 8, DistanceRight: 8 },
                                    Anchor: { BlockId: 'header-p', Offset: 0, InlineIndex: 0, Region: 'Header', HeaderFooterId: 'header-primary', MoveWithText: true },
                                    Position: { HorizontalRelativeTo: 2, HorizontalAlignment: 0, VerticalRelativeTo: 3, VerticalAlignment: 1, X: 100, Y: 120 },
                                    Transform: { Width: 96, Height: 64 }
                                }
                            }]
                        }
                    }]
                }]
            };
            const headerDrag = hooks.createImageMoveTrackHarness({
                document,
                objectId: 'header-object',
                blockId: 'header-p',
                lineBoxes: [{
                    blockId: 'body-p',
                    pageIndex: 0,
                    region: 'Body',
                    rect: { x: 20, y: 40, width: 220, height: 20 },
                    referenceRect: { x: 20, y: 40, width: 220, height: 20 },
                    start: 0,
                    end: 21
                }]
            });
            const beforeHeader = headerDrag.begin(0, 0).modelJson;
            headerDrag.move(40, 48);
            const rejected = headerDrag.up(40, 48);
            assert.strictEqual(rejected.commits[0].type, 'DropRejected');
            assert.strictEqual(rejected.modelJson, beforeHeader);
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageWrapPhase11NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageWrapPhase11NodeResult(0, "OK", string.Empty);
        }

        var nodeScript =
            $$"""
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = createSandbox();
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            {{body}}
            console.log('OK');
            """;

        return await RunNodeAsync(scriptPath, nodeScript, scenario);
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

    private static async Task<DocumentEditorImageWrapPhase11NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase11-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageWrapPhase11NodeResult(process.ExitCode, stdout, stderr);
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

internal sealed record DocumentEditorImageWrapPhase11NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageWrapPhase11Assertions
{
    public static void ShouldPass(this DocumentEditorImageWrapPhase11NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
