using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for the JS-owned WYSIWYG save/autosave boundary.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeSaveBoundaryTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase8_ExplicitSaveUsesJsCanonicalSnapshotAndMarksRuntimeSaved()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase8-save-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EditorTypeAsync(page, marker);
        await WaitForDirtyStateAsync(page, expectedDirty: true);

        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Saved");
        await WaitForDirtyStateAsync(page, expectedDirty: false);

        var dirtyState = await ReadDirtyStateAsync(page);
        Assert.IsFalse(dirtyState.IsDirty);
        Assert.IsFalse(string.IsNullOrWhiteSpace(dirtyState.LastSavedMarker), "The JS runtime should receive a provider save marker.");

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await WaitForDocumentEditorReadyAsync(page);
        await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).ToContainTextAsync(marker);
    }

    [TestMethod]
    public async Task Phase8_SaveFailureKeepsRuntimeDirty()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase8-fail-{DateTimeOffset.UtcNow:HHmmssfff}";

        await page.RouteAsync("**/api/document-editor/documents/**", async route =>
        {
            if (route.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = """{"success":false,"errorMessage":"Phase 8 save failed"}"""
                });
                return;
            }

            await route.ContinueAsync();
        });

        await EditorTypeAsync(page, marker);
        await WaitForDirtyStateAsync(page, expectedDirty: true);

        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Phase 8 save failed");

        var dirtyState = await ReadDirtyStateAsync(page);
        Assert.IsTrue(dirtyState.IsDirty, "Failed save must not acknowledge the JS runtime dirty state.");
    }

    [TestMethod]
    public async Task Phase8_AutosaveUsesJsCanonicalSnapshotAndMarksRuntimeSaved()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await page.GotoAsync($"{BaseUrl}/document-editor?autosaveMs=2000", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);

        var marker = $"phase8-autosave-{DateTimeOffset.UtcNow:HHmmssfff}";
        await EditorTypeAsync(page, marker);
        await WaitForDirtyStateAsync(page, expectedDirty: true);

        await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Autosaved", new()
        {
            Timeout = 10000
        });
        await WaitForDirtyStateAsync(page, expectedDirty: false);

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await WaitForDocumentEditorReadyAsync(page);
        await Assertions.Expect(page.GetByTestId("document-wysiwyg-host")).ToContainTextAsync(marker);
    }

    private static Task WaitForDirtyStateAsync(IPage page, bool expectedDirty)
    {
        return page.WaitForFunctionAsync(
            """
            expected => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getDirtyState?.(instanceId);
                return !!state && !!(state.IsDirty ?? state.isDirty) === expected;
            }
            """,
            expectedDirty);
    }

    private static Task<DirtyState> ReadDirtyStateAsync(IPage page)
    {
        return page.EvaluateAsync<DirtyState>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getDirtyState?.(instanceId) || {};
                return {
                    isDirty: !!(state.IsDirty ?? state.isDirty),
                    lastSavedMarker: String(state.LastSavedMarker ?? state.lastSavedMarker ?? '')
                };
            }
            """);
    }

    private sealed class DirtyState
    {
        [JsonPropertyName("isDirty")]
        public bool IsDirty { get; set; }

        [JsonPropertyName("lastSavedMarker")]
        public string? LastSavedMarker { get; set; }
    }
}
