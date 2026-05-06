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
    public async Task CanvasRenderer_RapidArrowDownKeepsSelectionMonotonicWhileScrolling()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var previousRow = 1;
        for (var i = 0; i < 80; i++)
        {
            await grid.PressAsync("ArrowDown");
            var activeRef = await grid.EvaluateAsync<string>(
                "el => el.__tmSpreadsheetCanvas?.model?.activeCellRef || el.__tmSpreadsheetCanvas?.model?.ActiveCellRef || ''");
            var row = ParseRow(activeRef);
            Assert.IsTrue(row >= previousRow, $"Expected active row to stay monotonic. Previous: {previousRow}, current: {row}, ref: {activeRef}.");
            previousRow = row;
        }

        var scrollTop = await grid.EvaluateAsync<double>("el => el.scrollTop");
        Assert.IsTrue(scrollTop > 0, $"Expected ArrowDown navigation to scroll canvas grid. scrollTop: {scrollTop}.");
        Assert.IsTrue(previousRow >= 70, $"Expected rapid ArrowDown navigation to reach a later row. Last row: {previousRow}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_RapidArrowRightKeepsSelectionMonotonicWhileScrolling()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var previousColumn = 1;
        for (var i = 0; i < 40; i++)
        {
            await grid.PressAsync("ArrowRight");
            var activeRef = await grid.EvaluateAsync<string>(
                "el => el.__tmSpreadsheetCanvas?.model?.activeCellRef || el.__tmSpreadsheetCanvas?.model?.ActiveCellRef || ''");
            var column = ParseColumn(activeRef);
            Assert.IsTrue(column >= previousColumn, $"Expected active column to stay monotonic. Previous: {previousColumn}, current: {column}, ref: {activeRef}.");
            previousColumn = column;
        }

        var scrollLeft = await grid.EvaluateAsync<double>("el => el.scrollLeft");
        Assert.IsTrue(scrollLeft > 0, $"Expected ArrowRight navigation to scroll canvas grid. scrollLeft: {scrollLeft}.");
        Assert.IsTrue(previousColumn >= 35, $"Expected rapid ArrowRight navigation to reach a later column. Last column: {previousColumn}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_RapidArrowUpKeepsSelectionMonotonicWhileScrolling()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        for (var i = 0; i < 80; i++)
        {
            await grid.PressAsync("ArrowDown");
        }

        var startRow = ParseRow(await GetCanvasActiveRefAsync(grid));
        var previousRow = startRow;
        for (var i = 0; i < 45; i++)
        {
            await grid.PressAsync("ArrowUp");
            var activeRef = await GetCanvasActiveRefAsync(grid);
            var row = ParseRow(activeRef);
            Assert.IsTrue(row <= previousRow, $"Expected active row to decrease monotonically. Previous: {previousRow}, current: {row}, ref: {activeRef}.");
            previousRow = row;
        }

        Assert.IsTrue(previousRow < startRow, $"Expected rapid ArrowUp navigation to reach an earlier row. Start row: {startRow}, last row: {previousRow}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_RapidArrowLeftKeepsSelectionMonotonicWhileScrolling()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        for (var i = 0; i < 40; i++)
        {
            await grid.PressAsync("ArrowRight");
        }

        var startColumn = ParseColumn(await GetCanvasActiveRefAsync(grid));
        var previousColumn = startColumn;
        for (var i = 0; i < 28; i++)
        {
            await grid.PressAsync("ArrowLeft");
            var activeRef = await GetCanvasActiveRefAsync(grid);
            var column = ParseColumn(activeRef);
            Assert.IsTrue(column <= previousColumn, $"Expected active column to decrease monotonically. Previous: {previousColumn}, current: {column}, ref: {activeRef}.");
            previousColumn = column;
        }

        Assert.IsTrue(previousColumn < startColumn, $"Expected rapid ArrowLeft navigation to reach an earlier column. Start column: {startColumn}, last column: {previousColumn}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_KeyboardNavigationUsesSelectionOverlay()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var canvasCount = await grid.Locator("canvas").CountAsync();
        Assert.IsTrue(canvasCount >= 3, $"Expected canvas renderer to use content, header, and selection canvases. Count: {canvasCount}.");

        await grid.ClickAsync();
        var before = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).selectionRedrawCount");

        await grid.PressAsync("ArrowDown");
        await grid.PressAsync("ArrowRight");

        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).selectionRedrawCount > {before}",
            await grid.ElementHandleAsync());
    }

    [TestMethod]
    public async Task CanvasRenderer_LayersTrackDevicePixelRatioAndResize()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 960, Height = 540 },
            DeviceScaleFactor = 2,
            Locale = "en-US",
            IgnoreHTTPSErrors = true
        });

        try
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync($"{BaseUrl}/spreadsheet");
            await WaitForAppReadyAsync(page);

            var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
            await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await page.WaitForFunctionAsync(
                @"el => [...el.querySelectorAll('canvas')].length >= 3
                    && [...el.querySelectorAll('canvas')].every(canvas =>
                        canvas.width === Math.round(el.clientWidth * window.devicePixelRatio)
                        && canvas.height === Math.round(el.clientHeight * window.devicePixelRatio))",
                await grid.ElementHandleAsync());

            await page.SetViewportSizeAsync(720, 420);
            await page.WaitForFunctionAsync(
                @"el => [...el.querySelectorAll('canvas')].length >= 3
                    && [...el.querySelectorAll('canvas')].every(canvas =>
                        canvas.width === Math.round(el.clientWidth * window.devicePixelRatio)
                        && canvas.height === Math.round(el.clientHeight * window.devicePixelRatio))",
                await grid.ElementHandleAsync());
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [TestMethod]
    public async Task CanvasRenderer_UnchangedLocalEditDoesNotRedrawContent()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.DblClickAsync(new LocatorDblClickOptions
        {
            Force = true,
            Position = new() { X = 120, Y = 56 }
        });

        var editor = grid.Locator(".tm-spreadsheet-canvas-grid__editor");
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var redrawsBeforeCommit = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).redrawCount");
        await editor.PressAsync("Enter");
        await page.WaitForTimeoutAsync(250);
        var redrawsAfterCommit = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).redrawCount");

        Assert.AreEqual(redrawsBeforeCommit, redrawsAfterCommit, "Expected an unchanged local edit to close without a content redraw.");
    }

    [TestMethod]
    public async Task CanvasRenderer_SmallScrollUsesBitmapShiftAndLargeScrollFallsBack()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            await grid.ElementHandleAsync());

        var shiftsBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftCount");
        await grid.EvaluateAsync("el => { el.scrollTop += 40; }");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftCount > {shiftsBefore}",
            await grid.ElementHandleAsync());

        var lastDy = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).lastBitmapShiftDy");
        Assert.AreNotEqual(0, lastDy, "Expected bitmap shift to record a vertical delta.");

        var exposedStripHasInk = await grid.EvaluateAsync<bool>(
            @"el => {
                const canvas = el.querySelector('.tm-spreadsheet-canvas-grid__canvas--content');
                const ctx = canvas.getContext('2d');
                const dpr = window.devicePixelRatio || 1;
                const rowHeader = 40 * dpr;
                const stripHeight = Math.min(48 * dpr, canvas.height - 20 * dpr);
                const y = Math.max(20 * dpr, canvas.height - stripHeight);
                const data = ctx.getImageData(rowHeader, y, Math.min(180 * dpr, canvas.width - rowHeader), stripHeight).data;
                for (let i = 0; i < data.length; i += 4) {
                    if (data[i + 3] !== 0 && (data[i] < 245 || data[i + 1] < 245 || data[i + 2] < 245)) return true;
                }
                return false;
            }");
        Assert.IsTrue(exposedStripHasInk, "Expected the newly exposed bitmap-shift strip to be redrawn with grid/content pixels.");

        shiftsBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftCount");
        await grid.EvaluateAsync("el => { el.scrollLeft += 40; }");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftCount > {shiftsBefore}",
            await grid.ElementHandleAsync());

        var lastDx = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).lastBitmapShiftDx");
        Assert.AreNotEqual(0, lastDx, "Expected bitmap shift to record a horizontal delta.");

        var fallbacksBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftFallbackCount");
        await grid.EvaluateAsync("el => { el.scrollTop += el.clientHeight; }");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftFallbackCount > {fallbacksBefore}",
            await grid.ElementHandleAsync());
    }

    [TestMethod]
    public async Task CanvasRenderer_DragSelectionUpdatesRangeLocally()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var box = await grid.BoundingBoxAsync();
        Assert.IsNotNull(box);

        var startX = (int)Math.Round(box.X + 92);
        var startY = (int)Math.Round(box.Y + 36);
        var endX = (int)Math.Round(box.X + 230);
        var endY = (int)Math.Round(box.Y + 86);

        await grid.DispatchEventAsync("pointerdown", new
        {
            clientX = startX,
            clientY = startY,
            button = 0,
            buttons = 1,
            pointerId = 7,
            pointerType = "mouse"
        });

        for (var i = 1; i <= 8; i++)
        {
            await grid.DispatchEventAsync("pointermove", new
            {
                clientX = startX + (endX - startX) * i / 8,
                clientY = startY + (endY - startY) * i / 8,
                button = 0,
                buttons = 1,
                pointerId = 7,
                pointerType = "mouse"
            });
        }

        await grid.DispatchEventAsync("pointerup", new
        {
            clientX = endX,
            clientY = endY,
            button = 0,
            buttons = 0,
            pointerId = 7,
            pointerType = "mouse"
        });

        await page.WaitForFunctionAsync(
            @"el => {
                const selection = el.__tmSpreadsheetCanvas?.model?.selection || el.__tmSpreadsheetCanvas?.model?.Selection;
                if (!selection) return false;
                const startRow = selection.startRow ?? selection.StartRow;
                const startCol = selection.startCol ?? selection.StartCol;
                const endRow = selection.endRow ?? selection.EndRow;
                const endCol = selection.endCol ?? selection.EndCol;
                return endRow > startRow && endCol > startCol;
            }",
            await grid.ElementHandleAsync());

        var selectionRedraws = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).selectionRedrawCount");
        Assert.IsTrue(selectionRedraws > 0, $"Expected drag selection to redraw the selection overlay. Count: {selectionRedraws}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_DragSelectionAutoscrollsDown()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var box = await grid.BoundingBoxAsync();
        Assert.IsNotNull(box);

        var startX = (int)Math.Round(box.X + 92);
        var startY = (int)Math.Round(box.Y + 42);
        var edgeY = (int)Math.Round(box.Y + box.Height - 2);

        await grid.DispatchEventAsync("pointerdown", new
        {
            clientX = startX,
            clientY = startY,
            button = 0,
            buttons = 1,
            pointerId = 8,
            pointerType = "mouse"
        });

        await grid.DispatchEventAsync("pointermove", new
        {
            clientX = startX,
            clientY = edgeY,
            button = 0,
            buttons = 1,
            pointerId = 8,
            pointerType = "mouse"
        });

        await page.WaitForFunctionAsync(
            @"el => {
                const metrics = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                const selection = el.__tmSpreadsheetCanvas?.model?.selection || el.__tmSpreadsheetCanvas?.model?.Selection;
                const startRow = selection?.startRow ?? selection?.StartRow ?? 0;
                const endRow = selection?.endRow ?? selection?.EndRow ?? 0;
                return el.scrollTop > 0 && metrics.dragAutoscrollFrames > 0 && endRow > startRow;
            }",
            await grid.ElementHandleAsync());

        await grid.DispatchEventAsync("pointerup", new
        {
            clientX = startX,
            clientY = edgeY,
            button = 0,
            buttons = 0,
            pointerId = 8,
            pointerType = "mouse"
        });
    }

    [TestMethod]
    public async Task CanvasRenderer_DragSelectionAutoscrollsRight()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var box = await grid.BoundingBoxAsync();
        Assert.IsNotNull(box);

        var startX = (int)Math.Round(box.X + 92);
        var startY = (int)Math.Round(box.Y + 42);
        var edgeX = (int)Math.Round(box.X + box.Width - 2);

        await grid.DispatchEventAsync("pointerdown", new
        {
            clientX = startX,
            clientY = startY,
            button = 0,
            buttons = 1,
            pointerId = 9,
            pointerType = "mouse"
        });

        await grid.DispatchEventAsync("pointermove", new
        {
            clientX = edgeX,
            clientY = startY,
            button = 0,
            buttons = 1,
            pointerId = 9,
            pointerType = "mouse"
        });

        await page.WaitForFunctionAsync(
            @"el => {
                const metrics = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                const selection = el.__tmSpreadsheetCanvas?.model?.selection || el.__tmSpreadsheetCanvas?.model?.Selection;
                const startCol = selection?.startCol ?? selection?.StartCol ?? 0;
                const endCol = selection?.endCol ?? selection?.EndCol ?? 0;
                return el.scrollLeft > 0 && metrics.dragAutoscrollFrames > 0 && endCol > startCol;
            }",
            await grid.ElementHandleAsync());

        await grid.DispatchEventAsync("pointerup", new
        {
            clientX = edgeX,
            clientY = startY,
            button = 0,
            buttons = 0,
            pointerId = 9,
            pointerType = "mouse"
        });
    }

    [TestMethod]
    public async Task CanvasRenderer_SelectedCellKeepsTextAndDrawsFormatting()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            await grid.ElementHandleAsync());

        var result = await grid.EvaluateAsync<CanvasFormattingProbeResult>(
            @"el => {
                const state = el.__tmSpreadsheetCanvas;
                const model = state.model;
                const cells = model.cells || model.Cells;
                const cell = cells.find(c => (c.row ?? c.Row) === 0 && (c.col ?? c.Col) === 0) || cells[0];
                cell.value = 'Visible';
                cell.Value = 'Visible';
                cell.active = true;
                cell.Active = true;
                cell.selected = true;
                cell.Selected = true;
                cell.selectionEnd = true;
                cell.SelectionEnd = true;
                cell.style = cell.Style = {
                    fontFamily: 'Arial',
                    FontFamily: 'Arial',
                    fontSize: 14,
                    FontSize: 14,
                    bold: true,
                    Bold: true,
                    italic: true,
                    Italic: true,
                    underline: true,
                    Underline: true,
                    foreColor: '#111827',
                    ForeColor: '#111827',
                    horizontalAlign: 'left',
                    HorizontalAlign: 'left',
                    verticalAlign: 'bottom',
                    VerticalAlign: 'bottom',
                    borderBottom: { style: 'thick', color: '#111827', Style: 'thick', Color: '#111827' },
                    BorderBottom: { style: 'thick', color: '#111827', Style: 'thick', Color: '#111827' }
                };
                window.tmSpreadsheetCanvas.render(el, state.canvas, model);

                const content = el.querySelector('.tm-spreadsheet-canvas-grid__canvas--content');
                const selection = el.querySelector('.tm-spreadsheet-canvas-grid__canvas--selection');
                const dpr = window.devicePixelRatio || 1;
                const composite = document.createElement('canvas');
                composite.width = content.width;
                composite.height = content.height;
                const ctx = composite.getContext('2d');
                ctx.drawImage(content, 0, 0);
                ctx.drawImage(selection, 0, 0);
                const darkPixels = (x, y, w, h) => {
                    const data = ctx.getImageData(Math.round(x * dpr), Math.round(y * dpr), Math.round(w * dpr), Math.round(h * dpr)).data;
                    let count = 0;
                    for (let i = 0; i < data.length; i += 4) {
                        if (data[i + 3] > 0 && data[i] < 180 && data[i + 1] < 180 && data[i + 2] < 180) count++;
                    }
                    return count;
                };

                return {
                    darkTextPixels: darkPixels(42, 24, 58, 16),
                    underlinePixels: darkPixels(42, 35, 58, 5),
                    borderPixels: darkPixels(40, 38, 64, 5),
                    fontCache: [...state.fontStringCache.values()].join('|')
                };
            }");

        Assert.IsTrue(result.DarkTextPixels > 8, $"Expected selected cell text to stay visible. Dark pixels: {result.DarkTextPixels}.");
        Assert.IsTrue(result.UnderlinePixels > 8, $"Expected underline pixels to be drawn. Dark pixels: {result.UnderlinePixels}.");
        Assert.IsTrue(result.BorderPixels > 20, $"Expected a bottom border to be drawn. Dark pixels: {result.BorderPixels}.");
        Assert.IsTrue(result.FontCache.Contains("italic 700", StringComparison.Ordinal), $"Expected italic bold canvas font. Cache: {result.FontCache}");
    }

    [TestMethod]
    public async Task CanvasRenderer_DrawsFormulaReferenceHighlights()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            await grid.ElementHandleAsync());

        var bluePixels = await grid.EvaluateAsync<int>(
            @"el => {
                const state = el.__tmSpreadsheetCanvas;
                const model = state.model;
                const cells = model.cells || model.Cells;
                const cell = cells.find(c => (c.row ?? c.Row) === 1 && (c.col ?? c.Col) === 1) || cells[0];
                cell.formulaRefColorIndex = 0;
                cell.FormulaRefColorIndex = 0;
                window.tmSpreadsheetCanvas.render(el, state.canvas, model);

                const selection = el.querySelector('.tm-spreadsheet-canvas-grid__canvas--selection');
                const dpr = window.devicePixelRatio || 1;
                const ctx = selection.getContext('2d');
                const left = (cell.left ?? cell.Left) - (model.scrollLeft ?? model.ScrollLeft ?? 0) + (model.rowHeaderWidth ?? model.RowHeaderWidth ?? 40);
                const top = (cell.top ?? cell.Top) - (model.scrollTop ?? model.ScrollTop ?? 0) + (model.columnHeaderHeight ?? model.ColumnHeaderHeight ?? 20);
                const data = ctx.getImageData(Math.round(left * dpr), Math.round(top * dpr), Math.round((cell.width ?? cell.Width) * dpr), Math.round((cell.height ?? cell.Height) * dpr)).data;
                let count = 0;
                for (let i = 0; i < data.length; i += 4) {
                    if (data[i + 3] > 0 && data[i] < 80 && data[i + 1] > 90 && data[i + 2] > 120) count++;
                }
                return count;
            }");

        Assert.IsTrue(bluePixels > 20, $"Expected formula reference highlight pixels. Count: {bluePixels}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_ExposesRedrawDebugMetrics()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();
        await grid.PressAsync("ArrowDown");
        await grid.PressAsync("ArrowDown");

        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            await grid.ElementHandleAsync());

        var keyboardInteractions = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).keyboardInteractions");
        var visibleCells = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).lastVisibleCellCount");

        Assert.IsTrue(keyboardInteractions >= 2, $"Expected keyboard debug counter to increase. Count: {keyboardInteractions}.");
        Assert.IsTrue(visibleCells > 0, $"Expected debug metrics to report visible cells. Count: {visibleCells}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_UsesTextAndLayoutCaches()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            await grid.ElementHandleAsync());

        await grid.EvaluateAsync(
            @"el => {
                const state = el.__tmSpreadsheetCanvas;
                const cells = state?.model?.cells || state?.model?.Cells || [];
                const cell = cells[0];
                if (!state || !cell) return;
                cell.value = 'Cache probe';
                cell.Value = 'Cache probe';
                const style = cell.style || cell.Style || {};
                style.fontSize = 11;
                style.FontSize = 11;
                style.fontFamily = 'Arial';
                style.FontFamily = 'Arial';
                style.foreColor = '#111827';
                style.ForeColor = '#111827';
                style.numberFormat = 'General';
                style.NumberFormat = 'General';
                cell.style = style;
                cell.Style = style;
                window.tmSpreadsheetCanvas.render(el, state.canvas, state.model);
            }");
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).lastTextCount > 0",
            await grid.ElementHandleAsync());

        var visibleRows = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).visibleRowCount");
        var visibleColumns = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).visibleColumnCount");
        var layoutMisses = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).visibleLayoutCacheMisses");
        var fontCacheSize = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).fontStringCacheSize");
        var paintCacheSize = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).paintStyleCacheSize");
        var displayCacheSize = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).displayValueCacheSize");
        var layoutHitsBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).visibleLayoutCacheHits");

        await grid.ClickAsync();
        await grid.PressAsync("ArrowDown");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).visibleLayoutCacheHits > {layoutHitsBefore}",
            await grid.ElementHandleAsync());

        Assert.IsTrue(visibleRows > 0, $"Expected visible row layout cache to report rows. Count: {visibleRows}.");
        Assert.IsTrue(visibleColumns > 0, $"Expected visible column layout cache to report columns. Count: {visibleColumns}.");
        Assert.IsTrue(layoutMisses > 0, $"Expected visible layout cache to record at least one miss. Count: {layoutMisses}.");
        Assert.IsTrue(fontCacheSize > 0, $"Expected font string cache to fill. Size: {fontCacheSize}.");
        Assert.IsTrue(paintCacheSize > 0, $"Expected paint style cache to fill. Size: {paintCacheSize}.");
        Assert.IsTrue(displayCacheSize > 0, $"Expected display value cache to fill. Size: {displayCacheSize}.");
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

    private static int ParseRow(string cellRef)
    {
        var digits = new string(cellRef.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var row) ? row : 0;
    }

    private static Task<string> GetCanvasActiveRefAsync(ILocator grid) =>
        grid.EvaluateAsync<string>(
            "el => el.__tmSpreadsheetCanvas?.model?.activeCellRef || el.__tmSpreadsheetCanvas?.model?.ActiveCellRef || ''");

    private static int ParseColumn(string cellRef)
    {
        var letters = new string(cellRef.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        var column = 0;
        foreach (var letter in letters)
        {
            column = column * 26 + letter - 'A' + 1;
        }

        return column;
    }

    private sealed class CanvasFormattingProbeResult
    {
        public int DarkTextPixels { get; set; }
        public int UnderlinePixels { get; set; }
        public int BorderPixels { get; set; }
        public string FontCache { get; set; } = string.Empty;
    }
}
