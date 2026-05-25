using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase3TextRunJavaScriptTests
{
    [Fact]
    public async Task Phase3_RunNormalizer_MergesCompatibleRunsAndPreservesBoundaries()
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
            const runs = hooks.mergeAdjacentTextRuns([
                { id: 'a', kind: 'text', text: 'Hel', marks: [{ type: 1 }, { type: 0 }] },
                { id: 'b', kind: 'text', text: 'lo', marks: [{ type: 0 }, { type: 1 }] },
                { id: 'c', kind: 'text', text: ' ', marks: [{ type: 0 }], commentIds: ['comment-1'] },
                { id: 'd', kind: 'text', text: 'world', marks: [{ type: 0 }], revisionId: 'revision-1' }
            ]);

            assertJsonEqual(runs.map(run => run.text), ['Hello', ' ', 'world']);
            assertJsonEqual(runs[0].marks.map(mark => mark.type), [0, 1]);
            assertJsonEqual(runs[1].commentIds, ['comment-1']);
            assert.strictEqual(runs[2].revisionId, 'revision-1');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_SplitRunsForRange_PreservesDiacriticsEmojiAndNonBreakingSpace()
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
            const text = 'Příliš 😀\u00a0text';
            const start = text.indexOf('😀');
            const end = start + '😀\u00a0'.length;
            const runs = hooks.splitRunsForRange({
                id: 'p1',
                type: 'paragraph',
                content: { runs: [{ id: 'r1', kind: 'text', text, marks: [] }] }
            }, start, end, { type: 0 }, false);

            assert.strictEqual(runs.map(run => run.text).join(''), text);
            assertJsonEqual(runs.map(run => run.text), ['Příliš ', '😀\u00a0', 'text']);
            assert.strictEqual(runs[1].marks.length, 1);
            assert.strictEqual(runs[1].marks[0].type, 0);
            assert.ok(!runs.some(run => run.text.includes('\ufffd')));

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_CommandDispatcher_ProducesMinimalRunsForOverlappingFormatting()
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
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase3-command',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'abcdefghij' }] } }
                ]
            });
            const dispatcher = hooks.createCommandDispatcher(model, {
                selection: selection('p1', 'r1', 2, 7)
            });

            let result = dispatcher.executeCommand('bold');
            assert.strictEqual(result.ok, true);
            let runs = model.body.blocks[0].content.runs;
            assertJsonEqual(runs.map(run => run.text), ['ab', 'cdefg', 'hij']);
            assertJsonEqual(runs.map(run => run.marks.map(mark => mark.type)), [[], [0], []]);

            dispatcher.refresh(selection('p1', 'r1', 4, 9));
            result = dispatcher.executeCommand('italic');
            assert.strictEqual(result.ok, true);
            runs = model.body.blocks[0].content.runs;
            assertJsonEqual(runs.map(run => run.text), ['ab', 'cd', 'efg', 'hi', 'j']);
            assertJsonEqual(runs.map(run => run.marks.map(mark => mark.type)), [[], [0], [0, 1], [1], []]);

            dispatcher.refresh(selection('p1', 'r1', 2, 9));
            result = dispatcher.executeCommand('bold');
            assert.strictEqual(result.ok, true);
            const state = dispatcher.refresh(selection('p1', 'r1', 2, 9));
            assert.strictEqual(state.commandValues.bold, true);
            assert.strictEqual(state.inline.mixed.bold, false);
            assert.ok(model.body.blocks[0].content.runs.length <= 4);

            console.log('OK');

            function selection(blockId, inlineId, start, end) {
                return {
                    region: 'Body',
                    blockId,
                    anchor: { region: 'Body', blockId, inlineId, offset: start },
                    focus: { region: 'Body', blockId, inlineId, offset: end },
                    isCollapsed: start === end,
                    direction: 'forward'
                };
            }
            """;

        var nodeResult = await RunNodeAsync(scriptPath, nodeScript);
        nodeResult.ExitCode.Should().Be(0, nodeResult.StandardError);
        nodeResult.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase3_CommandDispatcher_ReplacesValueMarksAndClearFormattingMergesBack()
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
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase3-clear',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } }
                ]
            });
            const dispatcher = hooks.createCommandDispatcher(model, {
                selection: selection('p1', 'r1', 6, 11)
            });

            dispatcher.executeCommand('fontSize', { size: '28pt' });
            dispatcher.executeCommand('textColor', { color: '#2563eb' });

            let runs = model.body.blocks[0].content.runs;
            assertJsonEqual(runs.map(run => run.text), ['Hello ', 'world']);
            assertJsonEqual(runs[1].marks.map(mark => [mark.type, mark.value]), [[10, '#2563eb'], [12, '28pt']]);

            dispatcher.executeCommand('textColor', { color: '#dc2626' });
            runs = model.body.blocks[0].content.runs;
            assertJsonEqual(runs[1].marks.map(mark => [mark.type, mark.value]), [[10, '#dc2626'], [12, '28pt']]);

            dispatcher.executeCommand('clearFormatting');
            runs = model.body.blocks[0].content.runs;
            assertJsonEqual(runs.map(run => run.text), ['Hello world']);
            assertJsonEqual(runs[0].marks, []);

            console.log('OK');

            function selection(blockId, inlineId, start, end) {
                return {
                    region: 'Body',
                    blockId,
                    anchor: { region: 'Body', blockId, inlineId, offset: start },
                    focus: { region: 'Body', blockId, inlineId, offset: end },
                    isCollapsed: start === end,
                    direction: 'forward'
                };
            }
            """;

        var nodeResult = await RunNodeAsync(scriptPath, nodeScript);
        nodeResult.ExitCode.Should().Be(0, nodeResult.StandardError);
        nodeResult.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase6_CommandDispatcher_ClearHighlightRemovesRangeMarkAndCollapsedPendingMark()
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
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase6-highlight-clear',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } }
                ]
            });
            const dispatcher = hooks.createCommandDispatcher(model, {
                selection: selection('p1', 'r1', 6, 11)
            });

            let result = dispatcher.executeCommand('backgroundColor', { value: '#fde68a' });
            assert.strictEqual(result.ok, true);
            let state = dispatcher.refresh(selection('p1', 'r1', 6, 11));
            assert.strictEqual(state.commandValues.backgroundColor, '#fde68a');

            result = dispatcher.executeCommand('backgroundColor', { value: '' });
            assert.strictEqual(result.ok, true);
            state = dispatcher.refresh(selection('p1', 'r1', 6, 11));
            assert.strictEqual(state.commandValues.backgroundColor, null);
            let runs = model.body.blocks[0].content.runs;
            assert.strictEqual(runs.some(run => run.marks.some(mark => mark.type === 9 || mark.type === 'highlight')), false);

            dispatcher.refresh(selection('p1', 'r1', 5, 5));
            result = dispatcher.executeCommand('backgroundColor', { value: '#fde68a' });
            assert.strictEqual(result.ok, true);
            assert.strictEqual(dispatcher.getPendingTypingMarks().some(mark => mark.type === 9), true);
            result = dispatcher.executeCommand('backgroundColor', { value: '' });
            assert.strictEqual(result.ok, true);
            assert.strictEqual(dispatcher.getPendingTypingMarks().some(mark => mark.type === 9), false);

            console.log('OK');

            function selection(blockId, inlineId, start, end) {
                return {
                    region: 'Body',
                    blockId,
                    anchor: { region: 'Body', blockId, inlineId, offset: start },
                    focus: { region: 'Body', blockId, inlineId, offset: end },
                    isCollapsed: start === end,
                    direction: 'forward'
                };
            }
            """;

        var nodeResult = await RunNodeAsync(scriptPath, nodeScript);
        nodeResult.ExitCode.Should().Be(0, nodeResult.StandardError);
        nodeResult.StandardOutput.Trim().Should().Be("OK");
    }

    private static string GetWysiwygScriptPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
    }

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
            process?.WaitForExit(2000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase3-runtime-{Guid.NewGuid():N}.js");
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
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            return sandbox;
        }

        function assertJsonEqual(actual, expected) {
            assert.strictEqual(JSON.stringify(actual), JSON.stringify(expected));
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
