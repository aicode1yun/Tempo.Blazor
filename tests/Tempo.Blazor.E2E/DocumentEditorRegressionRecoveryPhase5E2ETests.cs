using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Human-facing recovery tests for Google Docs engine revision markers.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorRegressionRecoveryPhase5E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task RecoveryRevisions_RenderVisibleInsertionAndDeletionMarkers()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        var insertion = RevisionMarker(page, "recovery-revision-insertion");
        var deletion = RevisionMarker(page, "recovery-revision-deletion");
        await Assertions.Expect(insertion).ToBeVisibleAsync();
        await Assertions.Expect(insertion).ToContainTextAsync("inserted recovery clause");
        await Assertions.Expect(deletion).ToBeVisibleAsync();
        await Assertions.Expect(deletion).ToContainTextAsync("deleted recovery clause");

        var visual = await ReadRevisionMarkerVisualAsync(page);
        Assert.IsTrue(visual.InsertionWidth > 1, "The insertion revision marker must have visible width.");
        Assert.IsFalse(string.Equals(visual.InsertionBackgroundColor, "rgba(0, 0, 0, 0)", StringComparison.Ordinal),
            "The insertion revision marker must have a visible highlight background.");
        Assert.IsTrue(visual.InsertionTextDecorationLine.Contains("underline", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(visual.InsertionBoxShadow) && visual.InsertionBoxShadow != "none"),
            "The insertion revision marker must have visible underline or focus decoration.");
        Assert.IsTrue(visual.DeletionWidth > 1, "The deletion revision marker must have visible width.");
        Assert.IsTrue(visual.DeletionTextDecorationLine.Contains("line-through", StringComparison.OrdinalIgnoreCase),
            "The deletion revision marker must render as struck-through text.");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryRevisions_RenderVisibleInsertionAndDeletionMarkers));
    }

    [TestMethod]
    public async Task RecoveryRevisions_TextAndPanelSelectionStayBidirectional()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await OpenRevisionsPanelAsync(page);
        var insertion = RevisionMarker(page, "recovery-revision-insertion");
        var insertionItem = RevisionItem(page, "recovery-revision-insertion");
        await Assertions.Expect(insertionItem).ToBeVisibleAsync();

        await insertion.ClickAsync();
        await Assertions.Expect(insertionItem).ToHaveClassAsync(new Regex("tm-document-revision-panel__item--selected"));
        await Assertions.Expect(insertionItem).ToHaveAttributeAsync("aria-current", "true");

        await insertionItem.Locator(".tm-document-revision-panel__summary").ClickAsync();
        await Assertions.Expect(insertion).ToHaveClassAsync(new Regex("tm-wysiwyg-revision--selected"));

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
        await WaitForDocumentEditorReadyAsync(page);
        await Assertions.Expect(RevisionMarker(page, "recovery-revision-insertion")).ToContainTextAsync("inserted recovery clause");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryRevisions_TextAndPanelSelectionStayBidirectional));
    }

    [TestMethod]
    public async Task RecoveryRevisions_AcceptAndRejectActionsUpdateTextAndMarkers()
    {
        var page = await OpenRecoveryDocumentAsync(width: 1440, height: 900);
        var console = GetMandatoryDocumentEditorConsoleCapture(page);

        await OpenRevisionsPanelAsync(page);

        await RevisionItem(page, "recovery-revision-insertion")
            .GetByTestId("document-revision-accept")
            .ClickAsync();
        await Assertions.Expect(RevisionMarker(page, "recovery-revision-insertion")).ToHaveCountAsync(0);

        await RevisionItem(page, "recovery-revision-deletion")
            .GetByTestId("document-revision-reject")
            .ClickAsync();
        await Assertions.Expect(RevisionMarker(page, "recovery-revision-deletion")).ToHaveCountAsync(0);

        var text = await ReadEditorPlainTextAsync(page);
        StringAssert.Contains(text, "inserted recovery clause");
        StringAssert.Contains(text, "deleted recovery clause");

        await AssertNoDocumentEditorConsoleErrorsAsync(page, console, nameof(RecoveryRevisions_AcceptAndRejectActionsUpdateTextAndMarkers));
    }

    private static ILocator RevisionMarker(IPage page, string revisionId)
        => page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-revision[data-revision-id='{revisionId}']").First;

    private static ILocator RevisionItem(IPage page, string revisionId)
        => page.Locator($"[data-testid='document-revision-item'][data-revision-id='{revisionId}']").First;

    private static async Task OpenRevisionsPanelAsync(IPage page)
    {
        var reviewTab = page.GetByTestId("document-ribbon-tab-review");
        if (await reviewTab.CountAsync() > 0)
        {
            await reviewTab.ClickAsync();
        }

        await page.GetByTestId("document-open-revisions").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-side-panel-tab-revisions"))
            .ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(page.GetByTestId("document-revision-panel")).ToBeVisibleAsync();
    }

    private static Task<RevisionMarkerVisualProbe> ReadRevisionMarkerVisualAsync(IPage page)
        => page.EvaluateAsync<RevisionMarkerVisualProbe>(
            """
            () => {
                const insertion = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-revision[data-revision-id="recovery-revision-insertion"]');
                const deletion = document.querySelector('[data-testid="document-wysiwyg-host"] .tm-wysiwyg-revision[data-revision-id="recovery-revision-deletion"]');
                const insertionRect = insertion?.getBoundingClientRect?.() || { width: 0, height: 0 };
                const deletionRect = deletion?.getBoundingClientRect?.() || { width: 0, height: 0 };
                const insertionStyle = insertion ? getComputedStyle(insertion) : {};
                const deletionStyle = deletion ? getComputedStyle(deletion) : {};
                return {
                    insertionWidth: insertionRect.width || 0,
                    insertionHeight: insertionRect.height || 0,
                    insertionBackgroundColor: insertionStyle.backgroundColor || '',
                    insertionBoxShadow: insertionStyle.boxShadow || '',
                    insertionTextDecorationLine: insertionStyle.textDecorationLine || '',
                    deletionWidth: deletionRect.width || 0,
                    deletionHeight: deletionRect.height || 0,
                    deletionBackgroundColor: deletionStyle.backgroundColor || '',
                    deletionTextDecorationLine: deletionStyle.textDecorationLine || ''
                };
            }
            """);

    private sealed class RevisionMarkerVisualProbe
    {
        [JsonPropertyName("insertionWidth")] public double InsertionWidth { get; set; }
        [JsonPropertyName("insertionHeight")] public double InsertionHeight { get; set; }
        [JsonPropertyName("insertionBackgroundColor")] public string InsertionBackgroundColor { get; set; } = string.Empty;
        [JsonPropertyName("insertionBoxShadow")] public string InsertionBoxShadow { get; set; } = string.Empty;
        [JsonPropertyName("insertionTextDecorationLine")] public string InsertionTextDecorationLine { get; set; } = string.Empty;
        [JsonPropertyName("deletionWidth")] public double DeletionWidth { get; set; }
        [JsonPropertyName("deletionHeight")] public double DeletionHeight { get; set; }
        [JsonPropertyName("deletionBackgroundColor")] public string DeletionBackgroundColor { get; set; } = string.Empty;
        [JsonPropertyName("deletionTextDecorationLine")] public string DeletionTextDecorationLine { get; set; } = string.Empty;
    }
}
