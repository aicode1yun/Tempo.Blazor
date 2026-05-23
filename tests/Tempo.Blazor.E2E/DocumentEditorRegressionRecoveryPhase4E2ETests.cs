using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for Google Docs engine comment markers.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase4E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task RecoveryComments_RenderVisibleMarkerWithoutOpenPanel()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var marker = CommentMarker(page);
        await Assertions.Expect(marker).ToBeVisibleAsync();
        await Assertions.Expect(marker).ToContainTextAsync("visible comment anchor");

        var visual = await ReadCommentMarkerVisualAsync(page);
        Assert.IsTrue(visual.Width > 1, "The comment marker must have visible width.");
        Assert.IsTrue(visual.Height > 1, "The comment marker must have visible height.");
        Assert.IsFalse(string.Equals(visual.BackgroundColor, "rgba(0, 0, 0, 0)", StringComparison.Ordinal),
            "The comment marker must have a visible highlight background.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(visual.BoxShadow) || visual.BoxShadow == "none",
            "The comment marker must have a visible underline or focus decoration.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryComments_RenderVisibleMarkerWithoutOpenPanel));
    }

    [TestMethod]
    public async Task RecoveryComments_TextAndPanelSelectionStayBidirectional()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await OpenCommentsPanelAsync(page);
        var thread = page.GetByTestId("document-comment-thread")
            .Filter(new() { HasText = "The recovery baseline must show this comment in the document." })
            .First;
        await Assertions.Expect(thread).ToBeVisibleAsync();

        var marker = CommentMarker(page);
        await marker.ClickAsync();
        await Assertions.Expect(thread).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("tm-document-comment-thread--selected"));

        await thread.GetByTestId("document-comment-thread-select").ClickAsync();
        await Assertions.Expect(marker).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("tm-document-inline--comment-anchor--selected"));

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);
        await Assertions.Expect(CommentMarker(page)).ToContainTextAsync("visible comment anchor");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryComments_TextAndPanelSelectionStayBidirectional));
    }

    private static ILocator CommentMarker(IPage page)
        => page.Locator("[data-testid='document-wysiwyg-host'] .tm-document-inline--comment-anchor[data-comment-id='recovery-comment-visible']").First;

    private static async Task OpenCommentsPanelAsync(IPage page)
    {
        var reviewTab = page.GetByTestId("document-ribbon-tab-review");
        if (await reviewTab.CountAsync() > 0)
        {
            await reviewTab.ClickAsync();
        }

        await page.GetByTestId("document-open-comments").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-comments"))
            .ToHaveAttributeAsync("aria-selected", "true");
    }

    private static Task<CommentMarkerVisualProbe> ReadCommentMarkerVisualAsync(IPage page)
        => page.EvaluateAsync<CommentMarkerVisualProbe>(
            """
            () => {
                const marker = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-document-inline--comment-anchor[data-comment-id="recovery-comment-visible"]');
                const rect = marker?.getBoundingClientRect?.() || { width: 0, height: 0 };
                const style = marker ? getComputedStyle(marker) : {};
                return {
                    width: rect.width || 0,
                    height: rect.height || 0,
                    backgroundColor: style.backgroundColor || '',
                    boxShadow: style.boxShadow || ''
                };
            }
            """);

    private sealed class CommentMarkerVisualProbe
    {
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
        [JsonPropertyName("backgroundColor")] public string BackgroundColor { get; set; } = string.Empty;
        [JsonPropertyName("boxShadow")] public string BoxShadow { get; set; } = string.Empty;
    }
}
