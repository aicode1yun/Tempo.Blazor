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

    [TestMethod]
    public async Task ArrowNavigation_ScrollsGridHorizontally()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-grid").Nth(2);
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var before = await grid.EvaluateAsync<double>("el => el.scrollLeft");

        for (var i = 0; i < 45; i++)
        {
            await grid.PressAsync("ArrowRight");
        }

        await page.WaitForFunctionAsync(
            "el => el.scrollLeft > 0",
            await grid.ElementHandleAsync());

        var after = await grid.EvaluateAsync<double>("el => el.scrollLeft");
        Assert.IsTrue(after > before, $"Expected spreadsheet grid to scroll right. Before: {before}, after: {after}.");

        await grid.Locator("[title='AT1']").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 10000
        });
    }

    [TestMethod]
    public async Task CanvasRenderer_RendersNonBlankCanvasAndScrollsHorizontally()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var canvas = grid.Locator("canvas").First;
        await page.WaitForFunctionAsync(
            @"canvas => {
                if (!canvas || canvas.width === 0 || canvas.height === 0) return false;
                const ctx = canvas.getContext('2d');
                const data = ctx.getImageData(0, 0, Math.min(canvas.width, 64), Math.min(canvas.height, 64)).data;
                for (let i = 0; i < data.length; i += 4) {
                    if (data[i + 3] !== 0 && (data[i] !== 255 || data[i + 1] !== 255 || data[i + 2] !== 255)) return true;
                }
                return false;
            }",
            await canvas.ElementHandleAsync());

        await grid.ClickAsync();
        for (var i = 0; i < 45; i++)
        {
            await grid.PressAsync("ArrowRight");
        }

        await page.WaitForFunctionAsync(
            "el => el.scrollLeft > 0",
            await grid.ElementHandleAsync());
    }

    [TestMethod]
    public async Task CanvasRenderer_DoubleClickStartsCellEdit()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var canvas = grid.Locator("canvas").First;
        await page.WaitForFunctionAsync(
            "canvas => canvas && canvas.width > 0 && canvas.height > 0",
            await canvas.ElementHandleAsync());

        await grid.DblClickAsync(new LocatorDblClickOptions
        {
            Force = true,
            Position = new() { X = 120, Y = 56 }
        });

        var editor = grid.Locator(".tm-spreadsheet-canvas-grid__editor");
        await editor.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
        var isFocused = await editor.EvaluateAsync<bool>("el => document.activeElement === el");
        Assert.IsTrue(isFocused, "Expected double-clicked canvas cell editor to receive focus.");
    }

    [TestMethod]
    public async Task BenchmarkPage_RunsCanvasBenchmark()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet-benchmark");
        await WaitForAppReadyAsync(page);

        await page.GetByTestId("spreadsheet-benchmark-run-canvas").ClickAsync();

        var result = page.GetByTestId("spreadsheet-benchmark-result-row").First;
        await result.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30000
        });

        var text = await result.InnerTextAsync();
        Assert.IsTrue(text.Contains("Canvas"), $"Expected a canvas benchmark result row, got: {text}");
    }
}
