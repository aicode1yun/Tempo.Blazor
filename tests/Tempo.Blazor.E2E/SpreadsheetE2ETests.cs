using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

[TestClass]
public class SpreadsheetE2ETests : WasmTestBase
{
    [TestMethod]
    public async Task ArrowNavigation_ScrollsGridVertically()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-grid").Nth(2);
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var before = await grid.EvaluateAsync<double>("el => el.scrollTop");

        for (var i = 0; i < 29; i++)
        {
            await grid.PressAsync("ArrowDown");
        }

        await page.WaitForFunctionAsync(
            "el => el.scrollTop > 0",
            await grid.ElementHandleAsync());

        var after = await grid.EvaluateAsync<double>("el => el.scrollTop");
        Assert.IsTrue(after > before, $"Expected spreadsheet grid to scroll down. Before: {before}, after: {after}.");
    }
}
