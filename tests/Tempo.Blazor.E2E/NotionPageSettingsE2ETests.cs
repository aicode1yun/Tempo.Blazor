using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E tests for the Notion editor page settings menu (⋮ button):
/// view toggles (full width, small text, lock), export, and mark-all-read.
/// </summary>
[TestClass]
public class NotionPageSettingsE2ETests : WasmTestBase
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
    /// Opens a browser context that accepts downloads (needed for export tests).
    /// </summary>
    private async Task<IPage> OpenNotionEditorWithDownloadsAsync()
    {
        using var http = new HttpClient();
        try { await http.PostAsync("https://localhost:5100/api/notion/reset", null); }
        catch { }

        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize      = new ViewportSize { Width = 1280, Height = 720 },
            Locale            = "en-US",
            IgnoreHTTPSErrors = true,
            AcceptDownloads   = true
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/notion-editor");
        await WaitForAppReadyAsync(page);
        await page.WaitForSelectorAsync(".tm-notion-editor", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(2000);
        return page;
    }

    /// <summary>Clicks the ⋮ trigger and returns the open settings menu locator.</summary>
    private async Task<ILocator> OpenPageSettingsAsync(IPage page)
    {
        var trigger = page.Locator(".tm-npsm-trigger").First;
        await trigger.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await trigger.ClickAsync();

        var menu = page.Locator(".tm-npsm").First;
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        return menu;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Open menu
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking the ⋮ button opens the page settings menu")]
    public async Task PageSettings_SettingsBtn_OpensMenu()
    {
        var page = await OpenNotionEditorAsync();
        var menu = await OpenPageSettingsAsync(page);
        Assert.IsTrue(await menu.IsVisibleAsync(), "Page settings menu should be visible after clicking trigger");

        await TakeScreenshotAsync(page, "pagesettings_menu_open");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  View toggles
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Toggling Full Width adds tm-notion-page--full-width to the page article")]
    public async Task PageSettings_FullWidth_Toggle_ChangesLayout()
    {
        var page = await OpenNotionEditorAsync();
        var menu = await OpenPageSettingsAsync(page);

        var fullWidthItem = menu.Locator(".tm-npsm__item")
                               .Filter(new LocatorFilterOptions { HasText = "Full width" })
                               .First;
        await fullWidthItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await fullWidthItem.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var count = await page.Locator(".tm-notion-page--full-width").CountAsync();
        Assert.IsTrue(count > 0, "Page should have tm-notion-page--full-width class after toggling Full Width");

        await TakeScreenshotAsync(page, "pagesettings_fullwidth_on");
    }

    [TestMethod]
    [Description("Toggling Small Text adds tm-notion-page--small-text to the page article")]
    public async Task PageSettings_SmallText_Toggle_ChangesFont()
    {
        var page = await OpenNotionEditorAsync();
        var menu = await OpenPageSettingsAsync(page);

        var smallTextItem = menu.Locator(".tm-npsm__item")
                               .Filter(new LocatorFilterOptions { HasText = "Small text" })
                               .First;
        await smallTextItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await smallTextItem.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var count = await page.Locator(".tm-notion-page--small-text").CountAsync();
        Assert.IsTrue(count > 0, "Page should have tm-notion-page--small-text class after toggling Small Text");

        await TakeScreenshotAsync(page, "pagesettings_smalltext_on");
    }

    [TestMethod]
    [Description("Toggling Lock Page adds tm-notion-page--readonly to the page article")]
    public async Task PageSettings_LockPage_DisablesEditing()
    {
        var page = await OpenNotionEditorAsync();
        var menu = await OpenPageSettingsAsync(page);

        var lockItem = menu.Locator(".tm-npsm__item")
                          .Filter(new LocatorFilterOptions { HasText = "Lock page" })
                          .First;
        await lockItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await lockItem.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var count = await page.Locator(".tm-notion-page--readonly").CountAsync();
        Assert.IsTrue(count > 0, "Page should have tm-notion-page--readonly class after locking");

        await TakeScreenshotAsync(page, "pagesettings_locked");
    }

    [TestMethod]
    [Description("Clicking Lock Page twice (lock then unlock) removes the --readonly class")]
    public async Task PageSettings_LockPage_Unlock_EnablesEditing()
    {
        var page = await OpenNotionEditorAsync();
        var menu = await OpenPageSettingsAsync(page);

        var lockItem = menu.Locator(".tm-npsm__item")
                          .Filter(new LocatorFilterOptions { HasText = "Lock page" })
                          .First;
        await lockItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // First click — lock
        await lockItem.ClickAsync();
        await page.WaitForTimeoutAsync(800);
        Assert.IsTrue(await page.Locator(".tm-notion-page--readonly").CountAsync() > 0,
            "Page should be locked after first toggle");

        // Second click in the same open menu — unlock
        await lockItem.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        var count = await page.Locator(".tm-notion-page--readonly").CountAsync();
        Assert.AreEqual(0, count, "Page should not have --readonly class after unlocking");

        await TakeScreenshotAsync(page, "pagesettings_unlocked");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Export
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking Export → Markdown triggers a .md file download")]
    public async Task PageSettings_ExportMarkdown_DownloadsFile()
    {
        var page = await OpenNotionEditorWithDownloadsAsync();
        var menu = await OpenPageSettingsAsync(page);

        // Open export sub-panel
        var exportItem = menu.Locator(".tm-npsm__item")
                            .Filter(new LocatorFilterOptions { HasText = "Export" })
                            .First;
        await exportItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await exportItem.ClickAsync();

        // Wait for the Markdown sub-item
        var mdItem = menu.Locator(".tm-npsm__sub-item")
                        .Filter(new LocatorFilterOptions { HasText = "Export as Markdown" })
                        .First;
        await mdItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        // Click and intercept the download
        var download = await page.RunAndWaitForDownloadAsync(async () => await mdItem.ClickAsync());
        Assert.IsNotNull(download, "A file download should start");
        Assert.IsTrue(download.SuggestedFilename.EndsWith(".md", StringComparison.OrdinalIgnoreCase),
            $"Downloaded file should be .md, got '{download.SuggestedFilename}'");

        await TakeScreenshotAsync(page, "pagesettings_export_md");
    }

    [TestMethod]
    [Description("Clicking Export → HTML triggers a .html file download")]
    public async Task PageSettings_ExportHtml_DownloadsFile()
    {
        var page = await OpenNotionEditorWithDownloadsAsync();
        var menu = await OpenPageSettingsAsync(page);

        var exportItem = menu.Locator(".tm-npsm__item")
                            .Filter(new LocatorFilterOptions { HasText = "Export" })
                            .First;
        await exportItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await exportItem.ClickAsync();

        var htmlItem = menu.Locator(".tm-npsm__sub-item")
                          .Filter(new LocatorFilterOptions { HasText = "Export as HTML" })
                          .First;
        await htmlItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var download = await page.RunAndWaitForDownloadAsync(async () => await htmlItem.ClickAsync());
        Assert.IsNotNull(download, "A file download should start");
        Assert.IsTrue(download.SuggestedFilename.EndsWith(".html", StringComparison.OrdinalIgnoreCase),
            $"Downloaded file should be .html, got '{download.SuggestedFilename}'");

        await TakeScreenshotAsync(page, "pagesettings_export_html");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Mark all comments as read
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("Clicking 'Mark all comments as read' shows a success toast")]
    public async Task PageSettings_MarkAllCommentsAsRead_ShowsToast()
    {
        var page = await OpenNotionEditorAsync();
        var menu = await OpenPageSettingsAsync(page);

        var markReadItem = menu.Locator(".tm-npsm__item")
                              .Filter(new LocatorFilterOptions { HasText = "Mark all comments as read" })
                              .First;
        await markReadItem.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await markReadItem.ClickAsync();

        // Toast appears after menu closes
        var toast = page.Locator(".tm-npsm__toast").First;
        await toast.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.IsTrue(await toast.IsVisibleAsync(), "Success toast should appear");

        var toastText = await toast.InnerTextAsync();
        Assert.IsTrue(
            toastText.Contains("All comments marked as read", StringComparison.OrdinalIgnoreCase),
            $"Toast should say 'All comments marked as read', got '{toastText}'");

        await TakeScreenshotAsync(page, "pagesettings_mark_all_read");
    }
}
