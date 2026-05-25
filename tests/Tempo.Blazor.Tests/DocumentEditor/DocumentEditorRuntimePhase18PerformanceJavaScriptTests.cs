using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase18PerformanceJavaScriptTests
{
    [Fact]
    public async Task Phase18_DebugMetricsExposeRenderBudgetCountersAndLatencyHistograms()
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
            sandbox.window.innerHeight = 900;
            sandbox.window.scrollY = 0;
            sandbox.window.pageYOffset = 0;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, { InstanceId: 'phase18' }, null);
            engine.loadDocument('phase18', {
                Document: {
                    DocumentId: 'phase18-doc',
                    Blocks: [
                        { Id: 'b1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'i1', Text: 'Hello' }] } }
                    ]
                }
            });

            engine.clearDebugMetrics('phase18');
            const metrics = engine.getDebugMetrics('phase18');
            assert.strictEqual(metrics.FullRenderCount, 0);
            assert.strictEqual(metrics.PartialRenderCount, 0);
            assert.strictEqual(metrics.BlazorCallbackDuringTypingCount, 0);
            assert.strictEqual(metrics.FormattingStateEventCount, 0);
            assert.strictEqual(metrics.ToolbarStateLayoutThrashCount, 0);
            assert.ok(metrics.LatencyBudgets.KeydownVisibleTextMs > 0);
            assert.strictEqual(metrics.KeydownVisibleTextHistogram.Count, 0);
            assert.strictEqual(metrics.SpaceVisibleTextHistogram.Count, 0);
            assert.strictEqual(metrics.EnterVisibleTextHistogram.Count, 0);
            assert.strictEqual(metrics.ToolbarCommandVisibleStyleHistogram.Count, 0);
            assert.strictEqual(metrics.SelectionChangeToolbarStateHistogram.Count, 0);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase18_InputDomApplyRecordsSeparateKeySpaceAndEnterHistograms()
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
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const engine = sandbox.window.tmDocumentEditorEngine;
            const hooks = engine.__testHooks;
            const types = engine.operations.types;
            const inst = {
                id: 'phase18-histograms',
                options: {},
                performanceStats: hooks.createStrictPerformanceStats(),
                diagnostics: { timeline: [], lastErrors: [], watchdogFailures: [], debugWarnings: [] },
                jsOwnedInputCount: 0
            };

            hooks.recordInputDomApply(inst, types.InsertText, { type: types.InsertText, target: { blockId: 'b1', offset: 0 }, text: 'a' });
            hooks.recordInputDomApply(inst, types.InsertText, { type: types.InsertText, target: { blockId: 'b1', offset: 1 }, text: ' ' });
            hooks.recordInputDomApply(inst, types.SplitParagraph, { type: types.SplitParagraph, target: { blockId: 'b1', offset: 2 }, newBlockId: 'b2' });

            const stats = inst.performanceStats;
            assert.strictEqual(stats.partialRenderCount, 3);
            assert.strictEqual(stats.textNodePatchCount, 2);
            assert.strictEqual(stats.blockPatchCount, 1);
            assert.deepStrictEqual(Array.from(stats.lastPartialRenderScopeIds), ['b1', 'b2']);
            assert.strictEqual(hooks.createLatencyHistogramSummary(stats.latencyHistograms.KeydownVisibleText, 150).Count, 1);
            assert.strictEqual(hooks.createLatencyHistogramSummary(stats.latencyHistograms.SpaceVisibleText, 150).Count, 1);
            assert.strictEqual(hooks.createLatencyHistogramSummary(stats.latencyHistograms.EnterVisibleText, 220).Count, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase18_BoundaryMetricsSeparateTypingCallbacksAndFormattingStateEvents()
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
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;
            const inst = {
                id: 'phase18-boundary',
                options: { TypingBatchMs: 500 },
                dotNetRef: null,
                pendingTypingBoundaryPatches: [{ transactionType: 'typing' }],
                performanceStats: hooks.createStrictPerformanceStats(),
                diagnostics: { timeline: [], lastErrors: [], watchdogFailures: [], debugWarnings: [] },
                boundaryFailures: []
            };

            hooks.invokeBoundaryMethod(inst, 'HandleJsBoundaryPatchGenerated', {}, 'boundary');
            hooks.invokeBoundaryMethod(inst, 'HandleFormattingStateChanged', {}, 'formatting');

            assert.strictEqual(inst.performanceStats.blazorInteropCallCount, 2);
            assert.strictEqual(inst.performanceStats.blazorCallbackDuringTypingCount, 2);
            assert.strictEqual(inst.performanceStats.formattingStateEventCount, 1);
            assert.strictEqual(inst.performanceStats.formattingStateNotifyCount, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase18_TypingBoundaryPatchesStayLightweightUntilFlush()
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
            sandbox.window.innerHeight = 900;
            sandbox.window.scrollY = 0;
            sandbox.window.pageYOffset = 0;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            (async () => {
                const engine = sandbox.window.tmDocumentEditorEngine;
                const calls = [];
                const dotNet = {
                    invokeMethodAsync(method, payload) {
                        calls.push({ method, payload });
                        return Promise.resolve(true);
                    }
                };
                const root = createRoot();
                engine.create(root, { InstanceId: 'phase18-lightweight', TypingBatchMs: 10 }, dotNet);
                engine.loadDocument('phase18-lightweight', {
                    Document: {
                        DocumentId: 'phase18-lightweight-doc',
                        Blocks: [
                            { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: '' }] } }
                        ]
                    }
                });
                engine.clearDebugMetrics('phase18-lightweight');

                const first = engine.applyCommand('phase18-lightweight', 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 0 },
                    text: 'a'
                });
                const second = engine.applyCommand('phase18-lightweight', 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 1 },
                    text: 'b'
                });

                assert.strictEqual(first.boundaryPatch.lightweight, true);
                assert.strictEqual(second.boundaryPatch.lightweight, true);
                assert.strictEqual(first.boundaryPatch.csharpDocument, undefined);
                assert.strictEqual(second.boundaryPatch.snapshot, undefined);
                assert.strictEqual(calls.some(call => call.method === 'HandleJsBoundaryPatchGenerated'), false);

                await new Promise(resolve => setTimeout(resolve, 35));
                const patchCalls = calls.filter(call => call.method === 'HandleJsBoundaryPatchGenerated');
                assert.strictEqual(patchCalls.length, 1);
                assert.strictEqual(patchCalls[0].payload.lightweight, false);
                assert.ok(patchCalls[0].payload.snapshot);
                assert.ok(patchCalls[0].payload.csharpDocument);
                assert.strictEqual(patchCalls[0].payload.coalescedPatchCount, 2);

                const metrics = engine.getDebugMetrics('phase18-lightweight');
                assert.strictEqual(metrics.LightweightBoundaryPatchCount, 2);
                assert.strictEqual(metrics.BoundarySnapshotExportCount, 1);

                const deleteResult = engine.applyCommand('phase18-lightweight', 'DeleteRange', {
                    transactionType: 'delete',
                    range: { blockId: 'p1', start: 1, end: 2 }
                });
                assert.strictEqual(deleteResult.boundaryPatch.lightweight, true);
                assert.strictEqual(deleteResult.boundaryPatch.csharpDocument, undefined);
                await new Promise(resolve => setTimeout(resolve, 35));

                const afterDeletePatchCalls = calls.filter(call => call.method === 'HandleJsBoundaryPatchGenerated');
                assert.strictEqual(afterDeletePatchCalls.length, 2);
                const deleteMetrics = engine.getDebugMetrics('phase18-lightweight');
                assert.strictEqual(deleteMetrics.LightweightBoundaryPatchCount, 3);
                assert.strictEqual(deleteMetrics.BoundarySnapshotExportCount, 2);

                console.log('OK');
            })().catch(error => {
                console.error(error);
                process.exit(1);
            });
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase18_FormattingAndUndoBoundaryPatchesDeferSnapshotExportUntilFlush()
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
            sandbox.window.innerHeight = 900;
            sandbox.window.scrollY = 0;
            sandbox.window.pageYOffset = 0;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            (async () => {
                const engine = sandbox.window.tmDocumentEditorEngine;
                const calls = [];
                const dotNet = {
                    invokeMethodAsync(method, payload) {
                        calls.push({ method, payload });
                        return Promise.resolve(true);
                    }
                };
                const root = createRoot();
                engine.create(root, { InstanceId: 'phase18-formatting', BoundaryPatchBatchMs: 10 }, dotNet);
                engine.loadDocument('phase18-formatting', {
                    Document: {
                        DocumentId: 'phase18-formatting-doc',
                        Blocks: [
                            { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Hello world' }] } }
                        ]
                    }
                });
                engine.applyCommand('phase18-formatting', 'SetSelection', {
                    selection: {
                        blockId: 'p1',
                        offset: 0,
                        anchor: { blockId: 'p1', offset: 0 },
                        focus: { blockId: 'p1', offset: 5 },
                        isCollapsed: false
                    }
                });
                engine.clearDebugMetrics('phase18-formatting');

                const bold = engine.applyCommand('phase18-formatting', 'toggleBold', {});
                assert.strictEqual(bold.ok, true);
                assert.strictEqual(bold.boundaryPatch.lightweight, true);
                assert.strictEqual(bold.boundaryPatch.csharpDocument, undefined);
                assert.strictEqual(calls.some(call => call.method === 'HandleJsBoundaryPatchGenerated'), false);

                await new Promise(resolve => setTimeout(resolve, 35));
                let patchCalls = calls.filter(call => call.method === 'HandleJsBoundaryPatchGenerated');
                assert.strictEqual(patchCalls.length, 1);
                assert.strictEqual(patchCalls[0].payload.lightweight, false);
                assert.ok(patchCalls[0].payload.csharpDocument);
                assert.strictEqual(patchCalls[0].payload.transactionType, 'default');

                const undo = engine.applyCommand('phase18-formatting', 'undo', {});
                assert.strictEqual(undo.ok, true);
                assert.strictEqual(undo.boundaryPatch.lightweight, true);
                assert.strictEqual(undo.boundaryPatch.csharpDocument, undefined);

                await new Promise(resolve => setTimeout(resolve, 35));
                patchCalls = calls.filter(call => call.method === 'HandleJsBoundaryPatchGenerated');
                assert.strictEqual(patchCalls.length, 2);
                assert.strictEqual(patchCalls[1].payload.lightweight, false);
                assert.ok(patchCalls[1].payload.csharpDocument);
                assert.strictEqual(patchCalls[1].payload.transactionType, 'undo');

                const metrics = engine.getDebugMetrics('phase18-formatting');
                assert.strictEqual(metrics.LightweightBoundaryPatchCount, 2);
                assert.strictEqual(metrics.BoundarySnapshotExportCount, 2);
                assert.strictEqual(metrics.DeferredBoundaryPatchDispatchCount, 2);
                assert.strictEqual(calls.some(call => call.method === 'HandleRevisionsChanged'), false);

                console.log('OK');
            })().catch(error => {
                console.error(error);
                process.exit(1);
            });
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase18_TrackChangesTypingDefersRevisionInteropUntilTypingFlush()
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
            sandbox.window.innerHeight = 900;
            sandbox.window.scrollY = 0;
            sandbox.window.pageYOffset = 0;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            (async () => {
                const engine = sandbox.window.tmDocumentEditorEngine;
                const calls = [];
                const dotNet = {
                    invokeMethodAsync(method, payload) {
                        calls.push({ method, payload });
                        return Promise.resolve(true);
                    }
                };
                const root = createRoot();
                engine.create(root, { InstanceId: 'phase18-revisions', TypingBatchMs: 10, TrackChangesEnabled: true }, dotNet);
                engine.loadDocument('phase18-revisions', {
                    Document: {
                        DocumentId: 'phase18-revisions-doc',
                        Blocks: [
                            { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: '' }] } }
                        ],
                        Revisions: []
                    }
                });
                engine.clearDebugMetrics('phase18-revisions');

                engine.applyCommand('phase18-revisions', 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 0 },
                    text: 'a',
                    revisionId: 'rev-a',
                    revision: { id: 'rev-a', type: 'Insertion', status: 'Pending', affectedRange: { blockId: 'p1', start: 0, end: 1 }, payload: { text: 'a' }, payloadJson: 'a' }
                });
                engine.applyCommand('phase18-revisions', 'InsertText', {
                    transactionType: 'typing',
                    target: { blockId: 'p1', offset: 1 },
                    text: 'b',
                    revisionId: 'rev-b',
                    revision: { id: 'rev-b', type: 'Insertion', status: 'Pending', affectedRange: { blockId: 'p1', start: 1, end: 2 }, payload: { text: 'b' }, payloadJson: 'b' }
                });

                assert.strictEqual(calls.some(call => call.method === 'HandleRevisionsChanged'), false);

                await new Promise(resolve => setTimeout(resolve, 35));
                const revisionCalls = calls.filter(call => call.method === 'HandleRevisionsChanged');
                assert.strictEqual(revisionCalls.length, 1);
                assert.ok(Array.isArray(revisionCalls[0].payload));
                assert.ok(revisionCalls[0].payload.length >= 1);

                const metrics = engine.getDebugMetrics('phase18-revisions');
                assert.strictEqual(metrics.DeferredRevisionNotifyCount, 2);
                assert.strictEqual(metrics.RevisionNotifyCount, 1);
                assert.strictEqual(metrics.MarkerStoreDeferredRefreshCount, 2);

                console.log('OK');
            })().catch(error => {
                console.error(error);
                process.exit(1);
            });
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase18-{Guid.NewGuid():N}.js");
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
