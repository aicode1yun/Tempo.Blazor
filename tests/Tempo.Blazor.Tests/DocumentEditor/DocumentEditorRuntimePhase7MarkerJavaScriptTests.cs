using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase7MarkerJavaScriptTests
{
    [Fact]
    public async Task Phase7_RuntimeMarkerStore_AddsQueriesTransformsAndMapsRenderClasses()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function assertJson(actual, expected) {
                assert.strictEqual(JSON.stringify(actual), JSON.stringify(expected));
            }

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            const store = hooks.createMarkerStore([
                {
                    id: 'search-1',
                    type: 'search',
                    range: { startBlockId: 'b1', startOffset: 1, endBlockId: 'b1', endOffset: 4 },
                    priority: 10,
                    affectsData: false
                },
                {
                    id: 'comment-1',
                    type: 'comment',
                    range: { startBlockId: 'b1', startOffset: 2, endBlockId: 'b1', endOffset: 6 },
                    priority: 60,
                    affectsData: true,
                    targetId: 'comment-1',
                    status: 'resolved'
                },
                {
                    id: 'revision-1',
                    type: 'revisionDeletion',
                    range: { startBlockId: 'b1', startOffset: 2, endBlockId: 'b1', endOffset: 6 },
                    priority: 80,
                    affectsData: true,
                    targetId: 'revision-1'
                }
            ]);

            assertJson(store.all.map(m => m.id), ['revision-1', 'comment-1', 'search-1']);
            assertJson(store.byType('comment').map(m => m.id), ['comment-1']);
            assertJson(store.byBlock('b1').map(m => m.id), ['revision-1', 'comment-1', 'search-1']);
            assertJson(
                store.overlapping({ startBlockId: 'b1', startOffset: 3, endBlockId: 'b1', endOffset: 5 }).map(m => m.id),
                ['revision-1', 'comment-1', 'search-1']);

            const afterInsert = store.transformText('b1', 1, 2, false);
            const search = afterInsert.find(m => m.id === 'search-1');
            assert.strictEqual(search.range.startOffset, 3);
            assert.strictEqual(search.range.endOffset, 6);

            const afterDelete = store.transformText('b1', 2, 1, true);
            const comment = afterDelete.find(m => m.id === 'comment-1');
            assert.strictEqual(comment.range.startOffset, 3);
            assert.strictEqual(comment.range.endOffset, 7);

            const classes = store.renderClasses();
            assert.strictEqual(classes.find(m => m.id === 'search-1').className.includes('tm-wysiwyg-marker--search'), true);
            assert.strictEqual(classes.find(m => m.id === 'comment-1').className.includes('tm-wysiwyg-marker--comment'), true);
            assert.strictEqual(classes.find(m => m.id === 'comment-1').className.includes('tm-document-inline--comment-anchor--resolved'), true);
            assert.strictEqual(classes.find(m => m.id === 'revision-1').className.includes('tm-wysiwyg-marker--revision-delete'), true);
            assert.strictEqual(store.remove('search-1'), true);
            assertJson(store.byType('search').map(m => m.id), []);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase14_AutocompleteTriggerDetection_CoversTokenMentionSlashAndMarkerClasses()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = {
                window: {},
                console,
                setTimeout,
                clearTimeout,
                URL,
                JSON
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentWysiwyg.__testHooks;
            assert.deepStrictEqual(JSON.parse(JSON.stringify(hooks.detectAutocompleteTriggerText('Hello {{client', 14))), {
                triggerId: 'token',
                marker: '{{',
                markerType: 'tokenQuery',
                query: 'client',
                startOffset: 6,
                endOffset: 14
            });
            assert.strictEqual(hooks.detectAutocompleteTriggerText('Hello @alex', 11).triggerId, 'mention');
            assert.strictEqual(hooks.detectAutocompleteTriggerText('/table', 6).triggerId, 'slash');
            assert.strictEqual(hooks.detectAutocompleteTriggerText('Hello @alex world', 17), null);

            const store = hooks.createMarkerStore([
                {
                    id: 'tag-query',
                    type: 'tagQuery',
                    range: { startBlockId: 'b1', startOffset: 0, endBlockId: 'b1', endOffset: 4 },
                    priority: 35,
                    affectsData: false
                },
                {
                    id: 'slash-query',
                    type: 'slashQuery',
                    range: { startBlockId: 'b1', startOffset: 5, endBlockId: 'b1', endOffset: 11 },
                    priority: 35,
                    affectsData: false
                }
            ]);

            const classes = store.renderClasses();
            assert.strictEqual(classes.find(m => m.id === 'tag-query').testId, 'document-tag-query-marker');
            assert.strictEqual(classes.find(m => m.id === 'slash-query').testId, 'document-slash-query-marker');
            assert.strictEqual(classes.find(m => m.id === 'slash-query').className.includes('tm-wysiwyg-marker--slash-query'), true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static string GetWysiwygScriptPath()
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js");
    }

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

        throw new InvalidOperationException("Repository root was not found.");
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
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase7-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
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
            File.Delete(tempFile);
        }
    }
}
