using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase4InputPipelineJavaScriptTests
{
    [Fact]
    public async Task Phase4_InsertTextSentence_PreservesOrderSpacesAndSingleTypingOperation()
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
                DocumentId: 'phase4-insertText',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            const result = pipeline.insertText('jak se mas');
            const text = model.body.blocks[0].content.runs.map(run => run.text || '').join('');

            assert.strictEqual(result.ok, true);
            assert.strictEqual(text, 'jak se mas');
            assert.strictEqual(result.transactionType, 'typing');
            assert.strictEqual(result.operations.length, 1);
            assert.strictEqual(result.operations[0].type, 'InsertText');
            assert.strictEqual(result.operations[0].text, 'jak se mas');
            assert.strictEqual(result.selection.offset, 'jak se mas'.length);
            assert.strictEqual(pipeline.debug().boundaryPatchCount, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase4_RapidBeforeInputEvents_PreserveCharacterOrderAndSpaces()
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
                DocumentId: 'phase4-beforeinput',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            for (const ch of 'jak se mas') {
                pipeline.handleBeforeInput({ inputType: 'insertText', data: ch, preventDefault() { this.prevented = true; } });
            }

            const text = model.body.blocks[0].content.runs.map(run => run.text || '').join('');
            assert.strictEqual(text, 'jak se mas');
            assert.strictEqual(pipeline.debug().lastVisibleText, 'jak se mas');
            assert.strictEqual(pipeline.debug().boundaryPatchCount, 'jak se mas'.length);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase4_SpaceEnterAndShiftEnter_UpdateSelectionImmediately()
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
                DocumentId: 'phase4-enter',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'AB' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                selection: { blockId: 'p1', offset: 1, isCollapsed: true }
            });

            const space = pipeline.handleBeforeInput({ inputType: 'insertText', data: ' ', preventDefault() {} });
            assert.strictEqual(model.body.blocks[0].content.runs.map(run => run.text || '').join(''), 'A B');
            assert.strictEqual(space.selection.blockId, 'p1');
            assert.strictEqual(space.selection.offset, 2);
            assert.strictEqual(pipeline.debug().lastVisibleText, 'A B');

            const lineBreak = pipeline.handleBeforeInput({ inputType: 'insertLineBreak', data: null, preventDefault() {} });
            assert.strictEqual(lineBreak.operations.length, 1);
            assert.strictEqual(lineBreak.operations[0].type, 'InsertText');
            assert.strictEqual(lineBreak.operations[0].text, '\n');
            assert.strictEqual(lineBreak.selection.blockId, 'p1');
            assert.strictEqual(lineBreak.selection.offset, 3);
            assert.strictEqual(model.body.blocks[0].content.runs.map(run => run.text || '').join(''), 'A \nB');

            const enter = pipeline.handleBeforeInput({ inputType: 'insertParagraph', data: null, preventDefault() {} });
            assert.strictEqual(enter.operations[0].type, 'SplitParagraph');
            assert.notStrictEqual(enter.selection.blockId, 'p1');
            assert.strictEqual(enter.selection.offset, 0);
            assert.strictEqual(model.body.blocks[0].content.runs.map(run => run.text || '').join(''), 'A \n');
            assert.strictEqual(model.body.blocks[1].content.runs.map(run => run.text || '').join(''), 'B');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase4_CompositionPreviewAndCommit_StayInOneCompositionSession()
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
                DocumentId: 'phase4-composition',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Ahoj ' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                selection: { blockId: 'p1', offset: 5, isCollapsed: true }
            });

            const start = pipeline.handleCompositionStart({ selection: { blockId: 'p1', offset: 5, isCollapsed: true } });
            const update = pipeline.handleCompositionUpdate({ data: 'ž' });
            assert.strictEqual(start.transactionType, 'composition');
            assert.strictEqual(update.transactionType, 'composition');
            assert.strictEqual(update.boundaryPatchQueued, false);
            assert.strictEqual(update.previewText, 'Ahoj ž');
            assert.strictEqual(model.body.blocks[0].content.runs.map(run => run.text || '').join(''), 'Ahoj ');
            assert.strictEqual(pipeline.debug().boundaryPatchCount, 0);

            const end = pipeline.handleCompositionEnd({ data: 'ž' });
            assert.strictEqual(end.transactionType, 'composition');
            assert.strictEqual(end.operations.length, 1);
            assert.strictEqual(end.operations[0].type, 'InsertText');
            assert.strictEqual(model.body.blocks[0].content.runs.map(run => run.text || '').join(''), 'Ahoj ž');
            assert.strictEqual(pipeline.debug().boundaryPatchCount, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase4-input-{Guid.NewGuid():N}.js");
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
