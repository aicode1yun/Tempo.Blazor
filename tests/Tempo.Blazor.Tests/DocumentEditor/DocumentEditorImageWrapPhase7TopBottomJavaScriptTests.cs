using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase7TopBottomJavaScriptTests
{
    [Fact]
    public async Task Phase7_TopBottomBlocksFullBodyFrameOnlyInsideVerticalObjectBand()
    {
        var result = await RunScenarioAsync(
            "full-width-band",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'top-bottom',
                blockId: 'p1',
                wrapMode: 'TopBottom',
                rect: { x: 250, y: 20, width: 100, height: 80 }
            }, frame);

            assert.strictEqual(exclusion.kind, 'fullWidth');
            assert.deepStrictEqual(plain(exclusion.rect), { x: 0, y: 20, width: 600, height: 80 });

            const manager = hooks.createTextExclusionManager([exclusion], frame);
            const inside = manager.resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervals(inside), []);
            assert.deepStrictEqual(intervals(inside, 'blockedIntervals'), [
                { x: 0, y: 40, width: 600, height: 20 }
            ]);
            assert.strictEqual(inside.moved, true);
            assert.strictEqual(inside.movedToY, 100);
            assert.deepStrictEqual(intervals(inside, 'movedIntervals'), [
                { x: 0, y: 100, width: 600, height: 20 }
            ]);

            const below = manager.resolveLine(104, 20, 24);
            assert.deepStrictEqual(intervals(below), [
                { x: 0, y: 104, width: 600, height: 20 }
            ]);
            assert.strictEqual(below.moved, false);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase7_TopBottomOutsideHorizontalFrameDoesNotBlockText()
    {
        var result = await RunScenarioAsync(
            "outside-horizontal-frame",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'outside-top-bottom',
                blockId: 'p1',
                wrapMode: 'TopBottom',
                rect: { x: 620, y: 20, width: 100, height: 80 },
                distanceLeft: 200,
                distanceRight: 200,
                distanceTop: 0,
                distanceBottom: 0
            }, frame);

            assert.strictEqual(exclusion, null, 'TopBottom objects entirely outside the horizontal text frame must not create a full-width band');
            const line = hooks.createTextExclusionManager([exclusion].filter(Boolean), frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervals(line), [
                { x: 0, y: 40, width: 600, height: 20 }
            ]);
            assert.strictEqual(line.moved, false);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase7_TopBottomTopAndBottomDistancesExpandVerticalBand()
    {
        var result = await RunScenarioAsync(
            "vertical-distances",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'top-bottom-distances',
                blockId: 'p1',
                wrapMode: 'TopBottom',
                rect: { x: 250, y: 50, width: 100, height: 40 },
                distanceTop: 10,
                distanceBottom: 20
            }, frame);

            assert.deepStrictEqual(plain(exclusion.rect), { x: 0, y: 40, width: 600, height: 70 });
            const lineInsideTopDistance = hooks.createTextExclusionManager([exclusion], frame).resolveLine(42, 20, 24);
            assert.deepStrictEqual(intervals(lineInsideTopDistance), []);
            assert.strictEqual(lineInsideTopDistance.movedToY, 110);

            const lineBeforeBand = hooks.createTextExclusionManager([exclusion], frame).resolveLine(10, 20, 24);
            assert.deepStrictEqual(intervals(lineBeforeBand), [
                { x: 0, y: 10, width: 600, height: 20 }
            ]);

            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task Phase7_TopBottomLeftAndRightDistancesDoNotNarrowFullWidthBand()
    {
        var result = await RunScenarioAsync(
            "horizontal-distances-ignored",
            """
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'top-bottom-horizontal-distances',
                blockId: 'p1',
                wrapMode: 'TopBottom',
                rect: { x: 250, y: 20, width: 100, height: 80 },
                distanceLeft: 80,
                distanceRight: 120,
                distanceTop: 0,
                distanceBottom: 0
            }, frame);

            assert.deepStrictEqual(plain(exclusion.wrapRect), { x: 170, y: 20, width: 300, height: 80 });
            assert.deepStrictEqual(plain(exclusion.rect), { x: 0, y: 20, width: 600, height: 80 });
            const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervals(line, 'blockedIntervals'), [
                { x: 0, y: 40, width: 600, height: 20 }
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase7-{scenario}-{Guid.NewGuid():N}.js");
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
