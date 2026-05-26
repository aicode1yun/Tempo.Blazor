using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase1TextExclusionJavaScriptTests
{
    [Fact]
    public async Task Phase1_NormalizesWrapModesAndWrapSides()
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

            const wrapModeCases = [
                [null, 'Inline', 0, 'inline'],
                ['Inline', 'Inline', 0, 'inline'],
                [1, 'Square', 1, 'square'],
                ['tight', 'Tight', 2, 'tight'],
                [3, 'Through', 3, 'through'],
                ['through', 'Through', 3, 'through'],
                ['TopAndBottom', 'TopBottom', 4, 'top-bottom'],
                ['break-text', 'TopBottom', 4, 'top-bottom'],
                ['behindText', 'BehindText', 5, 'behind-text'],
                ['in-front-of-text', 'InFrontOfText', 6, 'in-front-of-text']
            ];

            for (const [input, expectedName, expectedValue, expectedCss] of wrapModeCases) {
                assert.strictEqual(hooks.normalizeWrapModeName(input), expectedName, `wrap mode name for ${JSON.stringify(input)}`);
                const normalized = hooks.normalizeWrapMode(input);
                assert.strictEqual(normalized.value, expectedValue, `wrap mode value for ${JSON.stringify(input)}`);
                assert.strictEqual(normalized.css, expectedCss, `wrap mode css for ${JSON.stringify(input)}`);
            }

            const wrapSideCases = [
                [null, 'BothSides', 0, 'both-sides'],
                ['BothSides', 'BothSides', 0, 'both-sides'],
                ['both-sides', 'BothSides', 0, 'both-sides'],
                [1, 'Left', 1, 'left'],
                ['left', 'Left', 1, 'left'],
                [2, 'Right', 2, 'right'],
                ['right', 'Right', 2, 'right'],
                [3, 'Largest', 3, 'largest'],
                ['largest', 'Largest', 3, 'largest'],
                ['unknown-docx-value', 'BothSides', 0, 'both-sides']
            ];

            for (const [input, expectedName, expectedValue, expectedCss] of wrapSideCases) {
                assert.strictEqual(hooks.normalizeWrapSideName(input), expectedName, `wrap side name for ${JSON.stringify(input)}`);
                const normalized = hooks.normalizeWrapSide(input);
                assert.strictEqual(normalized.value, expectedValue, `wrap side value for ${JSON.stringify(input)}`);
                assert.strictEqual(normalized.css, expectedCss, `wrap side css for ${JSON.stringify(input)}`);
            }

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "normalization");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase1_TextExclusionCarriesOnlyOfficeLevelSourceOfTruthFields()
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
                objectId: 'object-1',
                blockId: 'anchor-1',
                pageIndex: 2,
                region: 'Header',
                headerFooterId: 'hf-1',
                tableId: 'table-1',
                cellId: 'cell-1',
                wrapMode: 'Square',
                wrapSide: 'Largest',
                rect: { x: 120, y: 30, width: 80, height: 40 },
                distanceLeft: 5,
                distanceRight: 7,
                distanceTop: 3,
                distanceBottom: 9,
                allowOverlap: true,
                zIndex: 42
            }, frame);

            assert.ok(exclusion, 'square object creates an exclusion');
            assert.strictEqual(exclusion.objectId, 'object-1');
            assert.strictEqual(exclusion.blockId, 'anchor-1');
            assert.strictEqual(exclusion.pageIndex, 2);
            assert.strictEqual(exclusion.region, 'Header');
            assert.strictEqual(exclusion.headerFooterId, 'hf-1');
            assert.strictEqual(exclusion.tableId, 'table-1');
            assert.strictEqual(exclusion.cellId, 'cell-1');
            assert.strictEqual(exclusion.scopeKey, '2|Header|hf-1|table-1|cell-1');
            assert.strictEqual(exclusion.wrapMode, 'Square');
            assert.strictEqual(exclusion.wrapSide, 'Largest');
            assert.strictEqual(exclusion.kind, 'rectangular');
            assert.deepStrictEqual(plain(exclusion.sourceRect), { x: 120, y: 30, width: 80, height: 40 });
            assert.deepStrictEqual(plain(exclusion.wrapRect), { x: 115, y: 27, width: 92, height: 52 });
            assert.deepStrictEqual(plain(exclusion.rect), { x: 115, y: 27, width: 92, height: 52 });
            assert.strictEqual(exclusion.distanceLeft, 5);
            assert.strictEqual(exclusion.distanceRight, 7);
            assert.strictEqual(exclusion.distanceTop, 3);
            assert.strictEqual(exclusion.distanceBottom, 9);
            assert.strictEqual(exclusion.allowOverlap, true);
            assert.strictEqual(exclusion.zIndex, 42);

            const normalized = hooks.normalizeImageObject({
                Content: {
                    ObjectId: 'object-2',
                    Layout: {
                        Wrap: { Mode: 1, Side: 3, DistanceLeft: 4 },
                        Transform: { Width: 90, Height: 50 }
                    }
                }
            });
            assert.strictEqual(normalized.wrapMode, 'Square');
            assert.strictEqual(normalized.wrapSide, 'Largest');
            const roundTripLayout = hooks.imageObjectToLayout(normalized);
            assert.strictEqual(roundTripLayout.Wrap.Mode, 1);
            assert.strictEqual(roundTripLayout.Wrap.Side, 3);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "text-exclusion-fields");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase1_WrapSideControlsAvailableTextIntervals()
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

            function snapshot(wrapSide, rect) {
                return hooks.snapshotWrapLayoutForTest({
                    frame: { x: 0, y: 0, width: 600, height: 400 },
                    lineY: 40,
                    lineHeight: 20,
                    minReadableWidth: 24,
                    object: {
                        objectId: 'object-' + wrapSide,
                        blockId: 'p1',
                        wrapMode: 'Square',
                        wrapSide,
                        rect: rect || { x: 250, y: 20, width: 100, height: 80 }
                    },
                    text: 'Text must flow on the allowed side of the image.'
                });
            }

            function intervalsOf(result) {
                return plain(result.availableIntervals.map(i => ({ x: i.x, width: i.width })));
            }

            assert.deepStrictEqual(
                intervalsOf(snapshot('BothSides')),
                [{ x: 0, width: 250 }, { x: 350, width: 250 }],
                'BothSides exposes text intervals on both sides of the object');

            assert.deepStrictEqual(
                intervalsOf(snapshot('Left')),
                [{ x: 0, width: 250 }],
                'Left means text is allowed only on the left side');

            assert.deepStrictEqual(
                intervalsOf(snapshot('Right')),
                [{ x: 350, width: 250 }],
                'Right means text is allowed only on the right side');

            assert.deepStrictEqual(
                intervalsOf(snapshot('Largest')),
                [{ x: 0, width: 250 }],
                'Largest resolves a tie to the left side, matching the C# geometry helper');

            assert.deepStrictEqual(
                intervalsOf(snapshot('Largest', { x: 100, y: 20, width: 100, height: 80 })),
                [{ x: 200, width: 400 }],
                'Largest chooses the right side when it has more room');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "wrap-side-intervals");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase1_TextExclusionUsesObjectRectInsteadOfHorizontalAlignment()
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

            const centerAlignedButDraggedRight = hooks.snapshotWrapLayoutForTest({
                frame: { x: 0, y: 0, width: 600, height: 400 },
                lineY: 40,
                lineHeight: 20,
                minReadableWidth: 24,
                object: {
                    objectId: 'object-center-align',
                    blockId: 'p1',
                    wrapMode: 'Square',
                    rect: { x: 300, y: 20, width: 80, height: 80 },
                    horizontalPosition: { align: 'Center' }
                },
                text: 'The exclusion must follow the dragged rectangle.'
            });
            assert.strictEqual(centerAlignedButDraggedRight.blockedIntervals[0].x, 300);
            assert.strictEqual(centerAlignedButDraggedRight.blockedIntervals[0].width, 80);
            assert.deepStrictEqual(
                plain(centerAlignedButDraggedRight.availableIntervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 300 }, { x: 380, width: 220 }]);

            const leftAlignedButDraggedToCenter = hooks.snapshotWrapLayoutForTest({
                frame: { x: 0, y: 0, width: 600, height: 400 },
                lineY: 40,
                lineHeight: 20,
                minReadableWidth: 24,
                object: {
                    objectId: 'object-left-align',
                    blockId: 'p1',
                    wrapMode: 'Square',
                    rect: { x: 250, y: 20, width: 100, height: 80 },
                    horizontalPosition: { align: 'Left' }
                },
                text: 'The exclusion must still use the actual rectangle.'
            });
            assert.strictEqual(leftAlignedButDraggedToCenter.blockedIntervals[0].x, 250);
            assert.deepStrictEqual(
                plain(leftAlignedButDraggedToCenter.availableIntervals.map(i => ({ x: i.x, width: i.width }))),
                [{ x: 0, width: 250 }, { x: 350, width: 250 }]);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "rect-over-align");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase1-{scenario}-{Guid.NewGuid():N}.js");
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
