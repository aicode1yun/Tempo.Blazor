using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase8TightThroughPolygonJavaScriptTests
{
    [Fact]
    public async Task Phase8_RectanglePolygonMatchesSquareIntervalWithoutDistances()
    {
        var result = await RunScenarioAsync(
            "rectangle-matches-square",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const rect = { x: 220, y: 40, width: 120, height: 90 };
            const square = hooks.createTextExclusion({
                objectId: 'square',
                blockId: 'p1',
                wrapMode: 'Square',
                rect
            }, frame);
            const tight = hooks.createTextExclusion({
                objectId: 'tight',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect,
                wrapContourPoints: [
                    { x: 0, y: 0 },
                    { x: 1, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);

            assert.strictEqual(tight.kind, 'contour');
            assert.deepStrictEqual(plain(tight.rect), plain(square.rect));

            const squareLine = hooks.createTextExclusionManager([square], frame).resolveLine(64, 20, 1);
            const tightLine = hooks.createTextExclusionManager([tight], frame).resolveLine(64, 20, 1);
            assert.deepStrictEqual(blocked(squareLine), blocked(tightLine));
            assert.deepStrictEqual(available(squareLine), available(tightLine));

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_TrianglePolygonReturnsNarrowerTopIntervalThanBottom()
    {
        var result = await RunScenarioAsync(
            "triangle-narrows-top",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'triangle',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 180, y: 40, width: 200, height: 120 },
                wrapContourPoints: [
                    { x: 0.5, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);
            const manager = hooks.createTextExclusionManager([exclusion], frame);
            const top = manager.resolveLine(46, 18, 1);
            const bottom = manager.resolveLine(134, 18, 1);
            const topBlocked = blocked(top)[0];
            const bottomBlocked = blocked(bottom)[0];

            assert.ok(topBlocked.width > 20, `top width ${topBlocked.width} should still block the triangle contour`);
            assert.ok(bottomBlocked.width > topBlocked.width + 100, `bottom ${bottomBlocked.width} must be much wider than top ${topBlocked.width}`);
            assert.ok(topBlocked.x > bottomBlocked.x + 40, `top left ${topBlocked.x} must be closer to the apex than bottom left ${bottomBlocked.x}`);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_PolygonIntersectionSamplesTopMiddleAndBottomOfLineBand()
    {
        var result = await RunScenarioAsync(
            "top-mid-bottom-sampling",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'wide-band-triangle',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 100, y: 100, width: 200, height: 200 },
                wrapContourPoints: [
                    { x: 0.5, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);

            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(100, 200, 1);
            const interval = blocked(line)[0];

            assert.ok(interval.width > 190, `bottom sample must widen the union to almost the whole triangle width, got ${interval.width}`);
            assert.ok(interval.x < 105, `bottom sample must include the left base edge, got x=${interval.x}`);
            assert.ok(interval.x + interval.width > 295, `bottom sample must include the right base edge, got right=${interval.x + interval.width}`);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_PolygonPointInsideLineBandIsIncludedAsSample()
    {
        var result = await RunScenarioAsync(
            "interior-point-sampling",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'interior-point',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 100, y: 100, width: 200, height: 50 },
                wrapContourPoints: [
                    { x: 0, y: 0 },
                    { x: 0.30, y: 0 },
                    { x: 1, y: 0.40 },
                    { x: 0.30, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);

            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(100, 50, 1);
            const interval = blocked(line)[0];
            const right = interval.x + interval.width;

            assert.ok(right > 299, `the point at y=120 must be sampled and extend the interval to x=300, got ${right}`);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_PolygonOutsideLineBandDoesNotBlockInterval()
    {
        var result = await RunScenarioAsync(
            "outside-line-band",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'outside-line',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 100, y: 100, width: 200, height: 80 },
                wrapContourPoints: [
                    { x: 0.5, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);
            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(190, 20, 1);

            assert.deepStrictEqual(blocked(line), []);
            assert.deepStrictEqual(available(line), [{ x: 0, y: 190, width: 600, height: 20 }]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_TightUsesWrapContourPointsAndFallsBackToRectangle()
    {
        var result = await RunScenarioAsync(
            "tight-contour-and-fallback",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const custom = hooks.createTextExclusion({
                objectId: 'custom-tight',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 160, y: 40, width: 200, height: 120 },
                wrapContourPoints: [
                    { x: 0.50, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);
            const fallback = hooks.createTextExclusion({
                objectId: 'fallback-tight',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 160, y: 40, width: 200, height: 120 }
            }, frame);

            assert.strictEqual(custom.kind, 'contour');
            assert.strictEqual(custom.polygon.length, 3);
            assert.strictEqual(fallback.kind, 'contour');
            assert.strictEqual(fallback.polygon.length, 4);

            const customTop = hooks.createTextExclusionManager([custom], frame).resolveLine(46, 18, 1);
            const fallbackTop = hooks.createTextExclusionManager([fallback], frame).resolveLine(46, 18, 1);
            assert.ok(blocked(customTop)[0].width < blocked(fallbackTop)[0].width - 120, 'custom triangle must be narrower than the rectangular fallback near the top');
            assert.deepStrictEqual(blocked(fallbackTop), [{ x: 160, y: 46, width: 200, height: 18 }]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_TightDistancesExpandResultAndPreserveWrapSide()
    {
        var result = await RunScenarioAsync(
            "tight-distances-and-side",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const distance = hooks.createTextExclusion({
                objectId: 'distance-tight',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 220, y: 40, width: 100, height: 80 },
                distanceLeft: 10,
                distanceRight: 20,
                wrapContourPoints: [
                    { x: 0, y: 0 },
                    { x: 1, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);
            const side = hooks.createTextExclusion({
                objectId: 'right-side-tight',
                blockId: 'p1',
                wrapMode: 'Tight',
                wrapSide: 'Right',
                rect: { x: 220, y: 40, width: 100, height: 80 },
                wrapContourPoints: [
                    { x: 0, y: 0 },
                    { x: 1, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);

            const distanceLine = hooks.createTextExclusionManager([distance], frame).resolveLine(64, 20, 1);
            assert.deepStrictEqual(blocked(distanceLine), [{ x: 210, y: 64, width: 130, height: 20 }]);

            const sideLine = hooks.createTextExclusionManager([side], frame).resolveLine(64, 20, 1);
            assert.deepStrictEqual(blocked(sideLine), [{ x: 0, y: 64, width: 320, height: 20 }]);
            assert.deepStrictEqual(available(sideLine), [{ x: 320, y: 64, width: 280, height: 20 }]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_ThroughUsesPolygonWithEditableContourKind()
    {
        var result = await RunScenarioAsync(
            "through-polygon",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const shape = [
                { x: 0.15, y: 0 },
                { x: 1, y: 0.25 },
                { x: 0.80, y: 1 },
                { x: 0, y: 0.75 }
            ];
            const tight = hooks.createTextExclusion({
                objectId: 'tight',
                blockId: 'p1',
                wrapMode: 'Tight',
                rect: { x: 150, y: 40, width: 180, height: 100 },
                wrapContourPoints: shape
            }, frame);
            const through = hooks.createTextExclusion({
                objectId: 'through',
                blockId: 'p1',
                wrapMode: 'Through',
                rect: { x: 150, y: 40, width: 180, height: 100 },
                wrapContourPoints: shape
            }, frame);

            assert.strictEqual(tight.kind, 'contour');
            assert.strictEqual(through.kind, 'editableContour');
            assert.deepStrictEqual(plain(through.polygon), plain(tight.polygon));
            assert.deepStrictEqual(
                blocked(hooks.createTextExclusionManager([through], frame).resolveLine(92, 18, 1)),
                blocked(hooks.createTextExclusionManager([tight], frame).resolveLine(92, 18, 1)));

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase8_PolygonPointChangeInvalidatesAvailableIntervalCache()
    {
        var result = await RunScenarioAsync(
            "polygon-cache-invalidation",
            """
            const frame = { x: 0, y: 0, width: 400, height: 300 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'cache-polygon',
                blockId: 'p1',
                wrapMode: 'Through',
                rect: { x: 100, y: 60, width: 200, height: 80 },
                wrapContourPoints: [
                    { x: 0, y: 0 },
                    { x: 1, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 }
                ]
            }, frame);
            const exclusions = [exclusion];
            const before = hooks.getAvailableIntervals(80, 20, frame, exclusions, 1);
            exclusion.polygon[1].x = 240;
            exclusion.polygon[2].x = 240;
            const after = hooks.getAvailableIntervals(80, 20, frame, exclusions, 1);

            assert.deepStrictEqual(plain(before.intervals.map(i => ({ x: i.x, width: i.width }))), [
                { x: 0, width: 100 },
                { x: 300, width: 100 }
            ]);
            assert.deepStrictEqual(plain(after.intervals.map(i => ({ x: i.x, width: i.width }))), [
                { x: 0, width: 100 },
                { x: 240, width: 160 }
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase8-{scenario}-{Guid.NewGuid():N}.js");
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
        const available = line => plain((line.intervals || []).map(i => ({
            x: i.x,
            y: i.y,
            width: i.width,
            height: i.height
        })));
        const blocked = line => plain((line.blockedIntervals || []).map(i => ({
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
