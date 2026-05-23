using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for typing performance and render-path budgets.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase10E2ETests : DocumentEditorE2ETestBase
{
    private const string TypingBlockId = "recovery-selection-paragraph";

    [TestMethod]
    public async Task RecoveryTyping_PerformanceStatsProveNoFullRenderPath()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickDocumentEditorBlockOffsetAsync(page, TypingBlockId, 0);
        var original = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        await ClearRuntimeStatsAsync(page);

        const string text = "phase10 rychle psani ";
        await page.Keyboard.TypeAsync(text, new() { Delay = 0 });
        await page.WaitForTimeoutAsync(650);
        var current = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        var stats = await ReadRuntimeStatsAsync(page);

        Assert.IsTrue(current.StartsWith(text + original, StringComparison.Ordinal), $"Typed text should be visible progressively, got '{current}'.");
        Assert.AreEqual(0, stats.FullRenderCount, "Plain typing must not use the full document render path.");
        Assert.IsTrue(stats.InputDomApplyCount >= text.Length, $"Expected one live DOM apply per typed character. Stats: {stats}");
        Assert.IsTrue(stats.PartialRenderCount >= text.Length, $"Expected partial render accounting for live typing. Stats: {stats}");
        Assert.IsTrue(stats.TextNodePatchCount >= text.Length, $"Expected text-node/block patch accounting for typing. Stats: {stats}");
        Assert.IsTrue(stats.InputOperationCount >= text.Length, $"Expected operation stats for every typed character. Stats: {stats}");
        Assert.IsTrue(stats.MedianKeyToDomMs is >= 0 and < 50, $"Median key-to-DOM latency budget exceeded. Stats: {stats}");
        Assert.IsTrue(stats.P95KeyToDomMs is >= 0 and < 100, $"P95 key-to-DOM latency budget exceeded. Stats: {stats}");
        Assert.IsTrue(stats.BlazorInteropCallCount <= 4, $"Typing should coalesce boundary/dirty interop, not call Blazor per key. Stats: {stats}");
        Assert.IsTrue(stats.TypingFlushCount <= 2, $"Typing patches should be debounced for side-panel/autosave sync. Stats: {stats}");
        Assert.IsTrue(stats.MaxTypingBatchSize >= text.Length, $"Typing boundary patches should batch while DOM remains immediate. Stats: {stats}");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryTyping_PerformanceStatsProveNoFullRenderPath));
    }

    [TestMethod]
    public async Task RecoveryTyping_SpaceEnterAndNextCharacterUsePartialPatches()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        const string prefix = "Select this inline";
        var original = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        Assert.IsTrue(original.StartsWith(prefix, StringComparison.Ordinal), "Recovery typing block text changed unexpectedly.");
        var suffix = original[prefix.Length..];

        await ClickDocumentEditorBlockOffsetAsync(page, TypingBlockId, prefix.Length);
        await ClearRuntimeStatsAsync(page);

        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("Enter");
        await page.Keyboard.PressAsync("Z");
        await page.WaitForTimeoutAsync(650);
        var paragraphs = await ReadVisibleParagraphsAsync(page);
        var stats = await ReadRuntimeStatsAsync(page);
        var splitIndex = Array.FindIndex(paragraphs, item => item.Id == TypingBlockId && item.Text == prefix + " ");

        Assert.IsTrue(splitIndex >= 0, "Space must be visible in the original paragraph before Enter finishes.");
        Assert.IsTrue(splitIndex + 1 < paragraphs.Length && paragraphs[splitIndex + 1].Text.StartsWith("Z" + suffix, StringComparison.Ordinal),
            $"The next character after Enter must appear in the new paragraph. Stats: {stats}");
        Assert.AreEqual(0, stats.FullRenderCount, "Space/Enter typing must not fall back to full document render.");
        Assert.IsTrue(stats.TextNodePatchCount >= 2, $"Space and Z should be text patches. Stats: {stats}");
        Assert.IsTrue(stats.BlockPatchCount >= 1, $"Enter should be a block patch. Stats: {stats}");
        Assert.IsTrue(stats.InputDomApplyCount >= 3, $"Expected immediate DOM applies for Space, Enter and Z. Stats: {stats}");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryTyping_SpaceEnterAndNextCharacterUsePartialPatches));
    }

    [TestMethod]
    public async Task RecoveryTyping_HoldKeyPaintsProgressivelyWhileInteropIsThrottled()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickDocumentEditorBlockOffsetAsync(page, TypingBlockId, 0);
        await ClearRuntimeStatsAsync(page);
        var held = await HoldKeyAndMeasureBatchesAsync(page, "x", holdMilliseconds: 900);
        await page.WaitForTimeoutAsync(650);
        var stats = await ReadRuntimeStatsAsync(page);

        Assert.AreEqual(0, held.FullRenderCount, "Held-key typing must not trigger full renders.");
        Assert.IsTrue(held.MutationBatchCount >= 5, $"Held-key text should appear over several DOM mutation batches, got {held.MutationBatchCount}.");
        Assert.IsTrue(stats.InputDomApplyCount >= 5, $"Held key should apply repeated DOM patches. Stats: {stats}");
        Assert.AreEqual(0, stats.FullRenderCount, "Held-key typing must stay off the full render path.");
        Assert.IsTrue(stats.BlazorInteropCallCount <= 4, $"Held-key typing should throttle Blazor-side sync. Stats: {stats}");
        Assert.IsTrue(stats.TypingFlushCount <= 2, $"Held-key typing should not flush side-panel/autosave sync at key rate. Stats: {stats}");
        Assert.IsTrue(stats.P95KeyToDomMs < 100, $"Held-key visible latency budget exceeded. Stats: {stats}");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryTyping_HoldKeyPaintsProgressivelyWhileInteropIsThrottled));
    }

    private static Task ClearRuntimeStatsAsync(IPage page)
        => page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorEngine?.clearDebugMetrics?.(instanceId);
            }
            """);

    private static Task<Phase10RuntimeStats> ReadRuntimeStatsAsync(IPage page)
        => page.EvaluateAsync<Phase10RuntimeStats>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorEngine?.getDebugMetrics?.(instanceId) || {};
            }
            """);

    private static Task<VisibleParagraphProbe[]> ReadVisibleParagraphsAsync(IPage page)
        => page.EvaluateAsync<VisibleParagraphProbe[]>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-block[data-block-id]'))
                .filter(node => node.tagName.toLowerCase() === 'p' && !node.closest('.tm-wysiwyg-page--virtual'))
                .map(node => ({
                    id: node.getAttribute('data-block-id') || '',
                    text: node.textContent || ''
                }))
            """);

    private sealed class Phase10RuntimeStats
    {
        [JsonPropertyName("KeyDownCount")] public int KeyDownCount { get; set; }
        [JsonPropertyName("BeforeInputCount")] public int BeforeInputCount { get; set; }
        [JsonPropertyName("InputDomApplyCount")] public int InputDomApplyCount { get; set; }
        [JsonPropertyName("FullRenderCount")] public int FullRenderCount { get; set; }
        [JsonPropertyName("PartialRenderCount")] public int PartialRenderCount { get; set; }
        [JsonPropertyName("TextNodePatchCount")] public int TextNodePatchCount { get; set; }
        [JsonPropertyName("BlockPatchCount")] public int BlockPatchCount { get; set; }
        [JsonPropertyName("MarkerOverlayPatchCount")] public int MarkerOverlayPatchCount { get; set; }
        [JsonPropertyName("ObjectOverlayPatchCount")] public int ObjectOverlayPatchCount { get; set; }
        [JsonPropertyName("SelectionNotifyCount")] public int SelectionNotifyCount { get; set; }
        [JsonPropertyName("BlazorInteropCallCount")] public int BlazorInteropCallCount { get; set; }
        [JsonPropertyName("TypingFlushCount")] public int TypingFlushCount { get; set; }
        [JsonPropertyName("MaxTypingBatchSize")] public int MaxTypingBatchSize { get; set; }
        [JsonPropertyName("MedianKeyToDomMs")] public double MedianKeyToDomMs { get; set; }
        [JsonPropertyName("P95KeyToDomMs")] public double P95KeyToDomMs { get; set; }
        [JsonPropertyName("InputOperationCount")] public int InputOperationCount { get; set; }

        public override string ToString()
            => $"keyDown={KeyDownCount}, beforeInput={BeforeInputCount}, domApply={InputDomApplyCount}, full={FullRenderCount}, partial={PartialRenderCount}, text={TextNodePatchCount}, block={BlockPatchCount}, interop={BlazorInteropCallCount}, flush={TypingFlushCount}, maxBatch={MaxTypingBatchSize}, median={MedianKeyToDomMs:0.##}, p95={P95KeyToDomMs:0.##}, ops={InputOperationCount}";
    }

    private sealed class VisibleParagraphProbe
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }
}
