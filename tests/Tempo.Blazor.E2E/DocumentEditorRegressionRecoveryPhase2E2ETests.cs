using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for immediate typing, Space, Enter, and fast key input.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase2E2ETests : DocumentEditorE2ETestBase
{
    private const string TypingBlockId = "recovery-selection-paragraph";

    [TestMethod]
    public async Task Recovery_SpaceKey_IsVisibleBeforeNextCharacter()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickDocumentEditorBlockOffsetAsync(page, TypingBlockId, 0);
        var original = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);

        await page.Keyboard.PressAsync("A");
        var afterA = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        var caretAfterA = await ReadDocumentEditorCaretRectAsync(page);

        await page.Keyboard.PressAsync("Space");
        var afterSpace = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        var caretAfterSpace = await ReadDocumentEditorCaretRectAsync(page);

        await page.Keyboard.PressAsync("B");
        var afterB = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);

        Assert.IsTrue(afterA.StartsWith("A" + original, StringComparison.Ordinal), $"Expected A to be visible immediately, got '{afterA}'.");
        Assert.IsTrue(afterSpace.StartsWith("A " + original, StringComparison.Ordinal), $"Expected Space to be visible before next character, got '{afterSpace}'.");
        Assert.IsTrue(caretAfterSpace.X > caretAfterA.X, $"Expected caret to move right after Space. Before: {caretAfterA.X}, after: {caretAfterSpace.X}.");
        Assert.IsTrue(afterB.StartsWith("A B" + original, StringComparison.Ordinal), $"Expected final text to contain 'A B', got '{afterB}'.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_SpaceKey_IsVisibleBeforeNextCharacter));
    }

    [TestMethod]
    public async Task Recovery_EnterKey_SplitsParagraphBeforeNextCharacter()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var original = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        const string prefix = "Select this inline";
        Assert.IsTrue(original.StartsWith(prefix, StringComparison.Ordinal), "Recovery typing block text changed unexpectedly.");
        var suffix = original[prefix.Length..];

        await ClickDocumentEditorBlockOffsetAsync(page, TypingBlockId, prefix.Length);
        await page.Keyboard.PressAsync("Enter");

        var paragraphsAfterEnter = await ReadVisibleParagraphsAsync(page);
        var splitIndex = Array.FindIndex(paragraphsAfterEnter, item => item.Id == TypingBlockId && item.Text == prefix);
        Assert.IsTrue(splitIndex >= 0, $"Expected first split paragraph to contain '{prefix}'.");
        Assert.IsTrue(splitIndex + 1 < paragraphsAfterEnter.Length, "Expected Enter to create a following paragraph immediately.");
        Assert.IsTrue(paragraphsAfterEnter[splitIndex + 1].Text.StartsWith(suffix, StringComparison.Ordinal),
            $"Expected suffix '{suffix}' to move to the second paragraph, got '{paragraphsAfterEnter[splitIndex + 1].Text}'.");

        await page.Keyboard.PressAsync("Z");
        var paragraphsAfterType = await ReadVisibleParagraphsAsync(page);
        Assert.IsTrue(paragraphsAfterType[splitIndex + 1].Text.StartsWith("Z" + suffix, StringComparison.Ordinal),
            $"Expected next typed character to appear at the start of the new paragraph, got '{paragraphsAfterType[splitIndex + 1].Text}'.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_EnterKey_SplitsParagraphBeforeNextCharacter));
    }

    [TestMethod]
    public async Task Recovery_FastTyping_IsNotBatchedIntoLargeChunks()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await ClickDocumentEditorBlockOffsetAsync(page, TypingBlockId, 0);
        var original = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        var text = "rychly text bez davkovani ";
        var typed = string.Empty;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            await page.Keyboard.TypeAsync(ch.ToString(), new() { Delay = 0 });
            typed += ch;
            if ((i + 1) % 5 == 0)
            {
                var current = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
                Assert.IsTrue(current.StartsWith(typed + original, StringComparison.Ordinal),
                    $"Expected visible prefix after {i + 1} chars to be '{typed}', got '{current}'.");
            }
        }

        var latencySamples = new List<DocumentEditorKeystrokeLatencyProbe>();
        foreach (var key in "latencyok")
        {
            latencySamples.Add(await MeasureKeystrokeLatencyAsync(page, key.ToString()));
        }

        var visibleLatencies = latencySamples
            .Select(sample => sample.VisibleTextChangeLatencyMs ?? double.PositiveInfinity)
            .Order()
            .ToArray();
        var medianLatency = Median(visibleLatencies);
        var p95Latency = PercentileNearestRank(visibleLatencies, 0.95);
        Assert.IsTrue(medianLatency is >= 0 and < 50, $"Expected median key-to-DOM latency below 50 ms, got {medianLatency}.");
        Assert.IsTrue(p95Latency is >= 0 and < 100, $"Expected p95 key-to-DOM latency below 100 ms, got {p95Latency}.");
        Assert.IsTrue(latencySamples.All(sample => sample.FullRenderCount == 0), "Single-character typing must not trigger a full document render.");

        await page.EvaluateAsync(
            """
            () => {
                if (!window.tmDocumentEditorTestProbe) throw new Error('tmDocumentEditorTestProbe is not available.');
                window.tmDocumentEditorTestProbe.start('[data-testid="document-wysiwyg-host"]');
            }
            """);
        var longTypingText = string.Concat(Enumerable.Repeat("abcdefghij", 10));
        await page.Keyboard.TypeAsync(longTypingText, new() { Delay = 0 });
        await page.WaitForTimeoutAsync(120);
        var longTypingProbe = await page.EvaluateAsync<DocumentEditorKeyHoldBatchProbe>(
            "() => window.tmDocumentEditorTestProbe.snapshot()");
        Assert.AreEqual(0, longTypingProbe.FullRenderCount, "Typing 100 characters must not trigger a full document render.");

        var held = await HoldKeyAndMeasureBatchesAsync(page, "x", holdMilliseconds: 2000);
        var afterHold = await ReadDocumentEditorBlockTextAsync(page, TypingBlockId);
        var insertedXCount = afterHold.Count(ch => ch == 'x');
        Assert.IsTrue(insertedXCount >= 10, $"Expected held-key repeat to insert progressively, got {insertedXCount} x characters.");
        Assert.IsTrue(held.MutationBatchCount >= 5, $"Expected multiple DOM mutation batches during held key, got {held.MutationBatchCount}.");
        Assert.AreEqual(0, held.FullRenderCount, "Held-key typing must not trigger full document renders.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(Recovery_FastTyping_IsNotBatchedIntoLargeChunks));
    }

    [TestMethod]
    public async Task DemoDocument_TrailingSpace_IsVisuallyRepresentedImmediately()
    {
        var page = await OpenDocumentEditorAsync(width: 890, height: 460);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var original = await ReadDocumentEditorBlockTextAsync(page, "contract-intro");
        await ClickDocumentEditorBlockOffsetAsync(page, "contract-intro", original.Length);

        var whiteSpace = await page.EvaluateAsync<string>(
            """
            () => getComputedStyle(document.querySelector('[data-testid="document-wysiwyg-host"] p.tm-wysiwyg-block[data-block-id="contract-intro"]')).whiteSpace
            """);
        Assert.AreEqual("break-spaces", whiteSpace, "Paragraph text must preserve trailing spaces visually, not only in textContent.");

        await page.Keyboard.PressAsync("f");
        var afterF = await ReadDocumentEditorBlockTextAsync(page, "contract-intro");
        var caretAfterF = await ReadDocumentEditorCaretRectAsync(page);
        await page.Keyboard.PressAsync("Space");
        var afterSpace = await ReadDocumentEditorBlockTextAsync(page, "contract-intro");
        var caretAfterSpace = await ReadDocumentEditorCaretRectAsync(page);
        await page.Keyboard.PressAsync("x");
        var afterX = await ReadDocumentEditorBlockTextAsync(page, "contract-intro");

        Assert.IsTrue(afterF.EndsWith("f", StringComparison.Ordinal), $"Expected typed f at the end, got '{afterF}'.");
        Assert.IsTrue(afterSpace.EndsWith("f ", StringComparison.Ordinal), $"Expected trailing space to be present immediately, got '{afterSpace}'.");
        Assert.IsTrue(caretAfterSpace.X > caretAfterF.X, $"Expected caret to move after trailing Space. Before: {caretAfterF.X}, after: {caretAfterSpace.X}.");
        Assert.IsTrue(afterX.EndsWith("f x", StringComparison.Ordinal), $"Expected next character after the preserved space, got '{afterX}'.");
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(DemoDocument_TrailingSpace_IsVisuallyRepresentedImmediately));
    }

    [TestMethod]
    public async Task DemoDocument_EnterAtParagraphEnd_MovesCaretToInsertedEmptyParagraph()
    {
        var page = await OpenDocumentEditorAsync(width: 890, height: 460);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var original = await ReadDocumentEditorBlockTextAsync(page, "contract-intro");
        await ClickDocumentEditorBlockOffsetAsync(page, "contract-intro", original.Length);
        await page.Keyboard.PressAsync("Enter");

        var paragraphsAfterEnter = await ReadVisibleParagraphsAsync(page);
        var introIndex = Array.FindIndex(paragraphsAfterEnter, item => item.Id == "contract-intro");
        Assert.IsTrue(introIndex >= 0, "Expected contract intro paragraph to remain visible.");
        Assert.IsTrue(introIndex + 1 < paragraphsAfterEnter.Length, "Expected Enter at paragraph end to create a following empty paragraph.");
        var inserted = paragraphsAfterEnter[introIndex + 1];
        Assert.AreEqual(string.Empty, inserted.Text, "The inserted paragraph after Enter at the end should be empty before typing.");

        var probeAfterEnter = await CaptureStrictFrameProbeAsync(page, "after-enter");
        Assert.AreEqual(inserted.Id, probeAfterEnter.Selection.BlockId, "Caret must move into the inserted empty paragraph immediately.");
        Assert.AreEqual(0, probeAfterEnter.Selection.Offset, "Caret in the inserted empty paragraph should be at offset 0.");

        await page.Keyboard.TypeAsync("aaa", new() { Delay = 0 });
        var paragraphsAfterTyping = await ReadVisibleParagraphsAsync(page);
        var insertedAfterTyping = paragraphsAfterTyping.First(item => item.Id == inserted.Id);
        Assert.AreEqual("aaa", insertedAfterTyping.Text, "Typing after Enter must land in the inserted paragraph, not back in the previous line.");
        Assert.AreEqual(original, await ReadDocumentEditorBlockTextAsync(page, "contract-intro"));
        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(DemoDocument_EnterAtParagraphEnd_MovesCaretToInsertedEmptyParagraph));
    }

    private static Task<VisibleParagraphProbe[]> ReadVisibleParagraphsAsync(IPage page)
        => page.EvaluateAsync<VisibleParagraphProbe[]>(
            """
            () => Array.from(document.querySelectorAll('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-block[data-block-id]'))
                .filter(node => node.tagName.toLowerCase() === 'p')
                .map(node => ({
                    id: node.getAttribute('data-block-id') || '',
                    text: node.textContent || ''
                }))
            """);

    private sealed class VisibleParagraphProbe
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return double.PositiveInfinity;
        }

        var middle = values.Length / 2;
        return values.Length % 2 == 1
            ? values[middle]
            : (values[middle - 1] + values[middle]) / 2;
    }

    private static double PercentileNearestRank(double[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return double.PositiveInfinity;
        }

        var index = (int)Math.Ceiling(percentile * values.Length) - 1;
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }
}
