using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase5JavaScriptTests
{
    [Fact]
    public async Task Phase5_RuntimeFacade_ExposesStablePublicApiAndInternalModules()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = RuntimeSandboxSetup +
            """
            const runtime = sandbox.window.tmDocumentEditorRuntime;
            assert.ok(runtime, 'runtime facade must exist');
            assert.strictEqual(typeof runtime.create, 'function');

            const publicKeys = Object.keys(runtime)
                .filter(key => !key.startsWith('__'))
                .sort();
            assert.deepStrictEqual(publicKeys, [
                'applyOfflineState',
                'applyRemoteCursor',
                'applyRemoteOperation',
                'applyRemoteOperationBatch',
                'applyRemoteOperations',
                'captureCommentAnchor',
                'clearRevisionDecorations',
                'closeHeaderFooter',
                'create',
                'dispose',
                'executeCommand',
                'focus',
                'getDebugSnapshot',
                'getDebugUndoStack',
                'getDirtyState',
                'getDocument',
                'getFormattingState',
                'getLastCommandTransaction',
                'getLinkInfo',
                'getOfflineState',
                'getPageMetrics',
                'getRuntimeSelection',
                'getSelectionSnapshot',
                'getUndoState',
                'insertImageNode',
                'loadDocument',
                'markSaved',
                'onSelectionStateChanged',
                'onTransactionCommitted',
                'redo',
                'removeComment',
                'restoreSelection',
                'reviewAllRevisions',
                'reviewRevision',
                'scrollToComment',
                'scrollToRevision',
                'setReadOnly',
                'setReviewDisplayMode',
                'setTrackChangesEnabled',
                'undo',
                'upsertComment'
            ]);

            const internal = runtime.__internal;
            assert.ok(internal, 'internal runtime namespace must exist');
            assert.deepStrictEqual(JSON.parse(JSON.stringify(internal.getModuleNames())), [
                'clipboard',
                'comments',
                'core',
                'formatting',
                'image',
                'input',
                'rendering',
                'revisions',
                'selection',
                'serialization',
                'table',
                'watchdog'
            ]);

            assert.strictEqual(typeof internal.modules.core.create, 'function');
            assert.strictEqual(typeof internal.modules.selection.onSelectionStateChanged, 'function');
            assert.strictEqual(typeof internal.modules.rendering.loadDocument, 'function');
            assert.strictEqual(typeof internal.modules.input.focus, 'function');
            assert.strictEqual(typeof internal.modules.formatting.executeCommand, 'function');
            assert.strictEqual(typeof internal.modules.clipboard.getLinkInfo, 'function');
            assert.strictEqual(typeof internal.modules.image.insertImageNode, 'function');
            assert.strictEqual(typeof internal.modules.table.insertTable, 'function');
            assert.strictEqual(typeof internal.modules.comments.upsertComment, 'function');
            assert.strictEqual(typeof internal.modules.revisions.setReviewDisplayMode, 'function');
            assert.strictEqual(typeof internal.modules.serialization.normalizeSnapshot, 'function');
            assert.strictEqual(typeof internal.modules.watchdog.getState, 'function');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase5_RuntimeFacade_DelegatesCoreFormattingTableImageAndUndoRedoCommands()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = RuntimeSandboxSetup +
            """
            const calls = [];
            sandbox.window.tmDocumentEditorWysiwyg = makeMockEngine({
                create(root, options) {
                    calls.push(['create', options.InstanceId]);
                    return options.InstanceId;
                },
                executeCommand(instanceId, command, payload) {
                    calls.push(['executeCommand', instanceId, command, payload || null]);
                },
                undo(instanceId) {
                    calls.push(['undo', instanceId]);
                    return true;
                },
                redo(instanceId) {
                    calls.push(['redo', instanceId]);
                    return true;
                },
                insertImageNode(instanceId, block) {
                    calls.push(['insertImageNode', instanceId, block.Id || block.id]);
                    return true;
                },
                getUndoState(instanceId) {
                    calls.push(['getUndoState', instanceId]);
                    return { CanUndo: true, CanRedo: false, UndoDepth: 1, RedoDepth: 0 };
                }
            });

            const runtime = sandbox.window.tmDocumentEditorRuntime;
            runtime.create({}, { InstanceId: 'phase5' }, null);
            runtime.executeCommand('phase5', 'toggleBold', { value: true });
            runtime.executeCommand('phase5', 'insertTable', { rows: 3, columns: 4 });
            runtime.executeCommand('phase5', 'insertImageUrl', { url: '/image.png', altText: 'Alt' });
            runtime.insertImageNode('phase5', { Id: 'img-1', Content: { $type: 'image' } }, true);
            runtime.undo('phase5');
            runtime.redo('phase5');
            const undo = runtime.getUndoState('phase5');

            assert.deepStrictEqual(calls.map(call => call[0]), [
                'create',
                'executeCommand',
                'getUndoState',
                'executeCommand',
                'getUndoState',
                'executeCommand',
                'getUndoState',
                'insertImageNode',
                'undo',
                'redo',
                'getUndoState'
            ]);
            assert.strictEqual(calls[1][2], 'toggleBold');
            assert.strictEqual(calls[3][2], 'insertTable');
            assert.strictEqual(calls[3][3].rows, 3);
            assert.strictEqual(calls[5][2], 'insertImageUrl');
            assert.strictEqual(calls[7][2], 'img-1');
            assert.strictEqual(undo.CanUndo, true);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase5_RuntimeFacade_LoadDocumentGetDocument_RoundTripsThroughSerializationModule()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript = RuntimeSandboxSetup +
            """
            let appliedSnapshot = null;
            sandbox.window.tmDocumentEditorWysiwyg = makeMockEngine({
                applySnapshot(instanceId, snapshot) {
                    appliedSnapshot = snapshot;
                },
                getSnapshot() {
                    return null;
                }
            });

            const runtime = sandbox.window.tmDocumentEditorRuntime;
            const snapshot = {
                ProtocolVersion: 1,
                Document: {
                    SchemaVersion: 1,
                    DocumentId: 'phase5-doc',
                    Blocks: [
                        {
                            Id: 'b1',
                            Content: {
                                $type: 'paragraph',
                                Inlines: [
                                    { $type: 'text', Id: 'i1', Text: 'Hello' },
                                    { $type: 'text', Id: 'i2', Text: ' world' }
                                ]
                            }
                        }
                    ]
                }
            };

            runtime.loadDocument('phase5-doc', snapshot);
            const raw = runtime.getDocument('phase5-doc');
            const roundTrip = JSON.parse(raw);
            assert.strictEqual(appliedSnapshot.Document.DocumentId, 'phase5-doc');
            assert.strictEqual(roundTrip.Document.DocumentId, 'phase5-doc');
            assert.strictEqual(roundTrip.Document.Blocks[0].Content.Inlines[0].Text, 'Hello world');

            const normalized = runtime.__internal.modules.serialization.normalizeSnapshot(snapshot);
            assert.strictEqual(normalized.Document.DocumentId, 'phase5-doc');
            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    private static string RuntimeSandboxSetup =>
        """
        const fs = require('fs');
        const vm = require('vm');
        const assert = require('assert');

        const code = fs.readFileSync(process.argv[2], 'utf8');
        const pendingTimers = [];
        const sandbox = {
            window: {},
            console,
            Map,
            WeakMap,
            URL,
            JSON,
            setTimeout: function (cb) { pendingTimers.push(cb); },
            clearTimeout: function () {}
        };
        sandbox.window.setTimeout = sandbox.setTimeout;
        sandbox.window.clearTimeout = sandbox.clearTimeout;
        sandbox.window.addEventListener = function () {};
        sandbox.window.removeEventListener = function () {};

        vm.createContext(sandbox);
        vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

        function makeMockEngine(overrides) {
            return Object.assign({
                create: function (rootEl, opts) { return opts && (opts.InstanceId || opts.instanceId) || 'inst'; },
                dispose: function () {},
                executeCommand: function () {},
                applySnapshot: function () {},
                getSnapshot: function () { return null; },
                applyRemoteOperation: function () {},
                applyRemoteOperationBatch: function () {},
                applyRemoteOperations: function () {},
                applyRemoteCursor: function () {},
                setTrackChangesEnabled: function () {},
                setReviewDisplayMode: function () {},
                setReadOnly: function () {},
                scrollToRevision: function () {},
                scrollToComment: function () {},
                upsertComment: function () {},
                removeComment: function () {},
                reviewRevision: function () {},
                clearRevisionDecorations: function () {},
                restoreSelection: function () {},
                focus: function () {},
                closeHeaderFooter: function () {},
                captureCommentAnchor: function () { return null; },
                getDebugSnapshot: function () { return null; },
                getFormattingState: function () { return null; },
                getLastCommandTransaction: function () { return null; },
                getUndoState: function () { return null; },
                getDebugUndoStack: function () { return null; },
                getDirtyState: function () { return null; },
                markSaved: function () { return false; },
                getOfflineState: function () { return null; },
                applyOfflineState: function () { return false; },
                undo: function () { return false; },
                redo: function () { return false; },
                getRuntimeSelection: function () { return null; },
                getSelectionSnapshot: function () { return null; },
                getLinkInfo: function () { return null; },
                insertImageNode: function () { return false; }
            }, overrides || {});
        }
        """;

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tempo-phase5-{Guid.NewGuid():N}.js");
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
