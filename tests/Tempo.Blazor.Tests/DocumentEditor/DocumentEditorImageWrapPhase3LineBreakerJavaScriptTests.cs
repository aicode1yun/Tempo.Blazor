using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorImageWrapPhase3LineBreakerJavaScriptTests
{
    [Fact]
    public async Task Phase3_LineBreakerUsesMultipleRangesAsRealTextCapacity()
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

            function layoutFor(text, options = {}) {
                const breaker = hooks.createLineBreaker(hooks.createTextMeasurementService());
                const intervals = options.intervals || [
                    { x: 0, y: 20, width: 50, height: 20 },
                    { x: 100, y: 20, width: 180, height: 20 }
                ];
                return breaker.breakParagraph({
                    id: 'p1',
                    alignment: options.alignment || 'left',
                    runs: [{ id: 'r1', kind: 'text', text }]
                }, {
                    x: 0,
                    y: 20,
                    width: 280,
                    lineGap: 0,
                    minReadableWidth: 8,
                    alignment: options.alignment || 'left',
                    availableIntervals: intervals
                });
            }

            const layout = layoutFor('alpha beta gamma');
            assert.strictEqual(layout.ok, true);
            assert.strictEqual(layout.fallback, false);
            assert.strictEqual(layout.lines.length, 1);

            const line = layout.lines[0];
            assert.strictEqual(line.ranges.length, 2);
            assert.deepStrictEqual(plain(line.ranges.map(r => ({ x: r.x, width: r.width }))), [
                { x: 0, width: 50 },
                { x: 100, width: 180 }
            ]);
            assert.strictEqual(line.availableIntervals.length, 2);
            assert.ok(line.ranges[0].segments.length > 0, 'left range owns text, not just caret metadata');
            assert.ok(line.ranges[1].segments.length > 0, 'right range owns text, not just caret metadata');
            assert.ok(line.ranges[1].segments.every(s => s.rect.x >= 100), 'right range segments start after the blocked image rect');
            assert.strictEqual(line.rect.x, 0);
            assert.strictEqual(line.rect.width, 280);

            for (const range of line.ranges) {
                assert.strictEqual(typeof range.x, 'number');
                assert.strictEqual(typeof range.width, 'number');
                assert.strictEqual(typeof range.start, 'number');
                assert.strictEqual(typeof range.end, 'number');
                assert.ok(Array.isArray(range.segments));
            }

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "multi-range-capacity");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_TextContinuesAcrossRangesWithoutWordGluing()
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
            const breaker = hooks.createLineBreaker(hooks.createTextMeasurementService());
            const text = 'alpha beta gamma';
            const layout = breaker.breakParagraph({
                id: 'p1',
                runs: [{ id: 'r1', kind: 'text', text }]
            }, {
                x: 0,
                y: 20,
                width: 280,
                minReadableWidth: 8,
                availableIntervals: [
                    { x: 0, y: 20, width: 50, height: 20 },
                    { x: 100, y: 20, width: 180, height: 20 }
                ]
            });

            const line = layout.lines[0];
            const leftText = line.ranges[0].segments.map(s => s.text).join('');
            const rightText = line.ranges[1].segments.map(s => s.text).join('');
            assert.strictEqual(leftText, 'alpha ');
            assert.strictEqual(rightText, 'beta gamma');
            assert.strictEqual(leftText + rightText, text);
            assert.ok(line.ranges[1].segments[0].rect.x >= 100);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "word-wrapping");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_LongWordsCjkAndNbspRemainRangeAware()
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

            function breakText(text, intervals) {
                return hooks.createLineBreaker(hooks.createTextMeasurementService()).breakParagraph({
                    id: 'p1',
                    runs: [{ id: 'r1', kind: 'text', text }]
                }, {
                    x: 0,
                    y: 20,
                    width: 240,
                    minReadableWidth: 8,
                    availableIntervals: intervals
                });
            }

            const narrowPair = [
                { x: 0, y: 20, width: 40, height: 20 },
                { x: 100, y: 20, width: 40, height: 20 }
            ];

            const longWord = breakText('supercalifragilistic', narrowPair);
            assert.ok(longWord.segments.some(s => s.splitFromLongToken), 'long words keep the existing split logic');
            assert.ok(longWord.lines[0].ranges[0].segments.length > 0);
            assert.ok(longWord.lines[0].ranges[1].segments.length > 0, 'split pieces continue into the right range before moving down');

            const cjk = breakText('漢字仮名', [
                { x: 0, y: 20, width: 36, height: 20 },
                { x: 100, y: 20, width: 36, height: 20 }
            ]);
            assert.strictEqual(cjk.lines[0].ranges[0].segments.map(s => s.text).join(''), '漢字');
            assert.strictEqual(cjk.lines[0].ranges[1].segments.map(s => s.text).join(''), '仮名');

            const nbspText = 'alpha\u00a0beta gamma';
            const nbsp = breakText(nbspText, [
                { x: 0, y: 20, width: 50, height: 20 },
                { x: 100, y: 20, width: 150, height: 20 }
            ]);
            const nbspSegment = nbsp.lines[0].ranges[1].segments.find(s => s.text === 'alpha\u00a0beta');
            assert.ok(nbspSegment, 'NBSP sequence moves as one token into the range where it fits');
            assert.strictEqual(nbspSegment.rangeIndex, 1);
            assert.strictEqual(nbsp.lines[0].ranges[0].segments.length, 0);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "special-tokens");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_CaretStopsAndPointerHitTestingUseRanges()
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
            const selection = sandbox.window.tmDocumentEditorEngine.selection;
            const text = 'alpha beta gamma';
            const layout = hooks.createLineBreaker(hooks.createTextMeasurementService()).breakParagraph({
                id: 'p1',
                runs: [{ id: 'r1', kind: 'text', text }]
            }, {
                x: 0,
                y: 20,
                width: 280,
                minReadableWidth: 8,
                availableIntervals: [
                    { x: 0, y: 20, width: 50, height: 20 },
                    { x: 100, y: 20, width: 180, height: 20 }
                ]
            });
            const line = layout.lines[0];
            const leftStart = layout.caretStops.find(s => s.lineId === line.id && s.offset === 0 && s.rangeIndex === 0);
            const rightStartOffset = line.ranges[1].start;
            const rightStart = layout.caretStops.find(s => s.lineId === line.id && s.offset === rightStartOffset && s.rangeIndex === 1);
            assert.ok(leftStart && leftStart.rect.x >= 0 && leftStart.rect.x <= 1);
            assert.ok(rightStart && rightStart.rect.x >= 100);

            const model = {
                body: {
                    blocks: [{
                        id: 'p1',
                        type: 'paragraph',
                        content: { runs: [{ id: 'r1', kind: 'text', text }] }
                    }]
                }
            };
            const hitLayout = {
                blocks: [{
                    blockId: 'p1',
                    type: 'paragraph',
                    rect: { x: 0, y: 20, width: 280, height: line.rect.height },
                    lines: [Object.assign({}, line, {
                        blockId: 'p1',
                        lineId: line.id,
                        availableIntervals: line.availableIntervals.map(i => Object.assign({}, i, { blockId: 'p1', lineId: line.id }))
                    })]
                }],
                objects: []
            };

            const leftHit = selection.pointerHitTest(model, hitLayout, 48, 25);
            assert.strictEqual(leftHit.type, 'text');
            assert.ok(leftHit.position.offset >= line.ranges[0].start);
            assert.ok(leftHit.position.offset <= line.ranges[0].end);

            const rightHit = selection.pointerHitTest(model, hitLayout, 260, 25);
            assert.strictEqual(rightHit.type, 'text');
            assert.ok(rightHit.position.offset >= line.ranges[1].start);

            const gapHit = selection.pointerHitTest(model, hitLayout, 75, 25);
            assert.strictEqual(gapHit.type, 'none', 'the caret is not placed inside the blocked image interval');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "caret-hit-testing");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_AlignmentAndJustifyStayInsideEachRange()
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

            function layoutFor(text, alignment, intervals) {
                return hooks.createLineBreaker(hooks.createTextMeasurementService()).breakParagraph({
                    id: 'p1',
                    alignment,
                    runs: [{ id: 'r1', kind: 'text', text }]
                }, {
                    x: 0,
                    y: 20,
                    width: 280,
                    minReadableWidth: 8,
                    alignment,
                    availableIntervals: intervals || [
                        { x: 0, y: 20, width: 60, height: 20 },
                        { x: 100, y: 20, width: 180, height: 20 }
                    ]
                });
            }

            for (const alignment of ['left', 'right', 'center']) {
                const line = layoutFor('alpha beta gamma', alignment).lines[0];
                for (const range of line.ranges) {
                    for (const segment of range.segments) {
                        assert.ok(segment.rect.x >= range.x - 0.001, `${alignment} segment starts inside its range`);
                        assert.ok(segment.rect.x + segment.rect.width <= range.x + range.width + 0.001, `${alignment} segment ends inside its range`);
                    }
                }
            }

            const justified = layoutFor('alpha beta gamma delta epsilon zeta eta theta', 'justify', [
                { x: 0, y: 20, width: 95, height: 20 },
                { x: 140, y: 20, width: 95, height: 20 }
            ]);
            assert.ok(justified.lines.length > 1, 'justify metadata is meaningful on non-final lines');
            const firstLine = justified.lines[0];
            assert.strictEqual(firstLine.justify.ranges.length, 2);
            assert.strictEqual(firstLine.justify.extraSpacePerGap, 0, 'global justify does not stretch across the blocked interval');
            for (const rangeJustify of firstLine.justify.ranges) {
                assert.ok(rangeJustify.remainingWidth <= 95, 'remaining width is computed per range');
            }

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "alignment");
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-image-wrap-phase3-{scenario}-{Guid.NewGuid():N}.js");
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
