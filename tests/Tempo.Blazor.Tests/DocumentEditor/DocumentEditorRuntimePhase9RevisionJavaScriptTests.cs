using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase9RevisionJavaScriptTests
{
    [Fact]
    public async Task Phase9_TrackChangesState_LocalSettingOverridesGlobalDefault()
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

            assert.deepStrictEqual(
                hooks.resolveTrackChangesState({ GlobalTrackChangesEnabled: true }).enabled,
                true);
            assert.strictEqual(
                JSON.stringify(hooks.resolveTrackChangesState({ GlobalTrackChangesEnabled: true, TrackChangesEnabled: false })),
                JSON.stringify({
                    displayMode: 'AllMarkup',
                    enabled: false,
                    globalEnabled: true,
                    localEnabled: false,
                    source: 'local'
                }));
            assert.deepStrictEqual(
                hooks.resolveTrackChangesState({ globalTrackChangesEnabled: false, trackChangesEnabled: true }).enabled,
                true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase9_InputPipeline_CoalescesSequentialTrackedTypingIntoSingleRevision()
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
                DocumentId: 'phase9-typing',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                trackChanges: true,
                userId: 'author-a',
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            for (const ch of 'jak se mas') {
                pipeline.handleBeforeInput({ inputType: 'insertText', data: ch, preventDefault() {} });
            }

            const text = model.body.blocks[0].content.runs.map(run => run.text || '').join('');
            const revisionIds = [...new Set(model.body.blocks[0].content.runs.map(run => run.revisionId).filter(Boolean))];

            assert.strictEqual(text, 'jak se mas');
            assert.strictEqual(model.revisions.length, 1);
            assert.strictEqual(revisionIds.length, 1);
            assert.strictEqual(model.revisions[0].type, 'Insertion');
            assert.strictEqual(model.revisions[0].author, 'author-a');
            assert.strictEqual(model.revisions[0].payload.text, 'jak se mas');
            assert.strictEqual(model.revisions[0].affectedRange.start, 0);
            assert.strictEqual(model.revisions[0].affectedRange.end, 'jak se mas'.length);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase9_RuntimeRevisionExport_UsesCSharpAuthorShape()
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
                DocumentId: 'phase9-export',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: '' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                trackChanges: true,
                userId: 'author-a',
                selection: { blockId: 'p1', offset: 0, isCollapsed: true }
            });

            pipeline.handleBeforeInput({ inputType: 'insertText', data: 'a', preventDefault() {} });

            const exported = hooks.exportToCSharpJson(model);
            const revision = exported.Revisions[0];

            assert.strictEqual(exported.Revisions.length, 1);
            assert.strictEqual(revision.PayloadJson, 'a');
            assert.strictEqual(revision.Author.Id, 'author-a');
            assert.strictEqual(revision.Author.DisplayName, 'author-a');
            assert.strictEqual(typeof revision.Author, 'object');
            assert.strictEqual(typeof revision.CreatedAt, 'string');
            assert.ok(revision.CreatedAt.includes('T'));

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase9_FormattingStateExport_UsesCSharpEnumShape()
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
            const state = hooks.toBlazorFormattingState({
                commandValues: { bold: false, italic: true, underline: false },
                inline: { mixed: { bold: false, italic: false, underline: true } },
                paragraph: { alignment: 'left' },
                selection: { region: 'Body', blockId: 'p1', offset: 0, isCollapsed: true }
            });

            assert.strictEqual(state.Bold, 0);
            assert.strictEqual(state.bold, 0);
            assert.strictEqual(state.Italic, 1);
            assert.strictEqual(state.italic, 1);
            assert.strictEqual(state.Underline, 2);
            assert.strictEqual(state.underline, 2);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase9_InputPipeline_DeleteWithTrackChangesMarksTextWithoutRemovingIt()
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
                DocumentId: 'phase9-delete',
                Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'abcdef' }] } }]
            });
            const pipeline = hooks.createInputPipeline({
                model,
                trackChanges: true,
                userId: 'author-a',
                selection: { blockId: 'p1', offset: 3, isCollapsed: true }
            });

            pipeline.handleBeforeInput({ inputType: 'deleteContentBackward', data: null, preventDefault() {} });

            const text = model.body.blocks[0].content.runs.map(run => run.text || '').join('');
            const deletedRun = model.body.blocks[0].content.runs.find(run => run.text === 'c');

            assert.strictEqual(text, 'abcdef');
            assert.ok(deletedRun);
            assert.ok(deletedRun.revisionId);
            assert.strictEqual(model.revisions.length, 1);
            assert.strictEqual(model.revisions[0].type, 'Deletion');
            assert.strictEqual(model.revisions[0].payload.text, 'c');
            assert.strictEqual(model.revisions[0].affectedRange.start, 2);
            assert.strictEqual(model.revisions[0].affectedRange.end, 3);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase9_RevisionNormalizer_MergesCompatibleAdjacentRevisionsOnly()
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
                DocumentId: 'phase9-normalizer',
                Blocks: [{
                    Id: 'p1',
                    Type: 'Paragraph',
                    Content: {
                        Inlines: [
                            { Id: 'r1', Text: 'ja', RevisionId: 'rev-a' },
                            { Id: 'r2', Text: 'k', RevisionId: 'rev-b' },
                            { Id: 'r3', Text: '!', RevisionId: 'rev-c' },
                            { Id: 'r4', Text: '?', RevisionId: 'rev-d', CommentIds: ['comment-1'] },
                            { Id: 'r5', Text: 'x', RevisionId: 'rev-e', Marks: [{ Type: 'FontSize', Value: '12pt' }] },
                            { Id: 'r6', Text: 'y', RevisionId: 'rev-f', Marks: [{ Type: 'FontSize', Value: '14pt' }] }
                        ]
                    }
                }],
                Revisions: [
                    { Id: 'rev-a', Type: 0, Action: 0, Author: { Id: 'author-a', DisplayName: 'A' }, Range: { BlockId: 'p1', StartOffset: 0, EndOffset: 2 }, PayloadJson: 'ja' },
                    { Id: 'rev-b', Type: 0, Action: 0, Author: { Id: 'author-a', DisplayName: 'A' }, Range: { BlockId: 'p1', StartOffset: 2, EndOffset: 3 }, PayloadJson: 'k' },
                    { Id: 'rev-c', Type: 0, Action: 0, Author: { Id: 'author-b', DisplayName: 'B' }, Range: { BlockId: 'p1', StartOffset: 3, EndOffset: 4 }, PayloadJson: '!' },
                    { Id: 'rev-d', Type: 0, Action: 0, Author: { Id: 'author-b', DisplayName: 'B' }, Range: { BlockId: 'p1', StartOffset: 4, EndOffset: 5 }, PayloadJson: '?' },
                    { Id: 'rev-e', Type: 0, Action: 0, Author: { Id: 'author-a', DisplayName: 'A' }, Range: { BlockId: 'p1', StartOffset: 5, EndOffset: 6 }, PayloadJson: 'x' },
                    { Id: 'rev-f', Type: 0, Action: 0, Author: { Id: 'author-a', DisplayName: 'A' }, Range: { BlockId: 'p1', StartOffset: 6, EndOffset: 7 }, PayloadJson: 'y' }
                ]
            });

            const result = hooks.normalizeRevisionGroups(model);
            const ids = model.revisions.map(revision => revision.id).sort();
            const revA = model.revisions.find(revision => revision.id === 'rev-a');
            const runs = model.body.blocks[0].content.runs;

            assert.strictEqual(result.merged, 1);
            assert.deepStrictEqual(ids, ['rev-a', 'rev-c', 'rev-d', 'rev-e', 'rev-f']);
            assert.strictEqual(revA.payload.text, 'jak');
            assert.strictEqual(revA.affectedRange.end, 3);
            assert.ok(runs.some(run => run.text === 'jak' && run.revisionId === 'rev-a'));
            assert.ok(runs.some(run => run.text === '!' && run.revisionId === 'rev-c'));
            assert.ok(runs.some(run => run.text === '?' && run.revisionId === 'rev-d'));
            assert.ok(runs.some(run => run.text === 'x' && run.revisionId === 'rev-e'));
            assert.ok(runs.some(run => run.text === 'y' && run.revisionId === 'rev-f'));

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase9-revisions-{Guid.NewGuid():N}.js");
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
