using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase2IntervalManagerJavaScriptTests
{
    [Fact]
    public async Task Phase2_TextExclusionManagerReturnsBasicIntervalsAndTopBottomMove()
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
            const plain = value => JSON.parse(JSON.stringify(value));
            const frame = { x: 0, y: 0, width: 600, height: 400 };

            function exclusion(object) {
                return hooks.createTextExclusion(Object.assign({
                    objectId: object.objectId || 'object',
                    blockId: 'p1',
                    wrapMode: 'Square',
                    rect: { x: 250, y: 20, width: 100, height: 80 }
                }, object), frame);
            }

            function intervalsOf(line, source = 'intervals') {
                return plain(line[source].map(i => ({ x: i.x, y: i.y, width: i.width, height: i.height })));
            }

            const empty = hooks.createTextExclusionManager([], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervalsOf(empty), [{ x: 0, y: 40, width: 600, height: 20 }]);
            assert.strictEqual(empty.moved, false);

            const center = hooks.createTextExclusionManager([exclusion({ objectId: 'center' })], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervalsOf(center), [
                { x: 0, y: 40, width: 250, height: 20 },
                { x: 350, y: 40, width: 250, height: 20 }
            ]);
            assert.strictEqual(center.moved, false);

            const left = hooks.createTextExclusionManager([exclusion({
                objectId: 'left',
                rect: { x: 0, y: 20, width: 100, height: 80 }
            })], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervalsOf(left), [{ x: 100, y: 40, width: 500, height: 20 }]);

            const right = hooks.createTextExclusionManager([exclusion({
                objectId: 'right',
                rect: { x: 500, y: 20, width: 100, height: 80 }
            })], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervalsOf(right), [{ x: 0, y: 40, width: 500, height: 20 }]);

            const topBottom = hooks.createTextExclusionManager([exclusion({
                objectId: 'top-bottom',
                wrapMode: 'TopBottom',
                rect: { x: 250, y: 20, width: 100, height: 80 }
            })], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(intervalsOf(topBottom), [], 'TopBottom has no readable interval at the original Y');
            assert.strictEqual(topBottom.moved, true);
            assert.strictEqual(topBottom.movedToY, 100);
            assert.deepStrictEqual(intervalsOf(topBottom, 'movedIntervals'), [{ x: 0, y: 100, width: 600, height: 20 }]);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "basic");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase2_SquareUsesWrapRectDistancesAndVerticalHorizontalIntersection()
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
            const plain = value => JSON.parse(JSON.stringify(value));
            const frame = { x: 0, y: 0, width: 600, height: 400 };
            const exclusion = hooks.createTextExclusion({
                objectId: 'distance-square',
                blockId: 'p1',
                wrapMode: 'Square',
                rect: { x: 250, y: 20, width: 100, height: 80 },
                distanceLeft: 10,
                distanceRight: 20,
                distanceTop: 5,
                distanceBottom: 15
            }, frame);

            assert.deepStrictEqual(plain(exclusion.wrapRect), { x: 240, y: 15, width: 130, height: 100 });

            const manager = hooks.createTextExclusionManager([exclusion], frame);
            const overlapping = manager.resolveLine(18, 20, 24);
            assert.deepStrictEqual(
                plain(overlapping.blockedIntervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 240, width: 130 }]);
            assert.deepStrictEqual(
                plain(overlapping.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 240 }, { x: 370, width: 230 }]);

            const above = manager.resolveLine(10, 4, 24);
            assert.deepStrictEqual(
                plain(above.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 600 }],
                'distanceTop starts at y=15, so a line ending at 14 does not collide');

            const below = manager.resolveLine(115, 20, 24);
            assert.deepStrictEqual(
                plain(below.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 600 }],
                'distanceBottom ends at y=115 and does not block the next line');

            const outside = hooks.createTextExclusion({
                objectId: 'outside',
                blockId: 'p1',
                wrapMode: 'Square',
                rect: { x: 700, y: 20, width: 100, height: 80 }
            }, frame);
            assert.strictEqual(outside, null, 'an object fully outside the body frame does not create a body exclusion');
            const outsideManager = hooks.createTextExclusionManager([outside], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(
                plain(outsideManager.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 600 }]);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "square-distances");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase2_WrapSideBlockedIntervalsAreIndependentOfCss()
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
            const plain = value => JSON.parse(JSON.stringify(value));
            const frame = { x: 0, y: 0, width: 600, height: 400 };

            function blockedFor(wrapSide, rect) {
                const exclusion = hooks.createTextExclusion({
                    objectId: 'side-' + wrapSide,
                    blockId: 'p1',
                    wrapMode: 'Square',
                    wrapSide,
                    rect: rect || { x: 250, y: 20, width: 100, height: 80 },
                    horizontalPosition: { align: 'Center' }
                }, frame);
                const line = hooks.createTextExclusionManager([exclusion], frame).resolveLine(40, 20, 24);
                return plain(line.blockedIntervals.map(i => ({ x: i.x, width: i.width })));
            }

            assert.deepStrictEqual(blockedFor('BothSides'), [{ x: 250, width: 100 }]);
            assert.deepStrictEqual(blockedFor('Left'), [{ x: 250, width: 350 }]);
            assert.deepStrictEqual(blockedFor('Right'), [{ x: 0, width: 350 }]);
            assert.deepStrictEqual(blockedFor('Largest'), [{ x: 250, width: 350 }], 'tie resolves left deterministically');
            assert.deepStrictEqual(blockedFor('Largest', { x: 100, y: 20, width: 100, height: 80 }), [{ x: 0, width: 200 }]);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "wrap-side-blocked");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase2_MergesOverlappingAndTinyGapBlockedIntervals()
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
            const plain = value => JSON.parse(JSON.stringify(value));
            const frame = { x: 0, y: 0, width: 600, height: 400 };

            function exclusion(id, x, width) {
                return hooks.createTextExclusion({
                    objectId: id,
                    blockId: 'p1',
                    wrapMode: 'Square',
                    rect: { x, y: 20, width, height: 80 }
                }, frame);
            }

            const overlap = hooks.createTextExclusionManager([
                exclusion('a', 100, 100),
                exclusion('b', 180, 100)
            ], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(
                plain(overlap.blockedIntervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 100, width: 180 }]);
            assert.deepStrictEqual(
                plain(overlap.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 100 }, { x: 280, width: 320 }]);

            const tinyGap = hooks.createTextExclusionManager([
                exclusion('c', 100, 100),
                exclusion('d', 210, 100)
            ], frame).resolveLine(40, 20, 24);
            assert.deepStrictEqual(
                plain(tinyGap.blockedIntervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 100, width: 210 }],
                'the 10px gap between objects is below minReadableWidth=24 and is merged away');
            assert.deepStrictEqual(
                plain(tinyGap.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 100 }, { x: 310, width: 290 }]);
            assert.strictEqual(tinyGap.moved, false, 'removing a tiny gap must not turn Square wrapping into TopBottom when readable side intervals remain');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "merge");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase2_MovesOnlyWhenNoReadableIntervalExists()
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
            const plain = value => JSON.parse(JSON.stringify(value));
            const frame = { x: 0, y: 0, width: 600, height: 400 };

            function lineFor(rect, minReadableWidth = 24) {
                const exclusion = hooks.createTextExclusion({
                    objectId: 'object',
                    blockId: 'p1',
                    wrapMode: 'Square',
                    rect
                }, frame);
                return hooks.createTextExclusionManager([exclusion], frame).resolveLine(40, 20, minReadableWidth);
            }

            const leftReadable = lineFor({ x: 250, y: 20, width: 330, height: 80 }, 24);
            assert.deepStrictEqual(
                plain(leftReadable.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 250 }]);
            assert.strictEqual(leftReadable.moved, false);

            const rightReadable = lineFor({ x: 20, y: 20, width: 330, height: 80 }, 24);
            assert.deepStrictEqual(
                plain(rightReadable.intervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 350, width: 250 }]);
            assert.strictEqual(rightReadable.moved, false);

            const noReadableSide = lineFor({ x: 20, y: 20, width: 560, height: 80 }, 50);
            assert.deepStrictEqual(plain(noReadableSide.intervals), []);
            assert.strictEqual(noReadableSide.moved, true);
            assert.strictEqual(noReadableSide.movedToY, 100);
            assert.deepStrictEqual(
                plain(noReadableSide.movedIntervals.map(i => ({ x: i.x, y: i.y, width: i.width }))),
                [{ x: 0, y: 100, width: 600 }]);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "moved");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase2-{scenario}-{Guid.NewGuid():N}.js");
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
