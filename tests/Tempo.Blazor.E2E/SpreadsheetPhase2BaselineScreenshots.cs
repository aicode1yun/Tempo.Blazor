using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Phase 2 baseline screenshots: status-bar aggregation, zoom (150% / 75%) and find/replace.
/// Run with the BaselineGeneration category against a running WASM demo.
/// </summary>
public partial class SpreadsheetBaselineScreenshots
{
    private static ILocator Phase2Grid(IPage page)
        => page.Locator("[data-testid='spreadsheet-demo'] .tm-spreadsheet-canvas-grid");

    private static ILocator Phase2Component(IPage page)
        => page.Locator("[data-testid='spreadsheet-demo']");

    private async Task SeedColumnDAsync(IPage page, params (string Ref, string Value)[] cells)
    {
        var grid = Phase2Grid(page);
        await page.WaitForFunctionAsync(
            "el => !!el.__tmSpreadsheetCanvas?.model",
            await grid.ElementHandleAsync(),
            new PageWaitForFunctionOptions { Timeout = 30000 });

        foreach (var (cellRef, value) in cells)
        {
            await grid.PressAsync("Escape");
            await page.Keyboard.PressAsync("Control+Home");
            // Navigate to the target cell via the canvas by clicking its center after focusing.
            await TypeIntoCellViaKeyboardAsync(page, grid, cellRef, value);
        }
    }

    private static async Task<CellPoint> ComputeCellPointAsync(ILocator grid, string cellRef)
        => await grid.EvaluateAsync<CellPoint>(
            @"(el, args) => {
                const refStr = String(args.ref);
                const m = refStr.match(/^([A-Za-z]+)(\d+)$/);
                let col = 0; for (const ch of m[1].toUpperCase()) col = col * 26 + (ch.charCodeAt(0) - 64);
                col -= 1; const row = Number(m[2]) - 1;
                const state = el.__tmSpreadsheetCanvas; const model = state?.model || {};
                const rowHeaderWidth = Number(model.rowHeaderWidth ?? model.RowHeaderWidth ?? 40);
                const columnHeaderHeight = Number(model.columnHeaderHeight ?? model.ColumnHeaderHeight ?? 20);
                const scrollLeft = Number(model.scrollLeft ?? model.ScrollLeft ?? 0);
                const scrollTop = Number(model.scrollTop ?? model.ScrollTop ?? 0);
                const colW = Number(model.defaultColumnWidth ?? model.DefaultColumnWidth ?? 64);
                const rowH = Number(model.defaultRowHeight ?? model.DefaultRowHeight ?? 20);
                return {
                    x: Math.round(rowHeaderWidth + col * colW - scrollLeft + colW / 2),
                    y: Math.round(columnHeaderHeight + row * rowH - scrollTop + rowH / 2)
                };
            }",
            new { @ref = cellRef });

    private static async Task ClickCellAsync(ILocator grid, string cellRef)
    {
        var point = await ComputeCellPointAsync(grid, cellRef);
        await grid.ClickAsync(new LocatorClickOptions { Force = true, Position = new() { X = point.X, Y = point.Y } });
    }

    private static async Task TypeIntoCellViaKeyboardAsync(IPage page, ILocator grid, string cellRef, string value)
    {
        await ClickCellAsync(grid, cellRef);
        await page.Keyboard.PressAsync(value[..1]);
        var editor = grid.Locator(".tm-spreadsheet-canvas-grid__editor");
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        if (value.Length > 1)
            await page.Keyboard.TypeAsync(value[1..]);
        await page.Keyboard.PressAsync("Enter");
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
    }

    private sealed class CellPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    [TestMethod]
    public async Task Baseline_StatusBar_Aggregation()
    {
        var page = await OpenSpreadsheetAsync();
        await SeedColumnDAsync(page, ("D1", "10"), ("D2", "20"), ("D3", "30"));

        var grid = Phase2Grid(page);
        // Click D1 directly (reliable), then extend the selection to D3 with Shift+ArrowDown.
        await ClickCellAsync(grid, "D1");
        await page.Keyboard.PressAsync("Shift+ArrowDown");
        await page.Keyboard.PressAsync("Shift+ArrowDown");

        var aggregations = Phase2Component(page).Locator(".tm-spreadsheet-statusbar__aggregations");
        await page.WaitForFunctionAsync(
            "el => el.textContent.includes('Sum') && el.textContent.includes('60')",
            await aggregations.ElementHandleAsync(),
            new PageWaitForFunctionOptions { Timeout = 10000 });
        await page.WaitForTimeoutAsync(300);

        await CaptureAsync(page, "statusbar-01-aggregation.png", Phase2Component(page));
    }

    [TestMethod]
    public async Task Baseline_Zoom_150()
    {
        var page = await OpenSpreadsheetAsync();
        var zoomIn = Phase2Component(page).Locator(".tm-spreadsheet-statusbar__zoom-btn").Nth(1);
        for (var i = 0; i < 5; i++) await zoomIn.ClickAsync();
        await page.WaitForTimeoutAsync(600);
        await CaptureAsync(page, "zoom-01-150.png", Phase2Component(page));
    }

    [TestMethod]
    public async Task Baseline_Zoom_50()
    {
        // The control steps in 10% increments, so 75% is not reachable; capture the 50% floor.
        var page = await OpenSpreadsheetAsync();
        var zoomOut = Phase2Component(page).Locator(".tm-spreadsheet-statusbar__zoom-btn").Nth(0);
        for (var i = 0; i < 5; i++) await zoomOut.ClickAsync();
        await page.WaitForTimeoutAsync(600);
        await CaptureAsync(page, "zoom-02-50.png", Phase2Component(page));
    }

    [TestMethod]
    public async Task Baseline_Find_Highlight()
    {
        var page = await OpenSpreadsheetAsync();
        await SeedColumnDAsync(page, ("D1", "apple"), ("D2", "apricot"), ("D3", "banana"));

        var grid = Phase2Grid(page);
        await grid.ClickAsync();
        await page.Keyboard.PressAsync("Control+f");
        var panel = Phase2Component(page).Locator(".tm-spreadsheet-find");
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await panel.Locator(".tm-spreadsheet-find__query").FillAsync("ap");
        await page.WaitForTimeoutAsync(600);
        await CaptureAsync(page, "find-01-highlight.png", Phase2Component(page));
    }

    [TestMethod]
    public async Task Baseline_Find_Replace()
    {
        var page = await OpenSpreadsheetAsync();
        await SeedColumnDAsync(page, ("D1", "alpha"));

        var grid = Phase2Grid(page);
        await grid.ClickAsync();
        await page.Keyboard.PressAsync("Control+h");
        var panel = Phase2Component(page).Locator(".tm-spreadsheet-find");
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await panel.Locator(".tm-spreadsheet-find__query").FillAsync("alpha");
        await panel.Locator(".tm-spreadsheet-find__replace").FillAsync("omega");
        await page.WaitForTimeoutAsync(400);
        await CaptureAsync(page, "find-02-replace.png", Phase2Component(page));
    }
}
