using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase11UndoJavaScriptTests
{
    [Fact]
    public async Task Phase11_FormattingUndoRedo_RestoresDocumentModelAndSelection()
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
                DocumentId: 'phase11-formatting',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } }]
            });
            const history = hooks.createHistoryController(model, {
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            const commit = history.commitOperation(hooks.createOperation('ApplyMark', {
                range: { blockId: 'p1', start: 0, end: 5 },
                mark: { type: 'Bold' }
            }, { source: 'toolbar', timestamp: 1000 }), {
                label: 'Bold',
                beforeSelection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });
            const afterCommit = history.debug();
            const undo = history.undo();
            const redo = history.redo();

            assert.strictEqual(commit.ok, true);
            assert.strictEqual(afterCommit.undoDepth, 1);
            assert.strictEqual(hasBold(model), true, 'redo should leave the target range bold');
            assert.strictEqual(undo.ok, true);
            assert.strictEqual(undo.selection.blockId, 'p1');
            assert.strictEqual(undo.selection.offset, 0);
            assert.strictEqual(redo.ok, true);
            assert.strictEqual(redo.selection.blockId, 'p1');
            assert.strictEqual(redo.selection.offset, 5);
            assert.strictEqual(history.debug().undoDepth, 1);
            assert.strictEqual(history.debug().redoDepth, 0);

            history.undo();
            assert.strictEqual(hasBold(model), false, 'undo must remove bold from the model');
            history.redo();
            assert.strictEqual(hasBold(model), true, 'redo must restore bold in the model');

            console.log('OK');

            function hasBold(doc) {
                return doc.body.blocks[0].content.runs.some(run =>
                    (run.text || '').includes('Hello')
                    && (run.marks || []).some(mark => String(mark.type || mark.Type).toLowerCase() === 'bold'));
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "formatting");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_TypingSession_CoalescesIntoOneUndoItemAndRestoresSelection()
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
                DocumentId: 'phase11-typing',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            const history = hooks.createHistoryController(model, {
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            history.commitOperation(hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 0 },
                text: 'jak '
            }, { source: 'typing', timestamp: 1000 }), {
                transactionType: 'typing',
                label: 'Typing',
                beforeSelection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });
            history.commitOperation(hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 4 },
                text: 'se mas'
            }, { source: 'typing', timestamp: 1100 }), {
                transactionType: 'typing',
                label: 'Typing',
                beforeSelection: { blockId: 'p1', offset: 4, isCollapsed: true }
            });

            const debugAfterTyping = history.debug();
            const undo = history.undo();
            const textAfterUndo = text(model);
            const redo = history.redo();

            assert.strictEqual(debugAfterTyping.undoDepth, 1);
            assert.strictEqual(debugAfterTyping.nextUndo.coalesced, true);
            assert.strictEqual(textAfterUndo, '');
            assert.strictEqual(undo.selection.blockId, 'p1');
            assert.strictEqual(undo.selection.offset, 0);
            assert.strictEqual(text(model), 'jak se mas');
            assert.strictEqual(redo.selection.blockId, 'p1');
            assert.strictEqual(redo.selection.offset, 'jak se mas'.length);

            console.log('OK');

            function text(doc) {
                return doc.body.blocks[0].content.runs.map(run => run.text || '').join('');
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "typing");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_TrackedInsertAndRevisionDecision_AreUndoableTransactions()
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

            const trackedModel = hooks.importFromCSharpJson({
                DocumentId: 'phase11-tracked-insert',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            const trackedHistory = hooks.createHistoryController(trackedModel, {
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });
            trackedHistory.commitOperation(hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 0 },
                text: 'abc',
                revisionId: 'rev-ins',
                revision: {
                    id: 'rev-ins',
                    type: 'Insertion',
                    status: 'Pending',
                    affectedRange: { blockId: 'p1', start: 0, end: 3 },
                    payload: { text: 'abc' },
                    payloadJson: 'abc'
                }
            }, { source: 'typing', timestamp: 1000 }), {
                transactionType: 'typing',
                label: 'Tracked typing'
            });

            assert.strictEqual(text(trackedModel), 'abc');
            assert.strictEqual(trackedModel.revisions.length, 1);
            assert.strictEqual(trackedHistory.debug().undoDepth, 1);
            trackedHistory.undo();
            assert.strictEqual(text(trackedModel), '');
            assert.strictEqual(trackedModel.revisions.length, 0);
            trackedHistory.redo();
            assert.strictEqual(text(trackedModel), 'abc');
            assert.strictEqual(trackedModel.revisions.length, 1);

            const reviewModel = hooks.importFromCSharpJson({
                DocumentId: 'phase11-review',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [
                    { Id: 'r1', Text: 'Priority ', RevisionId: 'rev-a' },
                    { Id: 'r2', Text: 'support' }
                ] } }],
                Revisions: [{
                    Id: 'rev-a',
                    Type: 'Insertion',
                    Author: 'author-a',
                    Status: 'Pending',
                    AffectedRange: { BlockId: 'p1', Start: 0, End: 9 },
                    Payload: { text: 'Priority ' },
                    PayloadJson: 'Priority '
                }]
            });
            const reviewHistory = hooks.createHistoryController(reviewModel, {
                selection: { blockId: 'p1', offset: 9, isCollapsed: true }
            });
            reviewHistory.commitOperation(hooks.createOperation('AcceptRevision', {
                revisionId: 'rev-a',
                selection: { blockId: 'p1', offset: 9, isCollapsed: true }
            }, { source: 'review', timestamp: 1200 }), {
                transactionType: 'revision',
                label: 'Accept revision'
            });

            assert.strictEqual(reviewModel.revisions[0].status, 'Accepted');
            assert.strictEqual(reviewModel.body.blocks[0].content.runs[0].revisionId || null, null);
            assert.strictEqual(reviewHistory.debug().undoDepth, 1);
            const undoAccept = reviewHistory.undo();
            assert.strictEqual(reviewModel.revisions[0].status, 'Pending');
            assert.strictEqual(reviewModel.body.blocks[0].content.runs[0].revisionId, 'rev-a');
            assert.strictEqual(undoAccept.selection.blockId, 'p1');
            assert.strictEqual(undoAccept.selection.offset, 9);
            reviewHistory.redo();
            assert.strictEqual(reviewModel.revisions[0].status, 'Accepted');
            assert.strictEqual(reviewModel.body.blocks[0].content.runs[0].revisionId || null, null);

            console.log('OK');

            function text(doc) {
                return doc.body.blocks[0].content.runs.map(run => run.text || '').join('');
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "revisions");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_SelectionOnlyChange_DoesNotCreateUndoItem()
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
                DocumentId: 'phase11-selection',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello world' }] } }]
            });
            const history = hooks.createHistoryController(model, {
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            const commit = history.commitOperation(hooks.createOperation('SetSelection', {
                selection: { blockId: 'p1', offset: 5, isCollapsed: true }
            }, { source: 'selection', timestamp: 1000 }), {
                label: 'Move selection',
                beforeSelection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });
            const debug = history.debug();

            assert.strictEqual(commit.ok, true);
            assert.strictEqual(commit.historyEntry, null);
            assert.strictEqual(commit.undoDepth, 0);
            assert.strictEqual(debug.undoDepth, 0);
            assert.strictEqual(debug.redoDepth, 0);
            assert.strictEqual(history.getSelection().blockId, 'p1');
            assert.strictEqual(history.getSelection().offset, 5);
            assert.strictEqual(text(model), 'Hello world');

            console.log('OK');

            function text(doc) {
                return doc.body.blocks[0].content.runs.map(run => run.text || '').join('');
            }
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "selection");
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase11_SaveKeepsUndoStackButReloadStartsFreshStack()
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
            const harness = hooks.createUndoStackContractHarness({
                DocumentId: 'phase11-save',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });

            const commit = harness.commitOperation(hooks.createOperation('InsertText', {
                target: { blockId: 'p1', offset: 0 },
                text: 'saved'
            }, { source: 'typing', timestamp: 1000 }), {
                transactionType: 'typing',
                label: 'Typing'
            });
            const stateAfterTyping = harness.state();
            const save = harness.saveAck();
            const stateAfterSave = harness.state();
            const stateAfterReload = harness.reload({
                DocumentId: 'phase11-save-reloaded',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'saved' }] } }]
            });

            assert.strictEqual(commit.ok, true);
            assert.strictEqual(harness.text(), 'saved');
            assert.strictEqual(stateAfterTyping.CanUndo, true);
            assert.strictEqual(stateAfterTyping.UndoDepth, 1);
            assert.strictEqual(save.isDirty || save.IsDirty || false, false);
            assert.strictEqual(stateAfterSave.CanUndo, true, 'save acknowledgement must not clear undo');
            assert.strictEqual(stateAfterSave.UndoDepth, 1);
            assert.strictEqual(stateAfterReload.CanUndo, false, 'reload must create a fresh undo stack');
            assert.strictEqual(stateAfterReload.CanRedo, false);
            assert.strictEqual(stateAfterReload.UndoDepth, 0);
            assert.strictEqual(stateAfterReload.RedoDepth, 0);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript, "save-reload");
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

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(
        string scriptPath,
        string nodeScript,
        string testName)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase11-undo-{testName}-{Guid.NewGuid():N}.js");
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
            const clock = { now: () => Date.now() };
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON,
                Date,
                Math,
                Promise,
                performance: clock
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            sandbox.window.performance = clock;
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
