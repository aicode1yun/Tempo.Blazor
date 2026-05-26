using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor.Performance;

/// <summary>Phase A — tests for <c>window.tmDocumentEditorPerformance</c>. Verifies the
/// public probe contract that benchmarks and the Blazor host rely on.</summary>
public sealed class WysiwygPerformanceProbeJavaScriptTests
{
    [Fact]
    public async Task PhaseA_ProbeGlobalIsDefinedWithRequiredMethods()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-globals",
            """
            assert.ok(probe, 'tmDocumentEditorPerformance global must exist');
            assert.strictEqual(typeof probe.startCapture, 'function');
            assert.strictEqual(typeof probe.stopCapture, 'function');
            assert.strictEqual(typeof probe.isCapturing, 'function');
            assert.strictEqual(typeof probe.clearAll, 'function');
            assert.strictEqual(typeof probe.noteJsInteropCall, 'function');
            assert.strictEqual(typeof probe.getActiveCaptures, 'function');
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseA_StartCaptureWithoutInstanceIdThrows()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-requires-instance-id",
            """
            let threw = false;
            try { probe.startCapture('', 'no-id'); }
            catch (e) { threw = true; }
            assert.ok(threw, 'startCapture must throw when instanceId is missing');
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseA_StopCaptureWithoutActiveCaptureReturnsNull()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-no-active-capture",
            """
            const report = probe.stopCapture('never-started');
            assert.strictEqual(report, null, 'stopCapture on unknown instance must return null');
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseA_StartAndStopProducesReportWithDeltaFields()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-start-stop",
            """
            const root = createRoot();
            engine.create(root, { InstanceId: 'perf-1' }, null);
            engine.loadDocument('perf-1', {
                Document: {
                    DocumentId: 'perf-1-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: 'Hello' }] } }
                    ]
                }
            });

            probe.startCapture('perf-1', 'noop');
            assert.ok(probe.isCapturing('perf-1'), 'isCapturing must be true after startCapture');
            const active = probe.getActiveCaptures();
            assert.strictEqual(active.length, 1, JSON.stringify(active));
            assert.strictEqual(active[0], 'perf-1');
            const report = probe.stopCapture('perf-1');
            assert.ok(report, 'stopCapture must return a report');
            assert.strictEqual(report.InstanceId, 'perf-1');
            assert.strictEqual(report.Label, 'noop');
            assert.ok(typeof report.ElapsedMs === 'number');
            assert.ok(report.ElapsedMs >= 0);
            assert.strictEqual(typeof report.ForcedReflowCount, 'number');
            assert.strictEqual(typeof report.KeyDownCount, 'number');
            assert.strictEqual(typeof report.FullRenderCount, 'number');
            assert.strictEqual(typeof report.RenderSwapCount, 'number');
            assert.strictEqual(typeof report.ModelCommitCount, 'number');
            assert.strictEqual(probe.isCapturing('perf-1'), false);
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseA_InteropCallCountIsTrackedWithinCapture()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-interop-count",
            """
            const root = createRoot();
            engine.create(root, { InstanceId: 'perf-interop' }, null);

            probe.noteJsInteropCall(); // before capture — must not count
            probe.startCapture('perf-interop', 'interop');
            probe.noteJsInteropCall();
            probe.noteJsInteropCall(3);
            const report = probe.stopCapture('perf-interop');
            assert.strictEqual(report.JsInteropCallCount, 4, JSON.stringify(report));
            console.log('OK');
            """);

        result.ShouldPass();
    }

    [Fact]
    public async Task PhaseA_TypingScenarioInsertCharsRecordsInputOperationCount()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-typing-delta",
            """
            const root = createRoot();
            engine.create(root, { InstanceId: 'perf-typing' }, null);
            engine.loadDocument('perf-typing', {
                Document: {
                    DocumentId: 'perf-typing-doc',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r1', Text: '' }] } }
                    ]
                }
            });
            engine.clearDebugMetrics('perf-typing');

            const api = engine.operations;
            probe.startCapture('perf-typing', 'insert-5-chars');
            for (let i = 0; i < 5; i++) {
                const insert = api.createOperation(api.types.InsertText,
                    { target: { blockId: 'p1', offset: i }, text: 'a' },
                    { source: 'perf-typing' });
                engine.applyRemoteOperationBatch('perf-typing', { Operations: [insert] });
            }
            const report = probe.stopCapture('perf-typing');
            assert.ok(report.InputOperationCount >= 5,
                'InputOperationCount must be at least 5, got ' + JSON.stringify(report));
            assert.ok(report.ModelCommitCount >= 5,
                'ModelCommitCount must be at least 5, got ' + JSON.stringify(report));
            console.log(JSON.stringify({ InputOperationCount: report.InputOperationCount, ModelCommitCount: report.ModelCommitCount }));
            """);

        result.ShouldPass();
        var payload = result.GetJsonPayload();
        if (result.NodeAvailable)
        {
            payload.Should().NotBeNull("typing scenario must emit a payload");
        }
    }

    [Fact]
    public async Task PhaseA_ClearAllResetsActiveCaptures()
    {
        var result = await PerformanceScenarioRunner.RunAsync(
            "probe-clear-all",
            """
            const root = createRoot();
            engine.create(root, { InstanceId: 'perf-clear' }, null);
            probe.startCapture('perf-clear', 'clear-test');
            probe.noteJsInteropCall(5);
            probe.clearAll();
            assert.strictEqual(probe.getActiveCaptures().length, 0);
            assert.strictEqual(probe.isCapturing('perf-clear'), false);
            const report = probe.stopCapture('perf-clear');
            assert.strictEqual(report, null, 'clearAll must drop the capture so stopCapture returns null');
            console.log('OK');
            """);

        result.ShouldPass();
    }
}
