using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase16ResizeTrackJavaScriptTests
{
    [Fact]
    public async Task Phase16_PointerDownOnHandleCreatesResizeTrack()
    {
        var result = await RunScenarioAsync(
            "predrag",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'se' });
            const state = harness.begin(196, 184);

            assert.strictEqual(state.track.mode, 'resize');
            assert.strictEqual(state.resizeTrack.mode, 'resize');
            assert.strictEqual(state.track.stage, 'predrag');
            assert.strictEqual(state.track.active, false);
            assert.strictEqual(state.track.handleName, 'se');
            assert.strictEqual(state.track.handleIndex, 4);
            assert.strictEqual(state.track.originalRect.width, 96);
            assert.strictEqual(state.track.originalRect.height, 64);
            assert.strictEqual(state.track.originalTransform.Width, 96);
            assert.strictEqual(state.track.originalTransform.Height, 64);
            assert.strictEqual(state.node.trackState, 'predrag');
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_ResizeTrackStoresHandleIndexAndFixedPoint()
    {
        var result = await RunScenarioAsync(
            "fixed-point",
            """
            const southEast = hooks.createImageResizeTrackHarness({ handleName: 'se' }).begin(196, 184);
            const northWest = hooks.createImageResizeTrackHarness({ handleName: 'nw' }).begin(100, 120);

            assert.strictEqual(southEast.track.handleIndex, 4);
            assert.strictEqual(southEast.track.fixedPoint.x, 100);
            assert.strictEqual(southEast.track.fixedPoint.y, 120);
            assert.strictEqual(northWest.track.handleIndex, 0);
            assert.strictEqual(northWest.track.fixedPoint.x, 196);
            assert.strictEqual(northWest.track.fixedPoint.y, 184);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_PointerMoveChangesPreviewWidthAndHeight()
    {
        var result = await RunScenarioAsync(
            "preview-size",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'se' });
            harness.begin(196, 184);
            const state = harness.move(236, 214);

            assert.strictEqual(state.track.stage, 'dragging');
            assert.strictEqual(state.track.active, true);
            assert.ok(state.track.previewWidth > 96, JSON.stringify(state.track));
            assert.ok(state.track.previewHeight > 64, JSON.stringify(state.track));
            assert.ok(state.node.width.endsWith('px'), state.node.width);
            assert.ok(state.node.height.endsWith('px'), state.node.height);
            assert.strictEqual(state.node.trackState, 'active');
            assert.ok(state.track.resizeBadgeText.includes(' x '), state.track.resizeBadgeText);
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_PointerMoveDoesNotMutatePersistentModel()
    {
        var result = await RunScenarioAsync(
            "model-stable",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'se' });
            const before = harness.begin(196, 184).modelJson;
            const after = harness.move(246, 224).modelJson;

            assert.strictEqual(after, before, 'resize pointermove must be preview-only');
            assert.strictEqual(harness.state().commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_ShiftResizePreservesAspectRatioOnSideHandle()
    {
        var result = await RunScenarioAsync(
            "shift-aspect",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'e' });
            harness.begin(196, 152);
            const state = harness.move(236, 152, { shiftKey: true });
            const ratio = state.track.previewWidth / state.track.previewHeight;

            assert.strictEqual(state.track.previewPreserveAspectRatio, true);
            assert.ok(Math.abs(ratio - 1.5) < 0.02, ratio);
            assert.ok(state.track.previewWidth > 96, JSON.stringify(state.track));
            assert.ok(state.track.previewHeight > 64, JSON.stringify(state.track));
            assert.ok(state.track.appliedDelta.y < 0, JSON.stringify(state.track));
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_MinimumWidthAndHeightAreRespected()
    {
        var result = await RunScenarioAsync(
            "minimum",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'nw', minWidth: 32, minHeight: 24 });
            harness.begin(100, 120);
            const state = harness.move(500, 500);

            assert.ok(state.track.previewWidth >= 32, JSON.stringify(state.track));
            assert.ok(state.track.previewHeight >= 24, JSON.stringify(state.track));
            assert.strictEqual(state.track.previewWidth, 36);
            assert.strictEqual(state.track.previewHeight, 24);
            assert.strictEqual(state.node.width, '36px');
            assert.strictEqual(state.node.height, '24px');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_EscapeCancelsResizeAndRestoresPreviewNode()
    {
        var result = await RunScenarioAsync(
            "escape",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'se' });
            harness.begin(196, 184);
            harness.move(236, 214);
            const state = harness.escape();

            assert.strictEqual(state.track.cancelled, true);
            assert.strictEqual(state.track.stage, 'escape');
            assert.strictEqual(state.resizeTrack, null);
            assert.strictEqual(state.node.trackState, '');
            assert.strictEqual(state.node.transform, '');
            assert.strictEqual(state.node.width, '');
            assert.strictEqual(state.node.height, '');
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_PointerUpCommitsOneUpdateImageLayoutOperation()
    {
        var result = await RunScenarioAsync(
            "commit",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'se' });
            const before = harness.begin(196, 184).modelJson;
            harness.move(236, 214);
            const committed = harness.up(236, 214);
            const secondUp = harness.up(260, 240);

            assert.notStrictEqual(committed.modelJson, before, 'pointerup must commit the resized layout');
            assert.strictEqual(committed.commitCount, 1);
            assert.strictEqual(secondUp.commitCount, 1);
            assert.strictEqual(committed.commits[0].type, 'UpdateImageLayout');
            assert.ok(committed.commits[0].layout.Transform.Width > 96, JSON.stringify(committed.commits[0]));
            assert.ok(committed.commits[0].layout.Transform.Height > 64, JSON.stringify(committed.commits[0]));
            assert.strictEqual(committed.commits[0].layout.Transform.LockAspectRatio, true);
            assert.strictEqual(committed.node.trackState, '');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase16_UpdateImageLayoutUndoRedoRestoresSizeAndCompensatedPosition()
    {
        var result = await RunScenarioAsync(
            "undo-redo",
            """
            const harness = hooks.createImageResizeTrackHarness({ handleName: 'nw' });
            harness.begin(100, 120);
            harness.move(84, 108);
            const committed = harness.up(84, 108);
            const resizedLayout = committed.commits[0].layout;
            const undo = hooks.createOperation('UpdateImageLayout', committed.commits[0].operation).getReversed();
            const undoResult = hooks.applyOperation(harness.model, undo);
            if (!undoResult || undoResult.ok === false) throw new Error(JSON.stringify(undoResult && undoResult.errors || undoResult));
            const afterUndo = hooks.findDrawingRunByObjectId(harness.model, 'phase14-object').run.layout;

            assert.strictEqual(afterUndo.Transform.Width, 96);
            assert.strictEqual(afterUndo.Transform.Height, 64);
            assert.strictEqual(afterUndo.Position.X, 100);
            assert.strictEqual(afterUndo.Position.Y, 120);

            const redo = hooks.createOperation('UpdateImageLayout', committed.commits[0].operation);
            const redoResult = hooks.applyOperation(harness.model, redo);
            if (!redoResult || redoResult.ok === false) throw new Error(JSON.stringify(redoResult && redoResult.errors || redoResult));
            const afterRedo = hooks.findDrawingRunByObjectId(harness.model, 'phase14-object').run.layout;

            assert.strictEqual(afterRedo.Transform.Width, resizedLayout.Transform.Width);
            assert.strictEqual(afterRedo.Transform.Height, resizedLayout.Transform.Height);
            assert.strictEqual(afterRedo.Position.X, resizedLayout.Position.X);
            assert.strictEqual(afterRedo.Position.Y, resizedLayout.Position.Y);
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageDrawingPhase16NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageDrawingPhase16NodeResult(0, "OK", string.Empty);
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

    private static async Task<DocumentEditorImageDrawingPhase16NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase16-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageDrawingPhase16NodeResult(process.ExitCode, stdout, stderr);
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

internal sealed record DocumentEditorImageDrawingPhase16NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageDrawingPhase16Assertions
{
    public static void ShouldPass(this DocumentEditorImageDrawingPhase16NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
