using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public sealed class DocumentEditorPhase9FindReplaceE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase9_ReplaceOne_UsesRuntimeTransactionAndUndo()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+h");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-find-input']").FillAsync("agreement");
        await page.Locator("[data-testid='document-replace-input']").FillAsync("contract");
        await page.Locator("[data-testid='document-replace-input']").DispatchEventAsync("change");

        await page.Locator("[data-testid='document-find-replace-one']").ClickAsync();

        await Assertions.Expect(body).ToContainTextAsync("contract", new() { Timeout = 3000 });

        await page.Keyboard.PressAsync("Control+z");

        await Assertions.Expect(body).ToContainTextAsync("agreement", new() { Timeout = 3000 });
    }

    [TestMethod]
    public async Task Phase9_ReplaceAll_UsesSingleRuntimeUndoBatchAndClearsSearchMarkers()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+h");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-find-input']").FillAsync("the");
        await page.Locator("[data-testid='document-replace-input']").FillAsync("tempo");
        await page.Locator("[data-testid='document-replace-input']").DispatchEventAsync("change");

        var before = await ReadEditorPlainTextAsync(page);
        Assert.IsTrue(before.Contains("the", StringComparison.OrdinalIgnoreCase), "Demo document should contain replace-all source text.");

        await page.Locator("[data-testid='document-find-replace-all']").ClickAsync();

        await page.WaitForFunctionAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const markers = window.tmDocumentEditorEngine?.getMarkers?.(instanceId) || [];
                return !markers.some(marker => {
                    const type = marker.type || marker.Type || '';
                    return type === 'search' || type === 'searchActive';
                });
            }
            """);

        var transaction = await page.EvaluateAsync<LastTransaction>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const transaction = window.tmDocumentEditorRuntime?.getLastCommandTransaction?.(instanceId) || {};
                return {
                    description: transaction.description || '',
                    operationCount: Array.isArray(transaction.operations) ? transaction.operations.length : 0
                };
            }
            """);

        Assert.AreEqual("Replace all", transaction.Description);
        Assert.IsTrue(transaction.OperationCount > 1, "Replace all should keep all replacements in one runtime transaction.");

        await page.Keyboard.PressAsync("Control+z");
        await Assertions.Expect(body).ToContainTextAsync("the", new() { Timeout = 3000 });
    }

    [TestMethod]
    public async Task Phase9_ReplaceOne_WithTrackChangesCreatesReviewableRevisions()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var body = await WaitForWysiwygBodyAsync(page);

        await page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.setTrackChangesEnabled?.(instanceId, true);
            }
            """);

        await body.ClickAsync();
        await page.Keyboard.PressAsync("Control+h");
        await Assertions.Expect(page.Locator("[data-testid='document-find-panel']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='document-find-input']").FillAsync("agreement");
        await page.Locator("[data-testid='document-replace-input']").FillAsync("contract");
        await page.Locator("[data-testid='document-replace-input']").DispatchEventAsync("change");
        await page.Locator("[data-testid='document-find-replace-one']").ClickAsync();

        await page.WaitForFunctionAsync(
            """
            () => document.querySelectorAll("[data-testid='document-wysiwyg-revision-delete']").length >= 1
                && document.querySelectorAll("[data-testid='document-wysiwyg-revision-insert']").length >= 1
            """);

        var revisionIds = await page.EvaluateAsync<string[]>(
            """
            () => {
                const deletion = document.querySelector("[data-testid='document-wysiwyg-revision-delete']")?.getAttribute('data-revision-id') || '';
                const insertion = document.querySelector("[data-testid='document-wysiwyg-revision-insert']")?.getAttribute('data-revision-id') || '';
                return [deletion, insertion].filter(Boolean);
            }
            """);

        Assert.IsTrue(revisionIds.Length >= 2, "Replace with track changes should create deletion and insertion revisions.");

        await page.EvaluateAsync(
            """
            ids => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.reviewRevision?.(instanceId, ids[0], 'Accepted');
                window.tmDocumentEditorRuntime?.reviewRevision?.(instanceId, ids[1], 'Rejected');
            }
            """,
            revisionIds);

        await page.WaitForFunctionAsync(
            """
            ids => ids.every(id => !document.querySelector(`[data-revision-id="${CSS.escape(id)}"]`))
            """,
            revisionIds);
    }

    private sealed class LastTransaction
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("operationCount")]
        public int OperationCount { get; set; }
    }
}
