using System.Diagnostics;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorRuntimePhase22PerformanceJavaScriptTests
{
    [Fact]
    public async Task Phase22_WysiwygScript_PassesNodeSyntaxCheck()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "--check", scriptPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        process.ExitCode.Should().Be(0, stdout + stderr);
    }

    [Fact]
    public async Task Phase22_BaselineHarness_RecordsTypingImageSelectionAndMemoryReports()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const harness = sandbox.window.tmDocumentEditorEngine.__testHooks.createPerformanceMetricsHarness();
            const onePage = harness.recordTypingLatency('1-page', [3, 4, 5]);
            const tenPages = harness.recordTypingLatency('10-pages', [6, 7, 8]);
            const hundredPages = harness.recordTypingLatency('100-pages-virtualized', [9, 10, 11]);
            const image = harness.recordImageDragLatency([12, 14]);
            const selection = harness.recordSelectionMovementLatency([1, 2]);
            const cleanup = harness.recordMemoryCleanup({
                removedEventListeners: 3,
                clearedTimers: 2,
                disconnectedObservers: 1,
                measurementCacheEntriesBefore: 4,
                measurementCacheEntriesAfter: 0,
                dotNetRefCleared: true,
                instanceRemoved: true
            });
            const metrics = harness.metrics();
            const snapshot = harness.snapshot();

            assert.strictEqual(onePage.Count, 3);
            assert.strictEqual(tenPages.MaxMs, 8);
            assert.strictEqual(hundredPages.Name, 'typing-100-pages-virtualized');
            assert.strictEqual(image.AverageMs, 13);
            assert.strictEqual(selection.LastMs, 2);
            assert.strictEqual(cleanup.measurementCacheEntriesAfter, 0);
            assert.strictEqual(metrics.Baselines.length, 5);
            assert.strictEqual(snapshot.BaselineCount, 5);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase22_Operations_RecordGranularInvalidationWithoutFullDocumentLayout()
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

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    contains() { return true; },
                    addEventListener() {},
                    removeEventListener() {},
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const api = engine.operations;
            const root = createRoot();
            engine.create(root, { InstanceId: 'phase22-invalidation' }, null);
            engine.loadDocument('phase22-invalidation', {
                Document: {
                    DocumentId: 'phase22-invalidation-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Hello world' }] } },
                        { Id: 'p2', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r2', Text: 'Second paragraph' }] } },
                        { Id: 'img1', Type: 'Image', Content: { Type: 'Image', AltText: 'Image' } },
                        { Id: 'tbl1', Type: 'Table', Content: { Type: 'Table', Rows: [
                            { Id: 'row1', Cells: [{ Id: 'cell1', Blocks: [{ Id: 'cp1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'cr1', Text: 'Cell' }] } }] }] }
                        ] } }
                    ]
                }
            });
            engine.clearDebugMetrics('phase22-invalidation');

            const insert = api.createOperation(api.types.InsertText, { target: { blockId: 'p1', offset: 5 }, text: '!' }, { source: 'phase22' });
            const insertResult = engine.applyRemoteOperationBatch('phase22-invalidation', { Operations: [insert] });
            assert.strictEqual(JSON.stringify(insertResult.transaction.invalidatedScopes), JSON.stringify(['p1']));

            const remove = api.createOperation(api.types.DeleteRange, { range: { blockId: 'p1', start: 0, end: 1 } }, { source: 'phase22' });
            const removeResult = engine.applyRemoteOperationBatch('phase22-invalidation', { Operations: [remove] });
            assert.strictEqual(JSON.stringify(removeResult.transaction.invalidatedScopes), JSON.stringify(['p1']));

            const image = api.createOperation(api.types.UpdateImageLayout, {
                target: { blockId: 'img1', offset: 0 },
                layout: { wrapMode: 1, x: 80, y: 20, width: 220, height: 120 },
                affectedParagraphIds: ['p2']
            }, { source: 'phase22' });
            const imageResult = engine.applyRemoteOperationBatch('phase22-invalidation', { Operations: [image] });
            assert.ok(imageResult.transaction.invalidatedScopes.includes('img1'));
            assert.ok(imageResult.transaction.invalidatedScopes.includes('p2'));

            const table = api.createOperation(api.types.UpdateTableCell, {
                cellId: 'cell1',
                blocks: [{ Id: 'cp2', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'cr2', Text: 'Updated' }] } }]
            }, { source: 'phase22' });
            const tableResult = engine.applyRemoteOperationBatch('phase22-invalidation', { Operations: [table] });
            assert.strictEqual(JSON.stringify(tableResult.transaction.invalidatedScopes), JSON.stringify(['cell1']));

            const metrics = engine.getDebugMetrics('phase22-invalidation');
            assert.strictEqual(metrics.FullDocumentLayoutCount, 0);
            assert.ok(metrics.IncrementalOperationCount >= 4);
            assert.ok(metrics.TypingLatencyCount >= 2);
            assert.ok(metrics.ImageDragLatencyCount >= 1);
            assert.ok(metrics.InputOperationCount >= 4);
            assert.ok(metrics.ModelCommitCount >= 4);
            assert.strictEqual(typeof metrics.RenderSwapCount, 'number');
            assert.strictEqual(typeof metrics.ObjectTrackFrameCount, 'number');
            assert.strictEqual(metrics.ActiveRegion, 'Body');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase22_ObjectTrackPointerMovesDoNotMutateModelUntilPointerUpCommit()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            const hooks = sandbox.window.tmDocumentEditorEngine.__testHooks;

            function assertPreviewOnly(factory, frameCounterName) {
                const harness = factory({ threshold: 0 });
                const before = harness.state().modelJson;
                harness.begin(0, 0);
                let afterMoves = null;
                for (let index = 1; index <= 20; index++) {
                    afterMoves = harness.move(index * 4, index * 3);
                }

                assert.strictEqual(afterMoves.modelJson, before, 'pointermove must not mutate the document model');
                assert.strictEqual(afterMoves.commitCount, 0, 'pointermove must not create commits');
                assert.ok(afterMoves.performance.objectTrackFrameCount >= 20, 'every pointermove should be tracked as a preview frame');
                assert.ok(afterMoves.performance[frameCounterName] >= 20, `${frameCounterName} should count preview frames`);
                assert.strictEqual(afterMoves.performance.objectTrackCommitCount, 0);
                assert.strictEqual(afterMoves.performance.modelCommitCount, 0);

                const afterCommit = harness.up(96, 72);
                assert.notStrictEqual(afterCommit.modelJson, before, 'pointerup should commit the final object geometry');
                assert.strictEqual(afterCommit.commitCount, 1);
                assert.strictEqual(afterCommit.performance.objectTrackCommitCount, 1);
                assert.strictEqual(afterCommit.performance.modelCommitCount, 1);
            }

            assertPreviewOnly(hooks.createImageMoveTrackHarness, 'objectTrackDragFrameCount');
            assertPreviewOnly(hooks.createImageResizeTrackHarness, 'objectTrackResizeFrameCount');

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase22_DebugSnapshotAndWrapIntervalsExposePerformanceGuardrails()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
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
                    contains() { return true; },
                    addEventListener() {},
                    removeEventListener() {},
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, { InstanceId: 'phase22-debug' }, null);
            engine.loadDocument('phase22-debug', {
                Document: {
                    DocumentId: 'phase22-debug-doc',
                    Blocks: [
                        { Id: 'body-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'body-r', Text: 'Body text' }] } }
                    ],
                    HeadersFooters: [
                        { Id: 'header-primary', Region: 'Header', Type: 'Header', Blocks: [
                            { Id: 'header-p', Type: 'Paragraph', Content: { Inlines: [{ Id: 'header-r', Text: 'Header text' }] } }
                        ] }
                    ]
                }
            });
            engine.restoreSelection('phase22-debug', {
                region: 'Header',
                headerFooterId: 'header-primary',
                blockId: 'header-p',
                offset: 6,
                isCollapsed: true
            });
            engine.clearDebugMetrics('phase22-debug');

            const snapshot = engine.getDebugSnapshot('phase22-debug');
            const metrics = engine.getDebugMetrics('phase22-debug');
            assert.strictEqual(snapshot.ActiveRegion, 'Header');
            assert.strictEqual(snapshot.performanceStats.activeRegion, 'Header');
            assert.strictEqual(metrics.ActiveRegion, 'Header');
            assert.strictEqual(typeof metrics.RenderSwapCount, 'number');
            assert.strictEqual(typeof metrics.ObjectTrackFrameCount, 'number');
            assert.strictEqual(typeof metrics.ModelCommitCount, 'number');

            const hooks = engine.__testHooks;
            const body = { x: 40, y: 0, width: 500, height: 700 };
            const exclusions = [{ rect: { x: 80, y: 20, width: 120, height: 48 }, allowOverlap: false }];
            const first = hooks.getAvailableIntervals(24, 18, body, exclusions, 24);
            assert.strictEqual(typeof exclusions.__tmAvailableIntervalsCache?.get, 'function');
            assert.strictEqual(exclusions.__tmAvailableIntervalsCache.size, 1);
            first.intervals[0].x = -999;
            const second = hooks.getAvailableIntervals(24, 18, body, exclusions, 24);
            assert.notStrictEqual(second.intervals[0].x, -999, 'cached interval results must be cloned before returning to callers');
            assert.strictEqual(exclusions.__tmAvailableIntervalsCache.size, 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase22_LongDocument_VirtualizesOffscreenPagesAndMaterializesSelectionTargets()
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

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    contains() { return true; },
                    addEventListener() {},
                    removeEventListener() {},
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            const root = createRoot();
            engine.create(root, {
                InstanceId: 'phase22-virtual',
                VirtualizationBlocksPerPage: 1,
                VirtualizationThresholdPages: 10,
                VirtualizationRenderedPageRadius: 0
            }, null);
            engine.loadDocument('phase22-virtual', {
                Document: {
                    DocumentId: 'phase22-virtual-doc',
                    Blocks: Array.from({ length: 100 }, (_, index) => ({
                        Id: 'p' + index,
                        Type: 'Paragraph',
                        Content: { Type: 'Paragraph', Inlines: [{ Id: 'r' + index, Text: 'Page block ' + index }] }
                    }))
                }
            });

            let metrics = engine.getPageMetrics('phase22-virtual');
            assert.strictEqual(metrics.TotalPages, 100);
            assert.strictEqual(metrics.VirtualizationEnabled, true);
            assert.strictEqual(metrics.ActivePageIndex, 0);
            assert.strictEqual(metrics.Pages[0].IsVirtual, false);
            assert.strictEqual(metrics.Pages[50].IsVirtual, true);
            assert.ok(metrics.VirtualizedPages > 90);
            assert.strictEqual(metrics.Pages[99].BlockIds[0], 'p99');
            assert.ok(root.innerHTML.includes('tm-wysiwyg-page--virtual'));

            const scrollPage = engine.scrollToPage('phase22-virtual', 50);
            assert.strictEqual(scrollPage.ok, true);
            metrics = engine.getPageMetrics('phase22-virtual');
            assert.strictEqual(metrics.ActivePageIndex, 50);
            assert.strictEqual(metrics.Pages[50].IsVirtual, false);
            assert.strictEqual(metrics.Pages[0].IsVirtual, true);

            const scrollBlock = engine.scrollToBlock('phase22-virtual', 'p75');
            assert.strictEqual(scrollBlock.ok, true);
            metrics = engine.getPageMetrics('phase22-virtual');
            assert.strictEqual(metrics.ActivePageIndex, 75);
            assert.strictEqual(metrics.Pages[75].IsVirtual, false);

            const debugMetrics = engine.getDebugMetrics('phase22-virtual');
            assert.strictEqual(debugMetrics.VirtualizationEnabled, true);
            assert.strictEqual(debugMetrics.TotalPages, 100);
            assert.ok(debugMetrics.VirtualizedPages > 90);
            assert.ok(debugMetrics.MaxLiveDomBlockCount <= 1);

            console.log('OK');
            """;

        var result = await RunNodeAsync(scriptPath, nodeScript);
        result.ExitCode.Should().Be(0, result.StandardError);
        result.StandardOutput.Trim().Should().Be("OK");
    }

    [Fact]
    public async Task Phase22_Dispose_RemovesHandlersTimersObserversCacheAndInstanceReferences()
    {
        var scriptPath = GetWysiwygScriptPath();
        if (!IsNodeAvailable()) return;

        var nodeScript =
            """
            const fs = require('fs');
            const vm = require('vm');
            const assert = require('assert');

            const code = fs.readFileSync(process.argv[2], 'utf8');
            const sandbox = { window: {}, console, setTimeout, clearTimeout, URL, JSON, Date, Math };
            sandbox.window.setTimeout = setTimeout;
            sandbox.window.clearTimeout = clearTimeout;
            sandbox.window.console = console;
            sandbox.window.performance = { now: () => Date.now() };
            vm.createContext(sandbox);
            vm.runInContext(code, sandbox, { filename: 'document-editor-wysiwyg.js' });

            function createRoot() {
                return {
                    innerHTML: '',
                    attributes: {},
                    listenerCount: 0,
                    classList: { add() {}, toggle() {}, remove() {} },
                    setAttribute(name, value) { this.attributes[name] = String(value); },
                    removeAttribute(name) { delete this.attributes[name]; },
                    contains() { return true; },
                    addEventListener() { this.listenerCount++; },
                    removeEventListener() { this.listenerCount = Math.max(0, this.listenerCount - 1); },
                    querySelector() { return null; },
                    querySelectorAll() { return []; }
                };
            }

            const engine = sandbox.window.tmDocumentEditorEngine;
            let lastCleanup = null;
            for (let index = 0; index < 25; index++) {
                const root = createRoot();
                const dotNet = { invokeMethodAsync() { return Promise.resolve(); } };
                engine.create(root, { InstanceId: 'phase22-dispose' }, dotNet);
                const inst = engine.__testHooks.instances.get('phase22-dispose');
                inst.measurementCache.set('sample', { width: 10 });
                inst.timers.push(setTimeout(() => {}, 1000));
                inst.observers.push({ disconnected: false, disconnect() { this.disconnected = true; } });
                const disposed = engine.dispose('phase22-dispose');
                assert.strictEqual(disposed.ok, true);
                assert.strictEqual(disposed.cleanup.instanceRemoved, true);
                assert.strictEqual(disposed.cleanup.dotNetRefCleared, true);
                assert.strictEqual(disposed.cleanup.measurementCacheEntriesBefore, 1);
                assert.strictEqual(disposed.cleanup.measurementCacheEntriesAfter, 0);
                assert.ok(disposed.cleanup.removedEventListeners >= 3);
                assert.ok(disposed.cleanup.clearedTimers >= 1);
                assert.strictEqual(disposed.cleanup.disconnectedObservers, 1);
                lastCleanup = disposed.cleanup;
            }

            assert.strictEqual(engine.__testHooks.instances.has('phase22-dispose'), false);
            assert.strictEqual(engine.__testHooks.instances.size, 0);
            assert.strictEqual(lastCleanup.instanceRemoved, true);

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
        var tempFile = Path.Combine(Path.GetTempPath(), $"tm-doc-runtime-phase22-{Guid.NewGuid():N}.js");
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
