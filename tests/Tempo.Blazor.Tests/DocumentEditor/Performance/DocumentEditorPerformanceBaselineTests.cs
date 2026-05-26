using System.Text.Json;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor.Performance;

/// <summary>Phase A — performance baseline scenarios. Runs canonical typing / paste / load
/// scenarios through the engine in a Node.js vm sandbox and records aggregated metrics
/// into <c>planning/baselines/perf-{date}.csv</c>. Designed to detect regressions in
/// follow-up Phase B/C work.
///
/// In CI without Node available the tests pass silently — exactly like the rest of the
/// JavaScript-driven test suite.</summary>
public sealed class DocumentEditorPerformanceBaselineTests
{
    private static readonly string BaselineDate = "2026-05-26";

    [Theory]
    [InlineData(10, 50)]
    [InlineData(100, 50)]
    [InlineData(500, 50)]
    public async Task PhaseA_Baseline_TypingIntoDocument(int paragraphCount, int iterations)
    {
        var nodeScript = $$"""
            const paragraphCount = {{paragraphCount}};
            const iterations = {{iterations}};
            const root = createRoot();
            engine.create(root, { InstanceId: 'baseline-typing' }, null);
            const blocks = [];
            for (let i = 0; i < paragraphCount; i++) {
                blocks.push({
                    Id: 'p' + i,
                    Type: 'Paragraph',
                    Content: { Type: 'Paragraph', Inlines: [{ Id: 'r' + i, Text: 'Paragraph ' + i }] }
                });
            }
            engine.loadDocument('baseline-typing', { Document: { DocumentId: 'baseline-typing-doc', Blocks: blocks } });
            engine.clearDebugMetrics('baseline-typing');

            const api = engine.operations;
            const targetBlockId = blocks[Math.floor(blocks.length / 2)].Id;
            probe.startCapture('baseline-typing', 'typing-' + paragraphCount + 'p');
            for (let i = 0; i < iterations; i++) {
                const op = api.createOperation(api.types.InsertText,
                    { target: { blockId: targetBlockId, offset: i }, text: 'x' },
                    { source: 'baseline-typing' });
                engine.applyRemoteOperationBatch('baseline-typing', { Operations: [op] });
            }
            const report = probe.stopCapture('baseline-typing');
            console.log(JSON.stringify(report));
            """;

        var result = await PerformanceScenarioRunner.RunAsync(
            $"baseline-typing-{paragraphCount}p",
            nodeScript);
        result.ShouldPass();

        var payload = result.GetJsonPayload();
        if (!result.NodeAvailable || payload is null) return;

        var report = JsonSerializer.Deserialize<JsonElement>(payload);
        PerformanceBaselineRecorder.AppendRow(BaselineDate, new BaselineRow(
            Scenario: $"typing-{paragraphCount}p",
            Paragraphs: paragraphCount,
            Iterations: iterations,
            ElapsedMs: report.GetProperty("ElapsedMs").GetDouble(),
            InputOperations: report.GetProperty("InputOperationCount").GetInt64(),
            ModelCommits: report.GetProperty("ModelCommitCount").GetInt64(),
            RenderSwaps: report.GetProperty("RenderSwapCount").GetInt64(),
            FullRenderSwaps: report.GetProperty("FullRenderSwapCount").GetInt64(),
            LayoutPasses: report.GetProperty("LayoutPassCount").GetInt64(),
            RenderPasses: report.GetProperty("RenderPassCount").GetInt64(),
            TypingLatencyTotalMs: report.GetProperty("TypingLatencyTotalMs").GetDouble(),
            ForcedReflows: report.GetProperty("ForcedReflowCount").GetInt64(),
            JsInteropCalls: report.GetProperty("JsInteropCallCount").GetInt64()));

        report.GetProperty("InputOperationCount").GetInt64().Should().BeGreaterThanOrEqualTo(iterations);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task PhaseA_Baseline_LoadDocument(int paragraphCount)
    {
        var nodeScript = $$"""
            const paragraphCount = {{paragraphCount}};
            const blocks = [];
            for (let i = 0; i < paragraphCount; i++) {
                blocks.push({
                    Id: 'p' + i,
                    Type: 'Paragraph',
                    Content: { Type: 'Paragraph', Inlines: [{ Id: 'r' + i, Text: 'Paragraph ' + i + ' contents of varying length to mimic a real document' }] }
                });
            }
            const root = createRoot();
            engine.create(root, { InstanceId: 'baseline-load' }, null);
            engine.clearDebugMetrics('baseline-load');
            probe.startCapture('baseline-load', 'load-' + paragraphCount + 'p');
            engine.loadDocument('baseline-load', { Document: { DocumentId: 'baseline-load-doc', Blocks: blocks } });
            const report = probe.stopCapture('baseline-load');
            console.log(JSON.stringify(report));
            """;

        var result = await PerformanceScenarioRunner.RunAsync(
            $"baseline-load-{paragraphCount}p",
            nodeScript);
        result.ShouldPass();

        var payload = result.GetJsonPayload();
        if (!result.NodeAvailable || payload is null) return;

        var report = JsonSerializer.Deserialize<JsonElement>(payload);
        PerformanceBaselineRecorder.AppendRow(BaselineDate, new BaselineRow(
            Scenario: $"load-{paragraphCount}p",
            Paragraphs: paragraphCount,
            Iterations: 1,
            ElapsedMs: report.GetProperty("ElapsedMs").GetDouble(),
            InputOperations: report.GetProperty("InputOperationCount").GetInt64(),
            ModelCommits: report.GetProperty("ModelCommitCount").GetInt64(),
            RenderSwaps: report.GetProperty("RenderSwapCount").GetInt64(),
            FullRenderSwaps: report.GetProperty("FullRenderSwapCount").GetInt64(),
            LayoutPasses: report.GetProperty("LayoutPassCount").GetInt64(),
            RenderPasses: report.GetProperty("RenderPassCount").GetInt64(),
            TypingLatencyTotalMs: report.GetProperty("TypingLatencyTotalMs").GetDouble(),
            ForcedReflows: report.GetProperty("ForcedReflowCount").GetInt64(),
            JsInteropCalls: report.GetProperty("JsInteropCallCount").GetInt64()));
    }

    [Fact]
    public async Task PhaseA_Baseline_BatchInsertSimulatesPaste()
    {
        var nodeScript = """
            const root = createRoot();
            engine.create(root, { InstanceId: 'baseline-paste' }, null);
            engine.loadDocument('baseline-paste', {
                Document: {
                    DocumentId: 'baseline-paste-doc',
                    Blocks: [
                        { Id: 'p0', Type: 'Paragraph', Content: { Type: 'Paragraph', Inlines: [{ Id: 'r0', Text: 'Start' }] } }
                    ]
                }
            });
            engine.clearDebugMetrics('baseline-paste');

            const api = engine.operations;
            const operations = [];
            for (let i = 0; i < 100; i++) {
                operations.push(api.createOperation(api.types.InsertText,
                    { target: { blockId: 'p0', offset: 5 + i }, text: 'X' },
                    { source: 'baseline-paste' }));
            }

            probe.startCapture('baseline-paste', 'batch-insert-100');
            engine.applyRemoteOperationBatch('baseline-paste', { Operations: operations });
            const report = probe.stopCapture('baseline-paste');
            console.log(JSON.stringify(report));
            """;

        var result = await PerformanceScenarioRunner.RunAsync("baseline-paste-100", nodeScript);
        result.ShouldPass();

        var payload = result.GetJsonPayload();
        if (!result.NodeAvailable || payload is null) return;

        var report = JsonSerializer.Deserialize<JsonElement>(payload);
        PerformanceBaselineRecorder.AppendRow(BaselineDate, new BaselineRow(
            Scenario: "batch-insert-100",
            Paragraphs: 1,
            Iterations: 100,
            ElapsedMs: report.GetProperty("ElapsedMs").GetDouble(),
            InputOperations: report.GetProperty("InputOperationCount").GetInt64(),
            ModelCommits: report.GetProperty("ModelCommitCount").GetInt64(),
            RenderSwaps: report.GetProperty("RenderSwapCount").GetInt64(),
            FullRenderSwaps: report.GetProperty("FullRenderSwapCount").GetInt64(),
            LayoutPasses: report.GetProperty("LayoutPassCount").GetInt64(),
            RenderPasses: report.GetProperty("RenderPassCount").GetInt64(),
            TypingLatencyTotalMs: report.GetProperty("TypingLatencyTotalMs").GetDouble(),
            ForcedReflows: report.GetProperty("ForcedReflowCount").GetInt64(),
            JsInteropCalls: report.GetProperty("JsInteropCallCount").GetInt64()));
    }
}
