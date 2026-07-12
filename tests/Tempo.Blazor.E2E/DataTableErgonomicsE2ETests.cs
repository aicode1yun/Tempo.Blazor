using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// E2E for TmDataTable K2 ergonomics on the Data Table demo page (WASM demo at 7106):
/// column pinning, multi-column sort, CSV export download, and validated inline editing.
/// Screenshots land in <c>__screenshots__/data-table/</c> for UX review.
/// </summary>
[TestClass]
public class DataTableErgonomicsE2ETests : WasmTestBase
{
    private const string DataTablePage = "/data-table";

    private async Task<IPage> OpenPageAsync()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1100);
        await page.GotoAsync($"{BaseUrl}{DataTablePage}",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);
        return page;
    }

    [TestMethod]
    [TestCategory("WASM")]
    public async Task DataTable_PinAndMultiSort_AndExportCsv()
    {
        var page = await OpenPageAsync();

        var section = page.Locator("[data-testid='dt-ergonomics-section']");
        await section.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await section.ScrollIntoViewIfNeededAsync();

        // Name column is pinned left by default.
        await section.Locator(".tm-col-pinned-left").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

        // Multi-column sort: plain-click one sortable header, Shift+click another → two precedence badges.
        var sortableHeaders = section.Locator("th[data-sortable='true']");
        await sortableHeaders.Nth(1).ClickAsync();
        await sortableHeaders.Nth(2).ClickAsync(new LocatorClickOptions { Modifiers = new[] { KeyboardModifier.Shift } });

        var badges = section.Locator(".tm-sort-order");
        await badges.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.AreEqual(2, await badges.CountAsync(), "Two columns should show sort-precedence badges.");
        await SaveScreenshotAsync(page, "ergonomics-pin-multisort");

        // Export CSV triggers a browser download.
        var download = await page.RunAndWaitForDownloadAsync(async () =>
        {
            await section.Locator("[data-testid='dt-export-csv']").ClickAsync();
        });
        StringAssert.EndsWith(download.SuggestedFilename, ".csv");
    }

    [TestMethod]
    [TestCategory("WASM")]
    public async Task DataTable_InlineEdit_EntersEditModeOnDoubleClick()
    {
        var page = await OpenPageAsync();

        var section = page.Locator("[data-testid='dt-inline-edit-section']");
        await section.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await section.ScrollIntoViewIfNeededAsync();

        await section.Locator("tbody tr").First.DblClickAsync();

        var editing = section.Locator("[data-testid='row-editing']");
        await editing.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
        await section.Locator("[data-testid='edit-commit']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await SaveScreenshotAsync(page, "inline-edit");
    }

    private static async Task SaveScreenshotAsync(IPage page, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot().FullName,
            "tests", "Tempo.Blazor.E2E", "__screenshots__", "data-table");
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, $"{fileName}.png"), FullPage = true });
    }

    private static DirectoryInfo FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
