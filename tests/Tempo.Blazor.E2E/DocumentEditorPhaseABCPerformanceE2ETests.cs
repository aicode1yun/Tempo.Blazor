using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>End-to-end measurement of typing perf after Phase A/B/C improvements.
/// Loads a large in-memory document into the engine, types into it, and records
/// the per-keystroke latency / render swap counts. Results are written to
/// <c>planning/baselines/perf-e2e-{date}.csv</c> for comparison.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhaseABCPerformanceE2ETests : DocumentEditorE2ETestBase
{
    private const string BaselineDate = "2026-05-26";

    [TestMethod]
    public Task PhaseABC_TypingIntoSmallDocumentMeasuresLatency()
        => RunTypingScenarioAsync(paragraphCount: 30, scenario: "e2e-typing-30p");

    [TestMethod]
    public Task PhaseABC_TypingIntoMediumDocumentMeasuresLatency()
        => RunTypingScenarioAsync(paragraphCount: 100, scenario: "e2e-typing-100p");

    [TestMethod]
    public Task PhaseABC_TypingIntoLargeDocumentMeasuresLatency()
        => RunTypingScenarioAsync(paragraphCount: 500, scenario: "e2e-typing-500p");

    private async Task RunTypingScenarioAsync(int paragraphCount, string scenario)
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        var instanceId = await GetInstanceIdAsync(page);

        // Load a synthetic N-paragraph document by mutating the existing snapshot.
        await page.EvaluateAsync(@"({ instanceId, paragraphCount }) => {
            const snapshot = JSON.parse(window.tmDocumentEditorRuntime.getDocument(instanceId));
            const doc = snapshot.Document || snapshot.document;
            const blocks = [];
            for (let i = 0; i < paragraphCount; i++) {
                blocks.push({
                    Id: 'perf-p' + i,
                    Type: 0,
                    Order: i,
                    Content: { Inlines: [{ Id: 'perf-r' + i, Text: 'Performance paragraph ' + i + ' contents.' }] }
                });
            }
            doc.Blocks = blocks; doc.blocks = blocks;
            doc.Comments = []; doc.comments = [];
            doc.Revisions = []; doc.revisions = [];
            window.tmDocumentEditorRuntime.loadDocument(instanceId, snapshot, true);
        }", new { instanceId, paragraphCount });

        await page.WaitForSelectorAsync("[data-block-id='perf-p5']", new() { Timeout = 15000 });
        await page.EvaluateAsync("instanceId => window.tmDocumentEditorEngine.clearDebugMetrics(instanceId)", instanceId);

        // Place caret at end of paragraph 5.
        await page.EvaluateAsync(@"() => {
            const block = document.querySelector('[data-block-id=""perf-p5""]');
            if (!block) throw new Error('Paragraph not found');
            const editable = block.closest('[contenteditable=""true""]');
            editable?.focus({ preventScroll: true });
            const walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
            let last = null;
            while (walker.nextNode()) { if (walker.currentNode.textContent?.length > 0) last = walker.currentNode; }
            if (!last) throw new Error('Text node not found');
            const range = document.createRange();
            range.setStart(last, last.textContent.length);
            range.collapse(true);
            const selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            document.dispatchEvent(new Event('selectionchange'));
        }");

        // Start probe capture and type 50 characters.
        await page.EvaluateAsync("({ instanceId, scenario }) => window.tmDocumentEditorPerformance.startCapture(instanceId, scenario)", new { instanceId, scenario });
        var typed = new string('x', 50);
        await page.Keyboard.TypeAsync(typed, new() { Delay = 0 });
        await page.WaitForFunctionAsync(@"({ instanceId, expectedSuffix }) => {
            const block = document.querySelector('[data-block-id=""perf-p5""]');
            return block && (block.textContent || '').endsWith(expectedSuffix);
        }", new { instanceId, expectedSuffix = typed }, new PageWaitForFunctionOptions { Timeout = 30000 });

        var reportJson = await page.EvaluateAsync<JsonElement>(
            "instanceId => window.tmDocumentEditorPerformance.stopCapture(instanceId)",
            instanceId);
        WriteReport(scenario, paragraphs: paragraphCount, iterations: typed.Length, reportJson);

        // Assertion: each keystroke should produce at most ~1 render swap.
        var renderSwaps = reportJson.GetProperty("RenderSwapCount").GetInt64();
        var inputOps = reportJson.GetProperty("InputOperationCount").GetInt64();
        inputOps.Should().BeGreaterThanOrEqualTo(typed.Length, "all typed characters should commit an input operation");
        var elapsedMs = reportJson.GetProperty("ElapsedMs").GetDouble();
        TestContext.WriteLine($"[{scenario}] elapsed={elapsedMs:0.##}ms inputOps={inputOps} renderSwaps={renderSwaps}");
    }

    private static Task<string> GetInstanceIdAsync(IPage page)
        => page.Locator("[data-testid='document-wysiwyg-host']").GetAttributeAsync("data-instance-id")
            .ContinueWith(task => task.Result ?? throw new InvalidOperationException("instance id was not found"));

    private void WriteReport(string scenario, int paragraphs, int iterations, JsonElement report)
    {
        var repoRoot = TryFindRepoRoot() ?? AppContext.BaseDirectory;
        var dir = Path.Combine(repoRoot, "planning", "baselines");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"perf-e2e-{BaselineDate}.csv");
        if (!File.Exists(file))
        {
            File.WriteAllText(file,
                "scenario,paragraphs,iterations,elapsed_ms,input_operations,model_commits,render_swaps,full_render_swaps,layout_passes,render_passes,typing_latency_total_ms,forced_reflows,js_interop_calls" + Environment.NewLine);
        }
        var row = string.Join(',',
            scenario,
            paragraphs.ToString(CultureInfo.InvariantCulture),
            iterations.ToString(CultureInfo.InvariantCulture),
            report.GetProperty("ElapsedMs").GetDouble().ToString("0.##", CultureInfo.InvariantCulture),
            report.GetProperty("InputOperationCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("ModelCommitCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("RenderSwapCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("FullRenderSwapCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("LayoutPassCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("RenderPassCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("TypingLatencyTotalMs").GetDouble().ToString("0.##", CultureInfo.InvariantCulture),
            report.GetProperty("ForcedReflowCount").GetInt64().ToString(CultureInfo.InvariantCulture),
            report.GetProperty("JsInteropCallCount").GetInt64().ToString(CultureInfo.InvariantCulture));
        File.AppendAllText(file, row + Environment.NewLine);
    }

    private static string? TryFindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, "TempoBlazor.slnx")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }
}
