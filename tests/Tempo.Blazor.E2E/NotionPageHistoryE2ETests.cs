using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests for the Notion editor page history panel.
/// Pre-seeded data: 3 versions for Page 1 ("Getting Started") — Demo User (2h ago),
/// Bob Smith (1d ago), Alice Johnson (3d ago). The first version is always "current".
/// </summary>
[TestClass]
public class NotionPageHistoryE2ETests : WasmTestBase
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<IPage> OpenNotionEditorAsync()
    {
        using var http = new HttpClient();
        try { await http.PostAsync("https://localhost:5100/api/notion/reset", null); }
        catch { /* ignore if API unavailable or cert untrusted */ }

        var context = await CreateContextAsync();
        var page    = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    /// <summary>
    /// Opens the page settings menu and clicks "Page history".
    /// Returns the visible history panel locator.
    /// </summary>
    private async Task<ILocator> OpenPageHistoryAsync(IPage page)
    {
        var trigger = page.Locator(".tm-npsm-trigger").First;
        await trigger.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await trigger.ClickAsync();

        var menu = page.Locator(".tm-npsm").First;
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var historyItem = menu.Locator(".tm-npsm__item")
                             .Filter(new LocatorFilterOptions { HasText = "Page history" })
                             .First;
        await historyItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await historyItem.ClickAsync();

        var panel = page.Locator(".tm-nph").First;
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        return panel;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Tests
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Opening Page History via the settings menu shows the history panel")]
    public async Task PageHistory_OpenPanel_ShowsVersionList()
    {
        var page  = await OpenNotionEditorAsync();
        var panel = await OpenPageHistoryAsync(page);

        Assert.IsTrue(await panel.IsVisibleAsync(), "History panel should be visible after opening");

        // Version list sidebar should be present
        var sidebar = panel.Locator(".tm-nph__sidebar").First;
        Assert.IsTrue(await sidebar.IsVisibleAsync(), "Version list sidebar should be visible");

        await TakeScreenshotAsync(page, "history_panel_open");
    }

    [TestMethod]
    [Description("The version list contains at least one entry with a date and author name")]
    public async Task PageHistory_ListHasVersions()
    {
        var page  = await OpenNotionEditorAsync();
        var panel = await OpenPageHistoryAsync(page);

        // Wait for versions to load (spinner disappears, items appear)
        var versionItems = panel.Locator(".tm-nph__version-item");
        await versionItems.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var count = await versionItems.CountAsync();
        Assert.IsTrue(count >= 1, $"Version list should have at least 1 version, got {count}");

        // Verify first item has time and author
        var firstItem  = versionItems.First;
        var timeEl     = firstItem.Locator(".tm-nph__version-time").First;
        var authorEl   = firstItem.Locator(".tm-nph__version-author-name").First;

        Assert.IsTrue(await timeEl.IsVisibleAsync(),   "First version should display a timestamp");
        Assert.IsTrue(await authorEl.IsVisibleAsync(),  "First version should display an author name");

        var authorText = await authorEl.InnerTextAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(authorText), "Author name should not be empty");

        await TakeScreenshotAsync(page, "history_version_list");
    }

    [TestMethod]
    [Description("Clicking a version in the list shows a preview of its block snapshot")]
    public async Task PageHistory_ClickVersion_ShowsPreview()
    {
        var page  = await OpenNotionEditorAsync();
        var panel = await OpenPageHistoryAsync(page);

        // Wait for version items
        var versionItems = panel.Locator(".tm-nph__version-item");
        await versionItems.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // Click the second version (index 1 = "Bob Smith", non-current)
        var secondItem = versionItems.Nth(1);
        await secondItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await secondItem.ClickAsync();

        // Preview content area should appear
        var preview = panel.Locator(".tm-nph__preview").First;
        await preview.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });
        Assert.IsTrue(await preview.IsVisibleAsync(), "Preview area should appear after selecting a version");

        // Preview should contain block elements
        var previewBlocks = preview.Locator(".tm-nph__preview-block");
        await previewBlocks.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var blockCount = await previewBlocks.CountAsync();
        Assert.IsTrue(blockCount >= 1, $"Preview should show at least 1 block, got {blockCount}");

        // Restore button should be visible in toolbar
        var restoreBtn = panel.Locator(".tm-nph__toolbar-btn--primary").First;
        Assert.IsTrue(await restoreBtn.IsVisibleAsync(), "Restore button should be visible after selecting a version");

        await TakeScreenshotAsync(page, "history_version_preview");
    }

    [TestMethod]
    [Description("Clicking Restore on a non-current version shows confirm dialog then closes the panel")]
    public async Task PageHistory_RestoreVersion_AddsAsNewHead()
    {
        var page  = await OpenNotionEditorAsync();
        var panel = await OpenPageHistoryAsync(page);

        // Select the second version (non-current — Restore is enabled for it)
        var versionItems = panel.Locator(".tm-nph__version-item");
        await versionItems.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await versionItems.Nth(1).ClickAsync();

        // Wait for the preview toolbar
        var restoreBtn = panel.Locator(".tm-nph__toolbar-btn--primary").First;
        await restoreBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000 });

        // Click Restore → confirm dialog appears
        await restoreBtn.ClickAsync();
        var confirmDialog = page.Locator(".tm-nph-confirm").First;
        await confirmDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await confirmDialog.IsVisibleAsync(), "Restore confirm dialog should appear");

        await TakeScreenshotAsync(page, "history_restore_confirm");

        // Confirm the restore — panel should close
        var okBtn = confirmDialog.Locator(".tm-nph-confirm__ok").First;
        await okBtn.ClickAsync();
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });
        Assert.IsFalse(await panel.IsVisibleAsync(), "History panel should close after confirming restore");

        await TakeScreenshotAsync(page, "history_restored");
    }

    [TestMethod]
    [Description("Clicking the X button in the history panel closes it")]
    public async Task PageHistory_ClosePanel()
    {
        var page  = await OpenNotionEditorAsync();
        var panel = await OpenPageHistoryAsync(page);

        var closeBtn = panel.Locator(".tm-nph__close-btn").First;
        await closeBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await closeBtn.ClickAsync();

        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
        Assert.IsFalse(await panel.IsVisibleAsync(), "History panel should be closed after clicking X");

        await TakeScreenshotAsync(page, "history_closed");
    }
}
