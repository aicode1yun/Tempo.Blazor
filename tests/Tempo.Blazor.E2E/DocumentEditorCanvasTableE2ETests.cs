using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.E2E.CanvasEngine;

namespace Tempo.Blazor.E2E;

/// <summary>Phase 14 E2E coverage for canvas table layout, caret navigation, table commands, and save/reload.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
[DoNotParallelize]
public sealed class DocumentEditorCanvasTableE2ETests : WasmTestBase
{
    private const string Phase14DocumentId = "phase-14-canvas-tables";

    [TestMethod]
    public async Task Phase14_CanvasTables_RenderEditNavigateAndPersist()
    {
        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await OpenPhase14DocumentAsync(page);

        var output = CreateOutputDirectory("desktop-1440x1000");
        var beforePath = Path.Combine(output, "00-phase14-table-before.png");
        var afterPath = Path.Combine(output, "01-phase14-table-after.png");

        await page.GetByTestId("document-editor-demo").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = beforePath,
            Type = ScreenshotType.Png
        });

        var firstCell = await ReadCellRectAsync(page, "canvas-table-phase14-c-layout");
        var rangeTargetCell = await ReadCellRectAsync(page, "canvas-table-phase14-c-undo");
        await page.Mouse.MoveAsync((float)firstCell.CenterX, (float)firstCell.CenterY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)rangeTargetCell.CenterX, (float)rangeTargetCell.CenterY, new MouseMoveOptions { Steps = 6 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync(
            """
            () => Number(document.querySelector('[data-testid="document-canvas-engine-root"]')?.getAttribute('data-canvas-selection-table-cell-range-count') || '0') >= 4
                && document.querySelectorAll('[data-canvas-table-cell-selection="true"]').length >= 4
            """,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        var widthBeforeResize = firstCell.Width;
        await page.Mouse.MoveAsync((float)(firstCell.X + firstCell.Width), (float)firstCell.CenterY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)(firstCell.X + firstCell.Width + 36), (float)firstCell.CenterY, new MouseMoveOptions { Steps = 5 });
        await page.Mouse.UpAsync();
        await page.WaitForFunctionAsync(
            """
            ([cellId, widthBeforeResize]) => {
                const cell = document.querySelector(`[data-canvas-table-cell][data-cell-id="${cellId}"]`);
                const root = document.querySelector('[data-testid="document-canvas-engine-root"]');
                return cell?.getBoundingClientRect().width > Number(widthBeforeResize) + 20
                    && root?.getAttribute('data-canvas-table-resize-active') === 'false';
            }
            """,
            new object[] { "canvas-table-phase14-c-layout", widthBeforeResize },
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        firstCell = await ReadCellRectAsync(page, "canvas-table-phase14-c-layout");
        await page.Mouse.ClickAsync((float)firstCell.CenterX, (float)firstCell.CenterY);
        await page.Keyboard.TypeAsync(" typed");
        await WaitForTypedCellAsync(page);

        await page.Keyboard.PressAsync("Tab");
        await page.WaitForFunctionAsync(
            """
            () => document.querySelector('[data-testid="document-canvas-engine-root"]')?.getAttribute('data-canvas-selection-cell-id') === 'canvas-table-phase14-c-state'
            """,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        var activeCell = await ReadCellRectAsync(page, "canvas-table-phase14-c-state");
        await page.Mouse.ClickAsync((float)activeCell.CenterX, (float)activeCell.CenterY, new MouseClickOptions { Button = MouseButton.Right });
        await Assertions.Expect(page.GetByTestId("document-table-context-menu")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await page.GetByTestId("document-table-insert-row").ClickAsync();
        await page.WaitForFunctionAsync(
            """
            () => Number(document.querySelector('[data-testid="document-canvas-page"]')?.getAttribute('data-canvas-table-cell-count') || '0') >= 12
            """,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        var rowCell = await ReadCellRectAsync(page, "canvas-table-phase14-c-state");
        await page.Mouse.ClickAsync((float)rowCell.CenterX, (float)rowCell.CenterY, new MouseClickOptions { Button = MouseButton.Right });
        await Assertions.Expect(page.GetByTestId("document-table-context-menu")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await page.GetByTestId("document-table-insert-column").ClickAsync();
        await page.WaitForFunctionAsync(
            """
            () => Number(document.querySelector('[data-testid="document-canvas-page"]')?.getAttribute('data-canvas-table-cell-count') || '0') >= 16
            """,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        await page.GetByTestId("document-save").ClickAsync();
        await WaitForSaveBoundaryAsync(page);
        await NavigateWithinBlazorAsync(page, "/canvas-engine-host?documentId=phase-5-canvas-render");
        await page.WaitForFunctionAsync(
            """
            () => document.querySelector('[data-testid="document-canvas-page"]')?.getAttribute('data-canvas-model-document-id') === 'phase-5-canvas-render'
            """,
            new PageWaitForFunctionOptions { Timeout = 20_000 });
        await NavigateWithinBlazorAsync(page, $"/canvas-engine-host?documentId={Phase14DocumentId}&showToolbar=true");
        await WaitForPhase14ReadyAsync(page);
        await page.WaitForFunctionAsync(
            """
            () => document.querySelector('[data-testid="document-canvas-a11y-mirror"]')?.textContent?.includes('Canvas grid typed') === true
                && Number(document.querySelector('[data-testid="document-canvas-page"]')?.getAttribute('data-canvas-table-cell-count') || '0') >= 16
            """,
            new PageWaitForFunctionOptions { Timeout = 10_000 });

        await DocumentEditorCanvasVisualAssert.AssertNoTextOverlapAsync(page);
        await DocumentEditorCanvasVisualAssert.AssertNoUiOverlapAsync(page);
        await DocumentEditorCanvasVisualAssert.AssertCanvasNonBlankAsync(page.Locator("[data-canvas-layer='content']").First);
        await page.GetByTestId("document-editor-demo").ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = afterPath,
            Type = ScreenshotType.Png
        });

        var probe = await page.EvaluateAsync<Phase14Probe>(
            """
            () => {
                const root = document.querySelector('[data-testid="document-canvas-engine-root"]');
                const page = document.querySelector('[data-testid="document-canvas-page"]');
                return {
                    modelDocumentId: page?.getAttribute('data-canvas-model-document-id') || '',
                    tableCount: Number(page?.getAttribute('data-canvas-table-count') || '0'),
                    tableCellCount: Number(page?.getAttribute('data-canvas-table-cell-count') || '0'),
                    textRunCount: Number(page?.getAttribute('data-canvas-text-run-count') || '0'),
                    lastCommand: root?.getAttribute('data-canvas-command-last') || '',
                    inTable: root?.getAttribute('data-canvas-selection-in-table') || '',
                    mirrorContainsTypedCell: document.querySelector('[data-testid="document-canvas-a11y-mirror"]')?.textContent?.includes('Canvas grid typed') === true
                };
            }
            """);

        Assert.AreEqual(Phase14DocumentId, probe.ModelDocumentId);
        Assert.AreEqual(1, probe.TableCount);
        Assert.IsTrue(probe.TableCellCount >= 16);
        Assert.IsTrue(probe.TextRunCount >= 12);
        Assert.IsTrue(probe.MirrorContainsTypedCell);

        var manifestPath = Path.Combine(output, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(new
        {
            testName = nameof(Phase14_CanvasTables_RenderEditNavigateAndPersist),
            seedDocumentId = Phase14DocumentId,
            userActions = new[]
            {
                "Open the phase 14 canvas table seed document.",
                "Drag across cells to create a table cell range selection and drag the first column border to resize it.",
                "Click into a table cell, type text, and navigate to the next cell with Tab.",
                "Use the table context menu to insert a row and a column.",
                "Save and reload the document."
            },
            expectedVisibleChanges = "The canvas paints table cells and cell text, multi-cell range selection is highlighted, column resize changes rendered cell geometry, the caret stays inside table cells, row and column insertions increase the rendered cell count, and the typed cell text survives save/reload.",
            screenshotPaths = new[] { beforePath, afterPath },
            probe
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

        TestContext.AddResultFile(beforePath);
        TestContext.AddResultFile(afterPath);
        TestContext.AddResultFile(manifestPath);
    }

    private async Task OpenPhase14DocumentAsync(IPage page)
    {
        await page.GotoAsync($"{BaseUrl}/canvas-engine-host?documentId={Phase14DocumentId}&showToolbar=true", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 60_000
        });
        await WaitForPhase14ReadyAsync(page);
    }

    private static Task WaitForPhase14ReadyAsync(IPage page)
        => page.WaitForFunctionAsync(
            """
            () => document.querySelector('[data-testid="document-canvas-engine-host"]')?.getAttribute('data-canvas-engine-ready') === 'true'
                && Number(document.querySelector('[data-testid="document-canvas-page"]')?.getAttribute('data-canvas-table-cell-count') || '0') >= 9
                && document.querySelector('[data-canvas-table-cell][data-cell-id="canvas-table-phase14-c-layout"]')
            """,
            new PageWaitForFunctionOptions { Timeout = 30_000 });

    private static Task<CellRect> ReadCellRectAsync(IPage page, string cellId)
        => page.EvaluateAsync<CellRect>(
            """
            cellId => {
                const node = document.querySelector(`[data-canvas-table-cell][data-cell-id="${cellId}"]`);
                if (!node) {
                    throw new Error(`Canvas table cell metadata not found: ${cellId}`);
                }

                const rect = node.getBoundingClientRect();
                return {
                    x: rect.x,
                    y: rect.y,
                    width: rect.width,
                    height: rect.height,
                    centerX: rect.x + rect.width / 2,
                    centerY: rect.y + rect.height / 2
                };
            }
            """,
            cellId);

    private static async Task WaitForTypedCellAsync(IPage page)
    {
        try
        {
            await page.WaitForFunctionAsync(
                """
                () => document.querySelector('[data-testid="document-canvas-a11y-mirror"]')?.textContent?.includes('Canvas grid typed') === true
                    && document.querySelector('[data-testid="document-canvas-engine-root"]')?.getAttribute('data-canvas-selection-in-table') === 'true'
                """,
                new PageWaitForFunctionOptions { Timeout = 10_000 });
        }
        catch (TimeoutException ex)
        {
            var state = await page.EvaluateAsync<Phase14DebugState>(
                """
                () => {
                    const root = document.querySelector('[data-testid="document-canvas-engine-root"]');
                    const mirror = document.querySelector('[data-testid="document-canvas-a11y-mirror"]');
                    const active = document.activeElement;
                    return {
                        mirrorText: mirror?.textContent || '',
                        selectionInTable: root?.getAttribute('data-canvas-selection-in-table') || '',
                        selectionCellId: root?.getAttribute('data-canvas-selection-cell-id') || '',
                        selectionFocusBlockId: root?.getAttribute('data-canvas-selection-focus-block-id') || '',
                        selectionFocusOffset: root?.getAttribute('data-canvas-selection-focus-offset') || '',
                        commandLast: root?.getAttribute('data-canvas-command-last') || '',
                        activeTagName: active?.tagName || '',
                        activeTestId: active?.getAttribute?.('data-testid') || ''
                    };
                }
                """);

            Assert.Fail($"Timed out typing into the first canvas table cell. State: {JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonSerializerDefaults.Web))}. {ex.Message}");
        }
    }

    private static async Task WaitForSaveBoundaryAsync(IPage page)
    {
        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const saveMessage = document.querySelector('[data-testid="document-save-message"]')?.textContent || '';
                    const lastSaved = document.querySelector('[data-testid="document-last-saved"]')?.textContent || '';
                    const pending = document.querySelector('[data-testid="document-pending-status"]')?.textContent || '';
                    const saveButtonDisabled = document.querySelector('[data-testid="document-save"]')?.hasAttribute('disabled') === true;
                    return saveButtonDisabled === false
                        && pending.trim().length === 0
                        && (/Saved|Autosaved/i.test(saveMessage) || /saved/i.test(lastSaved));
                }
                """,
                new PageWaitForFunctionOptions { Timeout = 10_000 });
        }
        catch (TimeoutException ex)
        {
            var state = await page.EvaluateAsync<Phase14SaveDebugState>(
                """
                () => ({
                    saveMessage: document.querySelector('[data-testid="document-save-message"]')?.textContent || '',
                    lastSaved: document.querySelector('[data-testid="document-last-saved"]')?.textContent || '',
                    pending: document.querySelector('[data-testid="document-pending-status"]')?.textContent || '',
                    dirty: document.querySelector('[data-testid="document-dirty-status"]')?.textContent || '',
                    saveDisabled: document.querySelector('[data-testid="document-save"]')?.hasAttribute('disabled') === true,
                    statusBar: document.querySelector('[data-testid="document-status-bar"]')?.textContent || '',
                    bodyHasSaved: /Saved|Autosaved/i.test(document.body.textContent || '')
                })
                """);

            Assert.Fail($"Timed out waiting for the canvas table save boundary. State: {JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonSerializerDefaults.Web))}. {ex.Message}");
        }
    }

    private static Task NavigateWithinBlazorAsync(IPage page, string url)
        => page.EvaluateAsync(
            """
            url => window.Blazor?.navigateTo
                ? window.Blazor.navigateTo(url)
                : window.history.pushState({}, '', url)
            """,
            url);

    private sealed class CellRect
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double CenterX { get; set; }

        public double CenterY { get; set; }
    }

    private sealed class Phase14Probe
    {
        public string ModelDocumentId { get; set; } = string.Empty;

        public int TableCount { get; set; }

        public int TableCellCount { get; set; }

        public int TextRunCount { get; set; }

        public string LastCommand { get; set; } = string.Empty;

        public string InTable { get; set; } = string.Empty;

        public bool MirrorContainsTypedCell { get; set; }
    }

    private sealed class Phase14DebugState
    {
        public string MirrorText { get; set; } = string.Empty;

        public string SelectionInTable { get; set; } = string.Empty;

        public string SelectionCellId { get; set; } = string.Empty;

        public string SelectionFocusBlockId { get; set; } = string.Empty;

        public string SelectionFocusOffset { get; set; } = string.Empty;

        public string CommandLast { get; set; } = string.Empty;

        public string ActiveTagName { get; set; } = string.Empty;

        public string ActiveTestId { get; set; } = string.Empty;
    }

    private sealed class Phase14SaveDebugState
    {
        public string SaveMessage { get; set; } = string.Empty;

        public string LastSaved { get; set; } = string.Empty;

        public string Pending { get; set; } = string.Empty;

        public string Dirty { get; set; } = string.Empty;

        public bool SaveDisabled { get; set; }

        public string StatusBar { get; set; } = string.Empty;

        public bool BodyHasSaved { get; set; }
    }

    private static string CreateOutputDirectory(string viewport)
    {
        var output = Path.Combine(
            FindRepositoryRoot().FullName,
            "tests",
            "Tempo.Blazor.E2E",
            "TestResults",
            "document-editor-canvas",
            "phase14-tables",
            "2026-06-04",
            viewport);
        Directory.CreateDirectory(output);
        return output;
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx from the E2E test output directory.");
    }
}
