using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

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
    public async Task CanvasRenderer_TypingStartsEditAndKeepsAcceptingCharacters()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        await grid.PressAsync("a");
        var editor = grid.Locator(".tm-spreadsheet-canvas-grid__editor");
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Keyboard.TypeAsync("bc");

        var value = await editor.InputValueAsync();
        Assert.AreEqual("abc", value);
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

        var logicalScrollTop = await grid.EvaluateAsync<double>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).logicalScrollTop");
        Assert.IsTrue(logicalScrollTop > 0, $"Expected ArrowDown navigation to advance the logical canvas scroll. logicalScrollTop: {logicalScrollTop}.");
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
    public async Task CanvasRenderer_RapidArrowDownStaysOnLocalHotPath()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var result = await grid.EvaluateAsync<CanvasArrowHotPathProbeResult>(
            @"el => new Promise(resolve => {
                const before = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                for (let i = 0; i < 80; i++) {
                    el.dispatchEvent(new KeyboardEvent('keydown', {
                        key: 'ArrowDown',
                        bubbles: true,
                        cancelable: true
                    }));
                }

                requestAnimationFrame(() => {
                    const first = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                    requestAnimationFrame(() => {
                    const after = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                    const model = el.__tmSpreadsheetCanvas?.model;
                    resolve({
                        activeRef: model?.activeCellRef || model?.ActiveCellRef || '',
                        keyboardInteractions: after.keyboardInteractions - before.keyboardInteractions,
                        selectionCallbacks: after.selectionCallbackCount - before.selectionCallbackCount,
                        keyCommandCallbacks: after.keyCommandCallbackCount - before.keyCommandCallbackCount,
                        paintRequests: after.paintRequestCount - before.paintRequestCount,
                        paintFrames: after.paintFrameCount - before.paintFrameCount,
                        firstFramePaints: first.paintFrameCount - before.paintFrameCount,
                        firstFrameSelectionPaints: first.selectionPaintFrameCount - before.selectionPaintFrameCount,
                        firstFrameContentPaints: first.contentPaintFrameCount - before.contentPaintFrameCount,
                        selectionPaintFrames: after.selectionPaintFrameCount - before.selectionPaintFrameCount,
                        contentPaintFrames: after.contentPaintFrameCount - before.contentPaintFrameCount,
                        mergedPaintRequests: after.mergedPaintRequestCount - before.mergedPaintRequestCount,
                        discardedIntermediatePaints: after.discardedIntermediatePaintCount - before.discardedIntermediatePaintCount,
                        maxMergedPaintRequestsPerFrame: after.maxMergedPaintRequestsPerFrame,
                        keyboardScrollToCount: after.keyboardScrollToCount - before.keyboardScrollToCount,
                        scrollToCount: after.scrollToCount - before.scrollToCount,
                        logicalKeyboardScrollCount: after.logicalKeyboardScrollCount - before.logicalKeyboardScrollCount,
                        logicalScrollTop: after.logicalScrollTop,
                        scrollTop: el.scrollTop
                    });
                    });
                });
            })");

        Assert.IsTrue(ParseRow(result.ActiveRef) >= 70, $"Expected rapid local ArrowDown navigation to advance far down the sheet. Ref: {result.ActiveRef}.");
        Assert.IsTrue(result.KeyboardInteractions >= 80, $"Expected every ArrowDown to stay on the canvas keyboard path. Count: {result.KeyboardInteractions}.");
        Assert.IsTrue(result.SelectionCallbacks <= 2, $"Expected selection sync to coalesce into at most two .NET callbacks. Count: {result.SelectionCallbacks}.");
        Assert.AreEqual(0, result.KeyCommandCallbacks, $"Arrow navigation should not use the .NET key command callback. Count: {result.KeyCommandCallbacks}.");
        Assert.IsTrue(result.PaintRequests >= result.KeyboardInteractions, $"Expected keyboard navigation to request paint locally. Paint requests: {result.PaintRequests}, keyboard interactions: {result.KeyboardInteractions}.");
        Assert.IsTrue(result.PaintFrames <= 2, $"Expected rapid keyboard navigation to coalesce paints into at most two animation frames. Frames: {result.PaintFrames}.");
        Assert.IsTrue(result.FirstFramePaints <= 1, $"Expected one scheduler paint at most in the first animation frame. Frames: {result.FirstFramePaints}.");
        Assert.IsTrue(result.FirstFrameSelectionPaints <= 1, $"Expected selection overlay to draw at most once in the first coalesced keyboard frame. Frames: {result.FirstFrameSelectionPaints}.");
        Assert.IsTrue(result.FirstFrameContentPaints <= 1, $"Expected content canvas to draw at most once in the first coalesced keyboard frame. Frames: {result.FirstFrameContentPaints}.");
        Assert.IsTrue(result.SelectionPaintFrames <= result.PaintFrames, $"Expected selection paints to be bounded by scheduler frames. Selection: {result.SelectionPaintFrames}, frames: {result.PaintFrames}.");
        Assert.IsTrue(result.ContentPaintFrames <= result.PaintFrames, $"Expected content paints to be bounded by scheduler frames. Content: {result.ContentPaintFrames}, frames: {result.PaintFrames}.");
        Assert.IsTrue(result.MergedPaintRequests > 0, $"Expected paint scheduler to merge repeated keyboard paint requests. Count: {result.MergedPaintRequests}.");
        Assert.IsTrue(result.DiscardedIntermediatePaints > 0, $"Expected paint scheduler to discard intermediate keyboard states. Count: {result.DiscardedIntermediatePaints}.");
        Assert.IsTrue(result.MaxMergedPaintRequestsPerFrame > 1, $"Expected more than one paint request in a coalesced frame. Max: {result.MaxMergedPaintRequestsPerFrame}.");
        Assert.AreEqual(0, result.KeyboardScrollToCount, $"Keyboard navigation should move logical scroll without calling root.scrollTo per key. Count: {result.KeyboardScrollToCount}.");
        Assert.IsTrue(result.ScrollToCount <= 1, $"Expected delayed native scroll sync to stay coalesced, not grow with key repeat. Count: {result.ScrollToCount}.");
        Assert.IsTrue(result.LogicalKeyboardScrollCount > 0, $"Expected keyboard navigation to advance logical scroll. Count: {result.LogicalKeyboardScrollCount}.");
        Assert.IsTrue(result.LogicalScrollTop > 0, $"Expected local ArrowDown navigation to move logical scroll. logicalScrollTop: {result.LogicalScrollTop}, native scrollTop: {result.ScrollTop}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_KeyboardScrollSyncsNativeScrollbarAfterDebounce()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        await grid.EvaluateAsync(
            @"el => {
                for (let i = 0; i < 80; i++) {
                    el.dispatchEvent(new KeyboardEvent('keydown', {
                        key: 'ArrowDown',
                        bubbles: true,
                        cancelable: true
                    }));
                }
            }");

        await page.WaitForFunctionAsync(
            @"el => {
                const metrics = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                return metrics.logicalScrollTop > 0
                    && Math.abs(metrics.nativeScrollTop - metrics.logicalScrollTop) <= 1;
            }",
            await grid.ElementHandleAsync(),
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var result = await grid.EvaluateAsync<CanvasScrollbarSyncProbeResult>(
            @"el => {
                const metrics = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                return {
                    nativeScrollTop: metrics.nativeScrollTop,
                    logicalScrollTop: metrics.logicalScrollTop,
                    keyboardScrollToCount: metrics.keyboardScrollToCount,
                    scrollToCount: metrics.scrollToCount,
                    ownNativeScrollEventCount: metrics.ownNativeScrollEventCount
                };
            }");

        Assert.AreEqual(0, result.KeyboardScrollToCount, $"Keyboard scroll should not call root.scrollTo directly. Count: {result.KeyboardScrollToCount}.");
        Assert.IsTrue(result.ScrollToCount <= 1, $"Expected native scrollbar sync to be debounced into at most one scrollTo. Count: {result.ScrollToCount}.");
        Assert.IsTrue(Math.Abs(result.NativeScrollTop - result.LogicalScrollTop) <= 1, $"Expected native scrollbar to match logical scroll. Native: {result.NativeScrollTop}, logical: {result.LogicalScrollTop}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_KeyboardRepeatAcceleratesButClampsToSheetEnd()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var result = await grid.EvaluateAsync<CanvasKeyboardRepeatProbeResult>(
            @"el => new Promise(resolve => {
                window.tmSpreadsheetCanvas.keyboardRepeatAccelerationEnabled = true;
                const state = el.__tmSpreadsheetCanvas;
                const model = state.model;
                const rowCount = model.rowCount ?? model.RowCount;
                const startRow = rowCount - 6;
                model.activeCellRef = model.ActiveCellRef = 'A' + (startRow + 1);
                const selection = model.selection || model.Selection || {};
                selection.startRow = selection.StartRow = startRow;
                selection.startCol = selection.StartCol = 0;
                selection.endRow = selection.EndRow = startRow;
                selection.endCol = selection.EndCol = 0;
                model.selection = model.Selection = selection;

                const before = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                el.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
                for (let i = 0; i < 80; i++) {
                    el.dispatchEvent(new KeyboardEvent('keydown', {
                        key: 'ArrowDown',
                        repeat: true,
                        bubbles: true,
                        cancelable: true
                    }));
                }

                requestAnimationFrame(() => requestAnimationFrame(() => {
                    const after = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                    resolve({
                        activeRef: model.activeCellRef || model.ActiveCellRef || '',
                        rowCount,
                        repeatEvents: after.keyboardRepeatEventCount - before.keyboardRepeatEventCount,
                        acceleratedEvents: after.keyboardRepeatAcceleratedEventCount - before.keyboardRepeatAcceleratedEventCount,
                        maxStep: after.keyboardRepeatMaxStep,
                        lastStep: after.keyboardRepeatLastStep,
                        sequenceCount: after.keyboardRepeatSequenceCount - before.keyboardRepeatSequenceCount
                    });
                }));
            })");

        Assert.AreEqual(result.RowCount, ParseRow(result.ActiveRef), $"Expected accelerated repeat to clamp at the last row. Ref: {result.ActiveRef}, row count: {result.RowCount}.");
        Assert.IsTrue(result.RepeatEvents >= 80, $"Expected repeat events to be counted. Count: {result.RepeatEvents}.");
        Assert.IsTrue(result.AcceleratedEvents > 0, $"Expected long ArrowDown repeat to accelerate. Count: {result.AcceleratedEvents}.");
        Assert.IsTrue(result.MaxStep > 1, $"Expected repeat acceleration to increase the navigation step. Max step: {result.MaxStep}.");
        Assert.AreEqual(1, result.SequenceCount, $"Expected one continuous repeat sequence. Count: {result.SequenceCount}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_ShortArrowPressesStaySingleStep()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var result = await grid.EvaluateAsync<CanvasKeyboardRepeatProbeResult>(
            @"el => new Promise(resolve => {
                window.tmSpreadsheetCanvas.keyboardRepeatAccelerationEnabled = true;
                const model = el.__tmSpreadsheetCanvas.model;
                const beforeRef = model.activeCellRef || model.ActiveCellRef || '';
                const before = window.tmSpreadsheetCanvas.getDebugMetrics(el);

                for (let i = 0; i < 4; i++) {
                    el.dispatchEvent(new KeyboardEvent('keydown', {
                        key: 'ArrowDown',
                        bubbles: true,
                        cancelable: true
                    }));
                }

                requestAnimationFrame(() => requestAnimationFrame(() => {
                    const after = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                    resolve({
                        activeRef: model.activeCellRef || model.ActiveCellRef || '',
                        startRef: beforeRef,
                        repeatEvents: after.keyboardRepeatEventCount - before.keyboardRepeatEventCount,
                        acceleratedEvents: after.keyboardRepeatAcceleratedEventCount - before.keyboardRepeatAcceleratedEventCount,
                        maxStep: after.keyboardRepeatMaxStep,
                        lastStep: after.keyboardRepeatLastStep
                    });
                }));
            })");

        var startRow = ParseRow(result.StartRef);
        var endRow = ParseRow(result.ActiveRef);
        Assert.AreEqual(startRow + 4, endRow, $"Expected four short ArrowDown presses to move exactly four rows. Start: {result.StartRef}, end: {result.ActiveRef}.");
        Assert.AreEqual(0, result.RepeatEvents, $"Expected non-repeat key presses not to count as repeat events. Count: {result.RepeatEvents}.");
        Assert.AreEqual(0, result.AcceleratedEvents, $"Expected non-repeat key presses not to accelerate. Count: {result.AcceleratedEvents}.");
        Assert.AreEqual(1, result.LastStep, $"Expected last keyboard step to remain one row. Step: {result.LastStep}.");
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
        await editor.EvaluateAsync("el => el.blur()");
        await page.WaitForTimeoutAsync(250);
        var redrawsAfterCommit = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).redrawCount");

        Assert.AreEqual(redrawsBeforeCommit, redrawsAfterCommit, "Expected an unchanged local edit to close without a content redraw.");
    }

    [TestMethod]
    public async Task CanvasRenderer_EnterAfterLocalEditMovesActiveCell()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var startRef = await GetCanvasActiveRefAsync(grid);
        await grid.PressAsync("x");

        var editor = grid.Locator(".tm-spreadsheet-canvas-grid__editor");
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Keyboard.TypeAsync("yz");
        await editor.PressAsync("Enter");

        await page.WaitForFunctionAsync(
            @"el => !el.querySelector('.tm-spreadsheet-canvas-grid__editor')
                && (el.__tmSpreadsheetCanvas?.model?.activeCellRef || el.__tmSpreadsheetCanvas?.model?.ActiveCellRef || '') !== ''",
            await grid.ElementHandleAsync());

        var endRef = await GetCanvasActiveRefAsync(grid);
        Assert.AreEqual(ParseRow(startRef) + 1, ParseRow(endRef), $"Expected Enter after local edit to move one row down. Start: {startRef}, end: {endRef}.");
        Assert.AreEqual(ParseColumn(startRef), ParseColumn(endRef), $"Expected Enter after local edit to keep the same column. Start: {startRef}, end: {endRef}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_ScrollDuringLocalEditKeepsEditorAlignedAndHidesWhenOutOfView()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();
        for (var i = 0; i < 10; i++)
        {
            await grid.PressAsync("ArrowDown");
        }
        await grid.PressAsync("F2");

        var editor = grid.Locator(".tm-spreadsheet-canvas-grid__editor");
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var beforeTop = await editor.EvaluateAsync<double>("el => el.getBoundingClientRect().top");

        await grid.EvaluateAsync(
            @"el => new Promise(resolve => {
                const state = el.__tmSpreadsheetCanvas;
                state.logicalScrollTop += 40;
                state.model.scrollTop = state.model.ScrollTop = state.logicalScrollTop;
                window.tmSpreadsheetCanvas.render(el, state.canvas, state.model);
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            })");
        var movedTopThreshold = (beforeTop - 20).ToString(CultureInfo.InvariantCulture);
        await page.WaitForFunctionAsync(
            $"el => el.getBoundingClientRect().top < {movedTopThreshold}",
            await editor.ElementHandleAsync());

        var afterTop = await editor.EvaluateAsync<double>("el => el.getBoundingClientRect().top");
        Assert.IsTrue(afterTop < beforeTop, $"Expected editor to move upward with the edited cell during scroll. Before: {beforeTop}, after: {afterTop}.");

        await grid.EvaluateAsync(
            @"el => new Promise(resolve => {
                const state = el.__tmSpreadsheetCanvas;
                state.logicalScrollTop += 400;
                state.model.scrollTop = state.model.ScrollTop = state.logicalScrollTop;
                window.tmSpreadsheetCanvas.render(el, state.canvas, state.model);
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            })");
        await page.WaitForFunctionAsync(
            @"el => {
                const editor = el.querySelector('.tm-spreadsheet-canvas-grid__editor');
                if (!editor) return true;
                if (getComputedStyle(editor).visibility === 'hidden') return true;
                const gridRect = el.getBoundingClientRect();
                const editorRect = editor.getBoundingClientRect();
                return editorRect.bottom < gridRect.top || editorRect.top > gridRect.bottom;
            }",
            await grid.ElementHandleAsync());
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
        var userScrollEventsBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).userNativeScrollEventCount");
        await grid.EvaluateAsync("el => { el.scrollTop += 40; }");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).bitmapShiftCount > {shiftsBefore}",
            await grid.ElementHandleAsync());
        var userScrollEventsAfter = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).userNativeScrollEventCount");
        Assert.IsTrue(userScrollEventsAfter > userScrollEventsBefore, $"Expected direct native scroll to count as a user scroll event. Before: {userScrollEventsBefore}, after: {userScrollEventsAfter}.");

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
                return metrics.logicalScrollTop > 0 && metrics.dragAutoscrollFrames > 0 && endRow > startRow;
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
                return metrics.logicalScrollLeft > 0 && metrics.dragAutoscrollFrames > 0 && endCol > startCol;
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
    public async Task CanvasRenderer_DragSelectionAutoscrollStaysOnLogicalHotPath()
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
        var before = await grid.EvaluateAsync<CanvasDragAutoscrollHotPathProbeResult>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el)");

        await grid.DispatchEventAsync("pointerdown", new
        {
            clientX = startX,
            clientY = startY,
            button = 0,
            buttons = 1,
            pointerId = 18,
            pointerType = "mouse"
        });

        await grid.DispatchEventAsync("pointermove", new
        {
            clientX = startX,
            clientY = edgeY,
            button = 0,
            buttons = 1,
            pointerId = 18,
            pointerType = "mouse"
        });

        await page.WaitForFunctionAsync(
            @"el => {
                const metrics = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                return metrics.logicalScrollTop > 0 && metrics.dragAutoscrollFrames >= 4;
            }",
            await grid.ElementHandleAsync());

        await grid.DispatchEventAsync("pointerup", new
        {
            clientX = startX,
            clientY = edgeY,
            button = 0,
            buttons = 0,
            pointerId = 18,
            pointerType = "mouse"
        });

        await page.WaitForTimeoutAsync(50);
        var after = await grid.EvaluateAsync<CanvasDragAutoscrollHotPathProbeResult>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el)");

        var dragFrames = after.DragAutoscrollFrames - before.DragAutoscrollFrames;
        var viewportCallbacks = after.ViewportCallbackCount - before.ViewportCallbackCount;
        var selectionCallbacks = after.SelectionCallbackCount - before.SelectionCallbackCount;
        var scrollToCount = after.ScrollToCount - before.ScrollToCount;
        var selectionRedraws = after.SelectionRedrawCount - before.SelectionRedrawCount;

        Assert.IsTrue(after.LogicalScrollTop > before.LogicalScrollTop, $"Expected drag autoscroll to advance logical scroll. Before: {before.LogicalScrollTop}, after: {after.LogicalScrollTop}.");
        Assert.IsTrue(after.LogicalPointerScrollCount > before.LogicalPointerScrollCount, "Expected drag autoscroll to use logical pointer scroll.");
        Assert.IsTrue(dragFrames >= 4, $"Expected several drag autoscroll frames. Count: {dragFrames}.");
        Assert.IsTrue(scrollToCount <= 1, $"Expected drag autoscroll to avoid native scrollTo per frame. scrollTo delta: {scrollToCount}, drag frames: {dragFrames}.");
        Assert.IsTrue(viewportCallbacks <= 2, $"Expected drag autoscroll viewport callbacks to be coalesced. Callbacks: {viewportCallbacks}, drag frames: {dragFrames}.");
        Assert.IsTrue(selectionCallbacks <= 2, $"Expected drag selection callbacks to be coalesced. Callbacks: {selectionCallbacks}, drag frames: {dragFrames}.");
        Assert.IsTrue(selectionRedraws >= dragFrames, $"Expected selection overlay to redraw during drag autoscroll frames. Selection redraws: {selectionRedraws}, drag frames: {dragFrames}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_WheelScrollStaysOnLogicalHotPath()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var result = await grid.EvaluateAsync<CanvasWheelHotPathProbeResult>(
            @"el => new Promise(resolve => {
                const before = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                for (let i = 0; i < 40; i++) {
                    el.dispatchEvent(new WheelEvent('wheel', {
                        deltaY: 32,
                        deltaMode: WheelEvent.DOM_DELTA_PIXEL,
                        bubbles: true,
                        cancelable: true
                    }));
                }

                requestAnimationFrame(() => requestAnimationFrame(() => {
                    setTimeout(() => {
                        const after = window.tmSpreadsheetCanvas.getDebugMetrics(el);
                        resolve({
                            logicalScrollTop: after.logicalScrollTop,
                            nativeScrollTop: after.nativeScrollTop,
                            wheelEvents: after.wheelEventCount - before.wheelEventCount,
                            wheelPrevented: after.wheelPreventedCount - before.wheelPreventedCount,
                            logicalWheelScrollCount: after.logicalWheelScrollCount - before.logicalWheelScrollCount,
                            viewportCallbacks: after.viewportCallbackCount - before.viewportCallbackCount,
                            scrollToCount: after.scrollToCount - before.scrollToCount,
                            paintRequests: after.paintRequestCount - before.paintRequestCount,
                            paintFrames: after.paintFrameCount - before.paintFrameCount,
                            contentPaintFrames: after.contentPaintFrameCount - before.contentPaintFrameCount,
                            maxMergedPaintRequestsPerFrame: after.maxMergedPaintRequestsPerFrame
                        });
                    }, 180);
                }));
            })");

        Assert.IsTrue(result.LogicalScrollTop > 0, $"Expected wheel to advance logical scroll. logicalScrollTop: {result.LogicalScrollTop}.");
        Assert.AreEqual(40, result.WheelEvents, $"Expected every wheel event to stay on the canvas wheel path. Count: {result.WheelEvents}.");
        Assert.AreEqual(40, result.WheelPrevented, $"Expected wheel events to be prevented before native scroll. Count: {result.WheelPrevented}.");
        Assert.IsTrue(result.LogicalWheelScrollCount > 0, $"Expected wheel to use logical scroll. Count: {result.LogicalWheelScrollCount}.");
        Assert.IsTrue(result.PaintFrames <= 2, $"Expected wheel paint to be coalesced into at most two frames. Paint frames: {result.PaintFrames}.");
        Assert.IsTrue(result.ContentPaintFrames <= 2, $"Expected wheel content paint to be coalesced. Content frames: {result.ContentPaintFrames}.");
        Assert.IsTrue(result.ScrollToCount <= 1, $"Expected wheel to sync the native scrollbar at most once after paint. scrollTo delta: {result.ScrollToCount}.");
        Assert.IsTrue(result.ViewportCallbacks <= 2, $"Expected wheel viewport callbacks to be coalesced. Count: {result.ViewportCallbacks}.");
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
            @"el => new Promise(resolve => {
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

                requestAnimationFrame(() => requestAnimationFrame(() => {
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

                    resolve({
                        darkTextPixels: darkPixels(42, 24, 58, 16),
                        underlinePixels: darkPixels(42, 35, 58, 5),
                        borderPixels: darkPixels(40, 38, 64, 5),
                        fontCache: [...state.fontStringCache.values()].join('|')
                    });
                }));
            })");

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
            @"el => new Promise(resolve => {
                const state = el.__tmSpreadsheetCanvas;
                const model = state.model;
                const cells = model.cells || model.Cells;
                const cell = cells.find(c => (c.row ?? c.Row) === 1 && (c.col ?? c.Col) === 1) || cells[0];
                cell.formulaRefColorIndex = 0;
                cell.FormulaRefColorIndex = 0;
                window.tmSpreadsheetCanvas.render(el, state.canvas, model);

                requestAnimationFrame(() => requestAnimationFrame(() => {
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
                    resolve(count);
                }));
            })");

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
        await grid.EvaluateAsync(
            @"el => {
                const state = el.__tmSpreadsheetCanvas;
                const model = state.model;
                const cells = model.cells || model.Cells;
                const simple = cells.find(c => (c.row ?? c.Row) === 2 && (c.col ?? c.Col) === 1) || cells[0];
                simple.value = 'Fast';
                simple.Value = 'Fast';
                simple.style = simple.Style = {
                    horizontalAlign: 'left',
                    HorizontalAlign: 'left',
                    verticalAlign: 'bottom',
                    VerticalAlign: 'bottom'
                };

                const slow = cells.find(c => (c.row ?? c.Row) === 2 && (c.col ?? c.Col) === 2) || cells[1];
                slow.value = 'Slow';
                slow.Value = 'Slow';
                slow.style = slow.Style = {
                    bold: true,
                    Bold: true,
                    backgroundColor: '#e2e8f0',
                    BackgroundColor: '#e2e8f0',
                    horizontalAlign: 'left',
                    HorizontalAlign: 'left',
                    verticalAlign: 'bottom',
                    VerticalAlign: 'bottom'
                };

                window.tmSpreadsheetCanvas.render(el, state.canvas, model);
            }");
        await grid.PressAsync("ArrowDown");
        await grid.PressAsync("ArrowDown");

        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            await grid.ElementHandleAsync());

        var keyboardInteractions = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).keyboardInteractions");
        var visibleCells = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).lastVisibleCellCount");
        var fastCells = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).fastCellPathCount");
        var slowCells = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).slowCellPathCount");
        var contextStateSkips = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).contextStateSkipCount");
        var clippedTexts = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).clippedTextCount");
        var unclippedTexts = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).unclippedTextCount");

        Assert.IsTrue(keyboardInteractions >= 2, $"Expected keyboard debug counter to increase. Count: {keyboardInteractions}.");
        Assert.IsTrue(visibleCells > 0, $"Expected debug metrics to report visible cells. Count: {visibleCells}.");
        Assert.IsTrue(fastCells > 0, $"Expected simple visible cells to use the canvas fast path. Count: {fastCells}.");
        Assert.IsTrue(slowCells > 0, $"Expected formatted/header cells to use the canvas slow path. Count: {slowCells}.");
        Assert.IsTrue(contextStateSkips > 0, $"Expected context state cache to skip redundant assignments. Count: {contextStateSkips}.");
        Assert.IsTrue(unclippedTexts > clippedTexts, $"Expected most regular cells to draw without clipping. Unclipped: {unclippedTexts}, clipped: {clippedTexts}.");
    }

    [TestMethod]
    public async Task CanvasRenderer_SelectionOnlyRedrawDoesNotMissContentSnapshots()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet");
        await WaitForAppReadyAsync(page);

        var grid = page.Locator(".tm-spreadsheet-canvas-grid").First;
        await grid.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await grid.ClickAsync();

        var gridHandle = await grid.ElementHandleAsync();
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas?.getDebugMetrics?.(el)?.redrawCount > 0",
            gridHandle);

        await grid.EvaluateAsync(
            @"el => new Promise(resolve => {
                const state = el.__tmSpreadsheetCanvas;
                const cells = state.model.cells || state.model.Cells || [];
                for (let i = 0; i < Math.min(cells.length, 12); i++) {
                    const cell = cells[i];
                    cell.value = 'Snapshot ' + i;
                    cell.Value = 'Snapshot ' + i;
                    cell.style = cell.Style = {
                        horizontalAlign: 'left',
                        HorizontalAlign: 'left',
                        verticalAlign: 'bottom',
                        VerticalAlign: 'bottom'
                    };
                }
                window.tmSpreadsheetCanvas.render(el, state.canvas, state.model);
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            })");
        await page.WaitForFunctionAsync(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).cellSnapshotMissCount > 0",
            gridHandle);

        var hitsBeforeSecondRender = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).cellSnapshotHitCount");
        await grid.EvaluateAsync(
            @"el => new Promise(resolve => {
                const state = el.__tmSpreadsheetCanvas;
                window.tmSpreadsheetCanvas.render(el, state.canvas, state.model);
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            })");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).cellSnapshotHitCount > {hitsBeforeSecondRender}",
            gridHandle);

        var missesBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).cellSnapshotMissCount");
        var selectionFramesBefore = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).selectionPaintFrameCount");

        await grid.PressAsync("ArrowRight");
        await page.WaitForFunctionAsync(
            $"el => window.tmSpreadsheetCanvas.getDebugMetrics(el).selectionPaintFrameCount > {selectionFramesBefore}",
            gridHandle);

        var missesAfter = await grid.EvaluateAsync<int>(
            "el => window.tmSpreadsheetCanvas.getDebugMetrics(el).cellSnapshotMissCount");

        Assert.AreEqual(missesBefore, missesAfter, "Expected selection-only keyboard movement to avoid content snapshot misses.");
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

    [TestMethod]
    public async Task BenchmarkPage_Phase11ReadinessMetricsPass()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet-benchmark");
        await WaitForAppReadyAsync(page);

        await page.GetByTestId("spreadsheet-benchmark-run-both").ClickAsync();
        await page.WaitForFunctionAsync(
            "document.querySelectorAll('[data-testid=\"spreadsheet-benchmark-result-row\"]').length >= 2",
            null,
            new PageWaitForFunctionOptions { Timeout = 60000 });

        var canvas = page.Locator("[data-testid=\"spreadsheet-benchmark-result-row\"][data-renderer=\"Canvas\"]").First;
        var dom = page.Locator("[data-testid=\"spreadsheet-benchmark-result-row\"][data-renderer=\"Dom\"]").First;
        await canvas.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await dom.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        var canvasDown = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-down-ms");
        var canvasUp = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-up-ms");
        var canvasRight = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-right-ms");
        var canvasLogicalKeyboard = await GetBenchmarkMetricAsync(canvas, "data-keyboard-logical-scroll-count");
        var canvasKeyboardScrollTo = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-to-count");
        var canvasWheelEvents = await GetBenchmarkMetricAsync(canvas, "data-wheel-events");
        var canvasWheelPrevented = await GetBenchmarkMetricAsync(canvas, "data-wheel-prevented");
        var canvasWheelLogical = await GetBenchmarkMetricAsync(canvas, "data-wheel-logical-scroll-count");
        var canvasWheelViewportCallbacks = await GetBenchmarkMetricAsync(canvas, "data-wheel-viewport-callback-count");
        var canvasWheelScrollTo = await GetBenchmarkMetricAsync(canvas, "data-wheel-scroll-to-count");
        var canvasWheelPaintFrames = await GetBenchmarkMetricAsync(canvas, "data-wheel-paint-frame-count");
        var canvasWheelContentFrames = await GetBenchmarkMetricAsync(canvas, "data-wheel-content-paint-frame-count");
        var canvasDragFrames = await GetBenchmarkMetricAsync(canvas, "data-drag-frames");
        var canvasDragLogical = await GetBenchmarkMetricAsync(canvas, "data-drag-logical-scroll-count");
        var canvasDragViewportCallbacks = await GetBenchmarkMetricAsync(canvas, "data-drag-viewport-callback-count");
        var canvasDragSelectionCallbacks = await GetBenchmarkMetricAsync(canvas, "data-drag-selection-callback-count");
        var canvasDragScrollTo = await GetBenchmarkMetricAsync(canvas, "data-drag-scroll-to-count");
        var domDown = await GetBenchmarkMetricAsync(dom, "data-keyboard-scroll-down-ms");

        const double phase10KeyboardEdgeBaselineMs = 612.2;

        Assert.IsTrue(canvasDown < phase10KeyboardEdgeBaselineMs, $"Expected ArrowDown edge navigation to stay below phase 10 baseline. Current: {canvasDown:N1} ms.");
        Assert.IsTrue(canvasUp < phase10KeyboardEdgeBaselineMs, $"Expected ArrowUp edge navigation to stay below phase 10 baseline. Current: {canvasUp:N1} ms.");
        Assert.IsTrue(canvasRight < phase10KeyboardEdgeBaselineMs, $"Expected ArrowRight edge navigation to stay below phase 10 baseline. Current: {canvasRight:N1} ms.");
        Assert.IsTrue(canvasDown <= domDown * 1.25, $"Expected canvas ArrowDown edge navigation to be comparable to DOM on 1,000 x 50. Canvas: {canvasDown:N1} ms, DOM: {domDown:N1} ms.");
        Assert.IsTrue(canvasLogicalKeyboard > 0, $"Expected keyboard edge navigation to use logical scroll. Count: {canvasLogicalKeyboard:N0}.");
        Assert.AreEqual(0d, canvasKeyboardScrollTo, $"Keyboard hot path should not call root.scrollTo per key. Count: {canvasKeyboardScrollTo:N0}.");
        Assert.IsTrue(canvasWheelEvents > 0, "Expected benchmark to exercise wheel events.");
        Assert.AreEqual(canvasWheelEvents, canvasWheelPrevented, $"Canvas wheel benchmark should prevent native wheel scroll. Events: {canvasWheelEvents:N0}, prevented: {canvasWheelPrevented:N0}.");
        Assert.IsTrue(canvasWheelLogical > 0, $"Expected wheel scroll to use logical scroll. Count: {canvasWheelLogical:N0}.");
        Assert.IsTrue(canvasWheelViewportCallbacks <= 2, $"Expected wheel viewport callbacks to stay coalesced. Count: {canvasWheelViewportCallbacks:N0}.");
        Assert.IsTrue(canvasWheelScrollTo <= 2, $"Expected wheel native scrollbar sync to stay coalesced. scrollTo count: {canvasWheelScrollTo:N0}.");
        Assert.IsTrue(canvasWheelPaintFrames <= canvasWheelEvents, $"Expected wheel paints not to exceed wheel events. Paint frames: {canvasWheelPaintFrames:N0}, wheel events: {canvasWheelEvents:N0}.");
        Assert.IsTrue(canvasWheelContentFrames <= canvasWheelPaintFrames, $"Expected wheel content paints to be bounded by paint frames. Content: {canvasWheelContentFrames:N0}, frames: {canvasWheelPaintFrames:N0}.");
        Assert.IsTrue(canvasDragFrames > 0, $"Expected drag autoscroll to produce frames. Count: {canvasDragFrames:N0}.");
        Assert.IsTrue(canvasDragLogical > 0, $"Expected drag autoscroll to use logical pointer scroll. Count: {canvasDragLogical:N0}.");
        Assert.IsTrue(canvasDragViewportCallbacks <= 2, $"Expected drag viewport callbacks to stay coalesced. Count: {canvasDragViewportCallbacks:N0}.");
        Assert.IsTrue(canvasDragSelectionCallbacks <= 2, $"Expected drag selection callbacks to stay coalesced. Count: {canvasDragSelectionCallbacks:N0}.");
        Assert.IsTrue(canvasDragScrollTo <= 2, $"Expected drag autoscroll native sync to stay coalesced. scrollTo count: {canvasDragScrollTo:N0}.");
    }

    [TestMethod]
    public async Task BenchmarkPage_CanvasKeyboardUsableOnLargeDataset()
    {
        var page = await CreatePageAsync();
        await page.GotoAsync($"{BaseUrl}/spreadsheet-benchmark");
        await WaitForAppReadyAsync(page);

        await page.Locator("#benchmark-dataset").SelectOptionAsync("10000x100");
        await page.GetByTestId("spreadsheet-benchmark-run-canvas").ClickAsync();

        var canvas = page.Locator("[data-testid=\"spreadsheet-benchmark-result-row\"][data-renderer=\"Canvas\"]").First;
        await canvas.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });

        var canvasDown = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-down-ms");
        var canvasUp = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-up-ms");
        var canvasRight = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-right-ms");
        var canvasLogicalKeyboard = await GetBenchmarkMetricAsync(canvas, "data-keyboard-logical-scroll-count");
        var canvasKeyboardScrollTo = await GetBenchmarkMetricAsync(canvas, "data-keyboard-scroll-to-count");

        const double phase10KeyboardEdgeBaselineMs = 612.2;

        Assert.IsTrue(canvasDown < phase10KeyboardEdgeBaselineMs, $"Expected 10,000 x 100 ArrowDown edge navigation to stay usable. Current: {canvasDown:N1} ms.");
        Assert.IsTrue(canvasUp < phase10KeyboardEdgeBaselineMs, $"Expected 10,000 x 100 ArrowUp edge navigation to stay usable. Current: {canvasUp:N1} ms.");
        Assert.IsTrue(canvasRight < phase10KeyboardEdgeBaselineMs, $"Expected 10,000 x 100 ArrowRight edge navigation to stay usable. Current: {canvasRight:N1} ms.");
        Assert.IsTrue(canvasLogicalKeyboard > 0, $"Expected large dataset keyboard navigation to use logical scroll. Count: {canvasLogicalKeyboard:N0}.");
        Assert.AreEqual(0d, canvasKeyboardScrollTo, $"Large dataset keyboard hot path should not call root.scrollTo per key. Count: {canvasKeyboardScrollTo:N0}.");
    }

    private static int ParseRow(string cellRef)
    {
        var digits = new string(cellRef.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var row) ? row : 0;
    }

    private static async Task<double> GetBenchmarkMetricAsync(ILocator row, string attributeName)
    {
        var value = await row.GetAttributeAsync(attributeName);
        Assert.IsNotNull(value, $"Expected benchmark row to expose {attributeName}.");
        return double.Parse(value, CultureInfo.InvariantCulture);
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

    private sealed class CanvasArrowHotPathProbeResult
    {
        public string ActiveRef { get; set; } = string.Empty;
        public int KeyboardInteractions { get; set; }
        public int SelectionCallbacks { get; set; }
        public int KeyCommandCallbacks { get; set; }
        public int PaintRequests { get; set; }
        public int PaintFrames { get; set; }
        public int FirstFramePaints { get; set; }
        public int FirstFrameSelectionPaints { get; set; }
        public int FirstFrameContentPaints { get; set; }
        public int SelectionPaintFrames { get; set; }
        public int ContentPaintFrames { get; set; }
        public int MergedPaintRequests { get; set; }
        public int DiscardedIntermediatePaints { get; set; }
        public int MaxMergedPaintRequestsPerFrame { get; set; }
        public int KeyboardScrollToCount { get; set; }
        public int ScrollToCount { get; set; }
        public int LogicalKeyboardScrollCount { get; set; }
        public double LogicalScrollTop { get; set; }
        public double ScrollTop { get; set; }
    }

    private sealed class CanvasScrollbarSyncProbeResult
    {
        public double NativeScrollTop { get; set; }
        public double LogicalScrollTop { get; set; }
        public int KeyboardScrollToCount { get; set; }
        public int ScrollToCount { get; set; }
        public int OwnNativeScrollEventCount { get; set; }
    }

    private sealed class CanvasDragAutoscrollHotPathProbeResult
    {
        public double LogicalScrollTop { get; set; }
        public int LogicalPointerScrollCount { get; set; }
        public int DragAutoscrollFrames { get; set; }
        public int ViewportCallbackCount { get; set; }
        public int SelectionCallbackCount { get; set; }
        public int SelectionRedrawCount { get; set; }
        public int ScrollToCount { get; set; }
    }

    private sealed class CanvasWheelHotPathProbeResult
    {
        public double LogicalScrollTop { get; set; }
        public double NativeScrollTop { get; set; }
        public int WheelEvents { get; set; }
        public int WheelPrevented { get; set; }
        public int LogicalWheelScrollCount { get; set; }
        public int ViewportCallbacks { get; set; }
        public int ScrollToCount { get; set; }
        public int PaintRequests { get; set; }
        public int PaintFrames { get; set; }
        public int ContentPaintFrames { get; set; }
        public int MaxMergedPaintRequestsPerFrame { get; set; }
    }

    private sealed class CanvasKeyboardRepeatProbeResult
    {
        public string ActiveRef { get; set; } = string.Empty;
        public string StartRef { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public int RepeatEvents { get; set; }
        public int AcceleratedEvents { get; set; }
        public int MaxStep { get; set; }
        public int LastStep { get; set; }
        public int SequenceCount { get; set; }
    }
}
