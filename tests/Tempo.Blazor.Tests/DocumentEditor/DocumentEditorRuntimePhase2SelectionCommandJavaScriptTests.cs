using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase2SelectionCommandJavaScriptTests
{
    [Fact]
    public async Task Phase2_SelectionToken_SerializesCollapsedCaretRangeAndBoundaryPath()
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
                JSON,
                Date,
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase2-selection',
                Blocks: [
                    {
                        Id: 'p1',
                        Type: 'Paragraph',
                        Content: {
                            Inlines: [
                                { Id: 'r1', Text: 'Hello ' },
                                { Id: 'r2', Text: 'world' }
                            ]
                        }
                    }
                ]
            });

            const collapsed = hooks.withStableSelectionToken('inst-phase2', {
                region: 'Body',
                blockId: 'p1',
                anchor: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 3 },
                focus: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 3 },
                isCollapsed: true
            }, model);
            assert.strictEqual(typeof collapsed.SelectionToken, 'string');
            const collapsedToken = JSON.parse(collapsed.SelectionToken);
            assert.strictEqual(collapsedToken.instanceId, 'inst-phase2');
            assert.strictEqual(collapsedToken.documentInstanceId, 'inst-phase2');
            assert.strictEqual(collapsedToken.region, 'body');
            assert.strictEqual(collapsedToken.blockId, 'p1');
            assert.strictEqual(collapsedToken.startOffset, 3);
            assert.strictEqual(collapsedToken.endOffset, 3);
            assert.deepStrictEqual(collapsedToken.inlinePath.anchor, ['body', 'body', '', '', 'p1', 'r1', '3']);
            assert.ok(String(collapsedToken.documentFingerprint).startsWith('fnv1a-'));
            assert.strictEqual(collapsedToken.selectionDocumentFingerprint, collapsedToken.documentFingerprint);

            const ranged = hooks.withStableSelectionToken('inst-phase2', {
                region: 'Body',
                anchor: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 2 },
                focus: { region: 'Body', blockId: 'p1', inlineId: 'r2', offset: 9 },
                isCollapsed: false,
                direction: 'forward'
            }, model);
            const rangeToken = JSON.parse(ranged.SelectionToken);
            assert.strictEqual(rangeToken.isCollapsed, false);
            assert.strictEqual(rangeToken.startOffset, 2);
            assert.strictEqual(rangeToken.endOffset, 9);
            assert.strictEqual(rangeToken.anchor.runId, 'r1');
            assert.strictEqual(rangeToken.focus.runId, 'r1');

            const valid = hooks.validateStableSelectionToken('inst-phase2', ranged.SelectionToken, model);
            assert.strictEqual(valid.ok, true);
            assert.strictEqual(valid.selection.SelectionToken, ranged.SelectionToken);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase2_CommandTransactionsExposeFingerprintsAndCommandName()
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
                JSON,
                Date,
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const model = hooks.importFromCSharpJson({
                DocumentId: 'phase2-transaction',
                Blocks: [
                    { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Hello' }] } }
                ]
            });
            const beforeSelection = {
                region: 'Body',
                blockId: 'p1',
                anchor: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 5 },
                focus: { region: 'Body', blockId: 'p1', inlineId: 'r1', offset: 5 },
                isCollapsed: true
            };

            const beforeFingerprint = hooks.createDocumentFingerprint(model);
            const transaction = hooks.createTransaction(model, {
                instanceId: 'inst-transaction',
                commandName: 'Insert text',
                label: 'Insert text',
                beforeSelection
            });
            const operation = hooks.createOperation(hooks.createOperation.types?.InsertText || 'InsertText', {
                target: { blockId: 'p1', offset: 5 },
                text: '!',
                selection: beforeSelection
            }, { source: 'phase2-test' });
            const applied = transaction.apply(operation);
            assert.strictEqual(applied.ok, true);
            const committed = transaction.commit();
            assert.strictEqual(committed.ok, true);
            const json = transaction.toJSON();

            assert.ok(json.id);
            assert.strictEqual(json.commandName, 'Insert text');
            assert.strictEqual(json.label, 'Insert text');
            assert.strictEqual(json.beforeDocFingerprint, beforeFingerprint);
            assert.notStrictEqual(json.afterDocFingerprint, beforeFingerprint);
            assert.strictEqual(json.beforeSelection.SelectionTokenData.instanceId, 'inst-transaction');
            assert.strictEqual(json.afterSelection.SelectionTokenData.instanceId, 'inst-transaction');
            assert.strictEqual(json.operationCount, 1);
            assert.strictEqual(json.committed, true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase2_CommandFacade_PassesValidTokenAndReturnsDiagnosticForStaleToken()
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
                JSON,
                Date,
                Math
            };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.addEventListener = function () {};
            sandbox.window.removeEventListener = function () {};
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const calls = [];
            sandbox.window.tmDocumentEditorRuntime.executeCommand = function (instanceId, command, payload) {
                calls.push({ instanceId, command, payload });
                if (payload.SelectionToken === 'stale-token') {
                    return { ok: false, error: { code: 'stale-selection-token', reason: 'document-fingerprint-mismatch' } };
                }
                return { ok: true, transaction: { id: 'txn-1' } };
            };

            const ok = sandbox.window.tmDocumentWysiwygCommand.execute('inst-facade', {
                command: 'toggleBold',
                selectionToken: 'valid-token',
                payload: { Value: true }
            });
            assert.strictEqual(ok.ok, true);
            assert.strictEqual(calls[0].command, 'toggleBold');
            assert.strictEqual(calls[0].payload.Value, true);
            assert.strictEqual(calls[0].payload.SelectionToken, 'valid-token');
            assert.strictEqual(calls[0].payload.selectionToken, 'valid-token');

            const stale = sandbox.window.tmDocumentWysiwygCommand.execute('inst-facade', {
                command: 'toggleBold',
                selectionToken: 'stale-token'
            });
            assert.strictEqual(stale.ok, false);
            assert.strictEqual(stale.error.code, 'stale-selection-token');
            assert.strictEqual(stale.error.reason, 'document-fingerprint-mismatch');

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
            process!.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<NodeResult> RunNodeAsync(string scriptPath, string nodeScript)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase2-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, nodeScript);
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                ArgumentList = { tempFile, scriptPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            var stdout = await process!.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new NodeResult(process.ExitCode, stdout, stderr);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

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

    private sealed record NodeResult(int ExitCode, string StandardOutput, string StandardError);
}
