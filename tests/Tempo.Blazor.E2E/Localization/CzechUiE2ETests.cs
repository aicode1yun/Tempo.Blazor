using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E.Localization;

/// <summary>
/// E2E for the K7 localization sweep (WASM @ 7106): loads the demo in Czech (the host reads
/// <c>tm-demo-culture</c> from localStorage at startup) and verifies swept component strings —
/// the widget selector title and the localized widget-category names — render in Czech.
/// Screenshot lands in <c>__screenshots__/localization/</c> for UX review.
/// </summary>
[TestClass]
public class CzechUiE2ETests : WasmTestBase
{
    [TestMethod]
    [TestCategory("WASM")]
    public async Task Dashboard_InCzech_ShowsLocalizedWidgetSelectorAndCategories()
    {
        var context = await CreateContextAsync();
        // The WASM host applies the culture stored here before the app renders.
        await context.AddInitScriptAsync("window.localStorage.setItem('tm-demo-culture','cs');");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await page.GotoAsync($"{BaseUrl}/dashboard", new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });
        await WaitForAppReadyAsync(page);

        // Enter edit mode → open the widget selector.
        await page.GetByTestId("dashboard-edit").ClickAsync();
        await page.GetByTestId("dashboard-add-widget").ClickAsync();

        var modal = page.Locator(".tm-widget-selector-modal");
        await modal.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

        // Swept, now-localized strings render in Czech.
        await Assertions.Expect(modal).ToContainTextAsync("Přidat widgety",
            new LocatorAssertionsToContainTextOptions { Timeout = 10000 });   // TmWidgetSelector_Title
        await Assertions.Expect(modal).ToContainTextAsync("Všechny widgety");  // TmWidgetSelector_AllWidgets
        await Assertions.Expect(modal).ToContainTextAsync("Analýzy a KPI");    // TmDashboard_Category_Analytics

        await SaveScreenshotAsync(page, "czech-widget-selector");
    }

    private static async Task SaveScreenshotAsync(IPage page, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot().FullName, "tests", "Tempo.Blazor.E2E", "__screenshots__", "localization");
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(dir, $"{fileName}.png"), FullPage = true });
    }

    private static DirectoryInfo FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx"))) return directory;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
