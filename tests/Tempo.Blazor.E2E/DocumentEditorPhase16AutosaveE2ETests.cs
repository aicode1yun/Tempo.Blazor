using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end checkpoints for phase 16 autosave, pending actions, retries, and beforeunload guard.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorPhase16AutosaveE2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase16_Autosave_ShowsWaitingSavingAndSynchronizedStatus()
    {
        var page = await OpenDocumentEditorWithQueryAsync("autosaveMs=700", width: 1440, height: 900);
        await DelayNextSaveAsync(page, 900);

        await EditorTypeAsync(page, $" phase16-autosave-{DateTimeOffset.UtcNow:HHmmssfff}");

        await Assertions.Expect(page.GetByTestId("document-pending-status"))
            .ToContainTextAsync("Autosave pending", new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-pending-status"))
            .ToContainTextAsync("Saving", new() { Timeout = 5000 });
        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync("Autosaved", new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-pending-status"))
            .ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase16_TypingDuringSave_TriggersSecondSave()
    {
        var page = await OpenDocumentEditorWithQueryAsync("autosaveMs=500", width: 1440, height: 900);
        var saveCount = 0;
        await page.RouteAsync("**/api/document-editor/documents/**", async route =>
        {
            if (route.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                saveCount++;
                if (saveCount == 1)
                {
                    await page.WaitForTimeoutAsync(900);
                }
            }

            await route.ContinueAsync();
        });

        await EditorTypeAsync(page, $" phase16-first-{DateTimeOffset.UtcNow:HHmmssfff}");
        await Assertions.Expect(page.GetByTestId("document-pending-status"))
            .ToContainTextAsync("Saving", new() { Timeout = 6000 });
        await EditorTypeAsync(page, $" phase16-second-{DateTimeOffset.UtcNow:HHmmssfff}");

        await page.WaitForFunctionAsync(
            """
            () => !document.querySelector('[data-testid="document-pending-status"]')
                && document.querySelector('[data-testid="document-save-message"]')?.textContent?.includes('Autosaved')
            """,
            options: new PageWaitForFunctionOptions { Timeout = 15000 });
        Assert.IsTrue(saveCount >= 2, "Typing while a save is in flight should queue a second save.");
    }

    [TestMethod]
    public async Task Phase16_ProviderErrorRetry_ThenSuccess()
    {
        var page = await OpenDocumentEditorWithQueryAsync("autosaveMs=30000", width: 1440, height: 900);
        var saveCount = 0;

        await EditorTypeAsync(page, $" phase16-retry-{DateTimeOffset.UtcNow:HHmmssfff}");
        await WaitForDirtyStateAsync(page, expectedDirty: true);

        await page.RouteAsync("**/api/document-editor/documents/**", async route =>
        {
            if (route.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                saveCount++;
                if (saveCount == 1)
                {
                    await route.FulfillAsync(new()
                    {
                        Status = 200,
                        ContentType = "application/json",
                        Body = """{"success":false,"errorMessage":"Phase 16 save failed","errorKind":1}"""
                    });
                    return;
                }
            }

            await route.ContinueAsync();
        });

        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync("Phase 16 save failed", new() { Timeout = 10000 });
        await Assertions.Expect(page.GetByTestId("document-save-retry"))
            .ToBeVisibleAsync(new() { Timeout = 5000 });

        await page.GetByTestId("document-save-retry").ClickAsync();

        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });
        Assert.IsTrue(saveCount >= 2, "Retry should issue another provider save.");
    }

    [TestMethod]
    public async Task Phase16_BeforeUnloadGuard_DebugStateTracksPendingWork()
    {
        var page = await OpenDocumentEditorWithQueryAsync("autosaveMs=30000", width: 1440, height: 900);

        Assert.IsFalse(await ReadBeforeUnloadGuardActiveAsync(page));
        await EditorTypeAsync(page, $" phase16-guard-{DateTimeOffset.UtcNow:HHmmssfff}");
        await WaitForDirtyStateAsync(page, expectedDirty: true);

        Assert.IsTrue(await ReadBeforeUnloadGuardActiveAsync(page));
        await page.GetByTestId("document-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });

        await page.WaitForFunctionAsync(
            """
            () => window.tmDocumentEditor?.getBeforeUnloadGuardState?.().active === false
            """,
            options: new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    [TestMethod]
    public async Task Phase16_ManualSave_StillPersistsAndClearsDirtyState()
    {
        var page = await OpenDocumentEditorWithQueryAsync("autosaveMs=30000", width: 1440, height: 900);
        var marker = $" phase16-manual-{DateTimeOffset.UtcNow:HHmmssfff}";

        await EditorTypeAsync(page, marker);
        await WaitForDirtyStateAsync(page, expectedDirty: true);
        await page.GetByTestId("document-save").ClickAsync();

        await Assertions.Expect(page.GetByTestId("document-save-message"))
            .ToContainTextAsync("Saved", new() { Timeout = 10000 });
        await WaitForDirtyStateAsync(page, expectedDirty: false);

        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await WaitForDocumentEditorReadyAsync(page);
        await Assertions.Expect(page.GetByTestId("document-wysiwyg-host"))
            .ToContainTextAsync(marker, new() { Timeout = 10000 });
    }

    private async Task<IPage> OpenDocumentEditorWithQueryAsync(string query, int width, int height)
    {
        var context = await CreateContextAsync();
        await InstallDocumentEditorClientStateIsolationAsync(context);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        var separator = string.IsNullOrWhiteSpace(query) ? string.Empty : "&";
        await page.GotoAsync($"{BaseUrl}/document-editor?{query}{separator}renderEngine=Legacy", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });
        await WaitForDocumentEditorReadyAsync(page);
        await ResetDocumentEditorTransientClientStateAsync(page);
        return page;
    }

    private static Task DelayNextSaveAsync(IPage page, int delayMs)
    {
        var delayed = false;
        return page.RouteAsync("**/api/document-editor/documents/**", async route =>
        {
            if (!delayed && route.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                delayed = true;
                await page.WaitForTimeoutAsync(delayMs);
            }

            await route.ContinueAsync();
        });
    }

    private static Task<bool> ReadBeforeUnloadGuardActiveAsync(IPage page)
    {
        return page.EvaluateAsync<bool>(
            """
            () => !!window.tmDocumentEditor?.getBeforeUnloadGuardState?.().active
            """);
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
}
