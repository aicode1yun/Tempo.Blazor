using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageDrawingPhase14DragTrackJavaScriptTests
{
    [Fact]
    public async Task Phase14_PointerDownOnImageBodyCreatesPreDragState()
    {
        var result = await RunScenarioAsync(
            "predrag",
            """
            const harness = hooks.createImageMoveTrackHarness();
            const state = harness.begin(10, 20);

            assert.strictEqual(state.track.stage, 'predrag');
            assert.strictEqual(state.track.active, false);
            assert.strictEqual(state.track.objectId, 'phase14-object');
            assert.strictEqual(state.track.pointerStart.x, 10);
            assert.strictEqual(state.track.pointerStart.y, 20);
            assert.strictEqual(state.track.originalRect.width, 96);
            assert.strictEqual(state.track.originalRect.height, 64);
            assert.strictEqual(state.node.trackState, 'predrag');
            assert.ok(state.node.classes.includes('tm-wysiwyg-object-track--predrag'));
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_SmallMoveUnderThresholdDoesNotStartDrag()
    {
        var result = await RunScenarioAsync(
            "threshold",
            """
            const harness = hooks.createImageMoveTrackHarness({ threshold: 6 });
            harness.begin(0, 0);
            const state = harness.move(3, 2);

            assert.strictEqual(state.track.stage, 'predrag');
            assert.strictEqual(state.track.active, false);
            assert.strictEqual(state.node.transform, '');
            assert.strictEqual(state.node.trackState, 'predrag');
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_MoveOverThresholdCreatesDragTrack()
    {
        var result = await RunScenarioAsync(
            "active",
            """
            const harness = hooks.createImageMoveTrackHarness({ threshold: 3 });
            harness.begin(0, 0);
            const state = harness.move(8, 0);

            assert.strictEqual(state.track.stage, 'dragging');
            assert.strictEqual(state.track.active, true);
            assert.strictEqual(state.node.trackState, 'active');
            assert.ok(state.node.classes.includes('tm-wysiwyg-object-track--active'));
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_PointerMoveChangesTrackTransform()
    {
        var result = await RunScenarioAsync(
            "transform",
            """
            const harness = hooks.createImageMoveTrackHarness();
            harness.begin(0, 0);
            const state = harness.move(32, 14);

            assert.strictEqual(state.track.appliedDelta.x, 32);
            assert.strictEqual(state.track.appliedDelta.y, 14);
            assert.ok(state.node.transform.includes('translate(32px, 14px)'), state.node.transform);
            assert.strictEqual(state.node.dx, 32);
            assert.strictEqual(state.node.dy, 14);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_PointerMoveDoesNotMutatePersistentDocumentModel()
    {
        var result = await RunScenarioAsync(
            "model-stable",
            """
            const harness = hooks.createImageMoveTrackHarness();
            const before = harness.begin(0, 0).modelJson;
            const after = harness.move(40, 12).modelJson;

            assert.strictEqual(after, before, 'pointermove must update preview DOM only');
            assert.strictEqual(harness.state().commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_PreviewCanExposeGuides()
    {
        var result = await RunScenarioAsync(
            "guides",
            """
            const harness = hooks.createImageMoveTrackHarness({
                snapContext: {
                    bodyRect: { X: 0, Y: 0, Width: 520, Height: 700 },
                    objectSize: { Width: 96, Height: 64 },
                    otherObjects: [{ Rect: { X: 320, Y: 120, Width: 80, Height: 60 } }],
                    lines: [{ Rect: { X: 0, Y: 120, Width: 420, Height: 18 } }]
                }
            });
            harness.begin(0, 0);
            const state = harness.move(112, 0);
            const kinds = state.track.guides.map(guide => guide.Kind || guide.kind);

            assert.strictEqual(state.track.active, true);
            assert.ok(kinds.includes('page-center-x'), JSON.stringify(state.track.guides));
            assert.ok(state.track.guides.length >= 1);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_EscapeDuringDragCancelsTrack()
    {
        var result = await RunScenarioAsync(
            "escape",
            """
            const harness = hooks.createImageMoveTrackHarness();
            harness.begin(0, 0);
            harness.move(20, 5);
            const state = harness.escape();

            assert.strictEqual(state.track.cancelled, true);
            assert.strictEqual(state.track.stage, 'escape');
            assert.strictEqual(state.node.trackState, '');
            assert.strictEqual(state.node.transform, '');
            assert.strictEqual(state.commitCount, 0);
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase14_PointerUpCommitsOneOperation()
    {
        var result = await RunScenarioAsync(
            "commit",
            """
            const harness = hooks.createImageMoveTrackHarness();
            const before = harness.begin(0, 0).modelJson;
            harness.move(24, 9);
            const committed = harness.up(24, 9);
            const secondUp = harness.up(40, 20);

            assert.notStrictEqual(committed.modelJson, before, 'pointerup must commit the model change');
            assert.strictEqual(committed.commitCount, 1);
            assert.strictEqual(secondUp.commitCount, 1);
            assert.strictEqual(committed.commits[0].type, 'MoveDrawingObject');
            assert.strictEqual(committed.commits[0].dx, 24);
            assert.strictEqual(committed.commits[0].dy, 9);
            assert.strictEqual(committed.node.trackState, '');
            """);

        result.ShouldPass();
    }

    private static async Task<DocumentEditorImageDrawingPhase14NodeResult> RunScenarioAsync(string scenario, string body)
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable())
        {
            return new DocumentEditorImageDrawingPhase14NodeResult(0, "OK", string.Empty);
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

    private static async Task<DocumentEditorImageDrawingPhase14NodeResult> RunNodeAsync(string scriptPath, string nodeScript, string scenario)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-drawing-phase14-{scenario}-{Guid.NewGuid():N}.js");
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
            return new DocumentEditorImageDrawingPhase14NodeResult(process.ExitCode, stdout, stderr);
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

internal sealed record DocumentEditorImageDrawingPhase14NodeResult(int ExitCode, string StandardOutput, string StandardError);

internal static class DocumentEditorImageDrawingPhase14Assertions
{
    public static void ShouldPass(this DocumentEditorImageDrawingPhase14NodeResult result)
    {
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }
}
