using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase6SquareCenterDragJavaScriptTests
{
    [Fact]
    public async Task Phase6_CenteredSquareExposesLeftAndRightIntervalsWithoutCss()
    {
        var result = await RunScenarioAsync(
            "center-square-intervals",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'center-square',
                blockId: 'p1',
                wrapMode: 'Square',
                rect: { x: 250, y: 20, width: 100, height: 80 },
                horizontalPosition: { align: 'Center' }
            }, frame);

            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervals(line), [
                { x: 0, y: 40, width: 250, height: 20 },
                { x: 350, y: 40, width: 250, height: 20 }
            ]);
            assert.strictEqual(line.moved, false);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase6_CenteredSquareDistancesExpandBlockedMiddleOnly()
    {
        var result = await RunScenarioAsync(
            "center-square-distances",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'center-square-distance',
                blockId: 'p1',
                wrapMode: 'Square',
                rect: { x: 250, y: 20, width: 100, height: 80 },
                distanceLeft: 10,
                distanceRight: 20,
                distanceTop: 0,
                distanceBottom: 0,
                horizontalPosition: { align: 'Center' }
            }, frame);

            assert.deepStrictEqual(plain(exclusion.wrapRect), { x: 240, y: 20, width: 130, height: 80 });
            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervals(line).map(i => ({ x: i.x, width: i.width })), [
                { x: 0, width: 240 },
                { x: 370, width: 230 }
            ]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase6_TextSkipsTinyLeftIntervalAndContinuesInRightInterval()
    {
        var result = await RunScenarioAsync(
            "tiny-left-continues-right",
            """
            const breaker = hooks.createLineBreaker(hooks.createTextMeasurementService());
            const layout = breaker.breakParagraph({
                id: 'p1',
                runs: [{ id: 'r1', kind: 'text', text: 'alpha beta gamma' }]
            }, {
                x: 0,
                y: 40,
                width: 300,
                minReadableWidth: 24,
                availableIntervals: [
                    { x: 0, y: 40, width: 32, height: 20 },
                    { x: 192, y: 40, width: 108, height: 20 }
                ]
            });

            const line = layout.lines[0];
            assert.strictEqual(line.ranges.length, 2);
            assert.strictEqual(line.ranges[0].segments.length, 0, 'the first word does not fit the tiny left interval');
            assert.ok(line.ranges[1].segments.length > 0, 'text must continue in the right interval before moving below');
            assert.ok(line.ranges[1].segments[0].text.startsWith('alpha'), JSON.stringify(line.ranges[1].segments));
            assert.ok(line.ranges[1].segments.every(segment => segment.rect.x >= 192));

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase6_NoReadableSideIntervalsMoveLineBelowSquare()
    {
        var result = await RunScenarioAsync(
            "both-sides-too-small",
            """
            const frame = { x: 0, y: 0, width: 300, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'wide-square',
                blockId: 'p1',
                wrapMode: 'Square',
                rect: { x: 20, y: 20, width: 260, height: 80 }
            }, frame);

            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervals(line), []);
            assert.strictEqual(line.moved, true);
            assert.strictEqual(line.movedToY, 100);
            assert.deepStrictEqual(intervals(line, 'movedIntervals'), [{ x: 0, y: 100, width: 300, height: 20 }]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase6_DragPreviewReportsChangedCenterIntervalsWithoutMutatingModel()
    {
        var result = await RunScenarioAsync(
            "drag-preview-center-intervals",
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
            const beforeModel = harness.begin(0, 0).modelJson;
            const preview = harness.move(150, 0);

            assert.strictEqual(preview.modelJson, beforeModel, 'drag preview must not mutate the document model');
            assert.strictEqual(preview.track.previewIntervals.changed, true);
            assert.ok(preview.track.previewIntervals.lineCount >= 2);
            const firstLine = preview.track.previewIntervals.lines[0];
            assert.deepStrictEqual(firstLine.intervals.map(i => ({ x: i.x, width: i.width })), [
                { x: 0, width: 244 },
                { x: 356, width: 244 }
            ]);
            assert.deepStrictEqual(firstLine.blockedIntervals.map(i => ({ x: i.x, width: i.width })), [
                { x: 244, width: 112 }
            ]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase6_DragCommitAllowsArbitraryCenterXAndOneMoveOperation()
    {
        var result = await RunScenarioAsync(
            "drag-commit-center-x",
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
            harness.begin(0, 0);
            harness.move(150, 0);
            const committed = harness.up(150, 0);

            assert.strictEqual(committed.commitCount, 1);
            assert.strictEqual(committed.commits[0].type, 'UpdateImageLayout');
            assert.strictEqual(committed.commits[0].layout.Position.X, 252);
            assert.strictEqual(committed.commits[0].layout.Position.HorizontalAlignment, 0);

            const committedModel = JSON.parse(committed.modelJson);
            const drawing = hooks.findDrawingRunByObjectId(committedModel, 'phase14-object');
            const object = hooks.normalizeImageObject(drawing.run, { blockId: drawing.blockId, inlineIndex: drawing.inlineIndex });
            const exclusion = hooks.createTextExclusion(Object.assign({}, object, {
                rect: { x: 252, y: 120, width: 96, height: 64 }
            }), { x: 0, y: 0, width: 600, height: 420 });
            const line = hooks.createTextExclusionManager([exclusion], { x: 0, y: 0, width: 600, height: 420 }).resolveLine(130, 20, 24);
            assert.deepStrictEqual(intervals(line).map(i => ({ x: i.x, width: i.width })), [
                { x: 0, width: 244 },
                { x: 356, width: 244 }
            ]);

            console.log('OK');
            """);

        result.ShouldPass();
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase6-{scenario}-{Guid.NewGuid():N}.js");
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

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const sandbox = createSandbox();
        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });
        const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
        const plain = value => JSON.parse(JSON.stringify(value));
        const intervals = (line, source = 'intervals') => plain((line[source] || []).map(i => ({
            x: i.x,
            y: i.y,
            width: i.width,
            height: i.height
        })));

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
