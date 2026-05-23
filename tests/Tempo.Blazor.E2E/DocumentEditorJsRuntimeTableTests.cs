using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.E2E;

/// <summary>
/// End-to-end coverage for JS-owned table runtime objects.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorJsRuntimeTableTests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task Phase12_InsertTableFocusesFirstCellAndTypingStaysInsideCell()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var marker = $"phase12-cell-{DateTimeOffset.UtcNow:HHmmssfff}";

        var tableId = await InsertTableThroughRuntimeAsync(page);
        await page.Keyboard.InsertTextAsync(marker);

        var firstCell = page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}'] td[data-cell-id]").First;
        await Assertions.Expect(firstCell).ToContainTextAsync(marker);
        Assert.AreEqual(1, await CountTextOccurrencesAsync(page, marker));

        var selection = await ReadRuntimeSelectionAsync(page);
        Assert.AreEqual("TableCell", selection.Region);
        Assert.IsFalse(string.IsNullOrWhiteSpace(selection.ActiveTableCellId));
    }

    [TestMethod]
    public async Task Phase12_TabMovesCaretToNextCell()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var first = $"A{DateTimeOffset.UtcNow:HHmmssfff}";
        var second = $"B{DateTimeOffset.UtcNow:HHmmssfff}";

        var tableId = await InsertTableThroughRuntimeAsync(page);
        await page.Keyboard.InsertTextAsync(first);
        var firstSelection = await ReadRuntimeSelectionAsync(page);

        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.InsertTextAsync(second);

        var cells = page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}'] td[data-cell-id]");
        await Assertions.Expect(cells.Nth(0)).ToContainTextAsync(first);
        await Assertions.Expect(cells.Nth(1)).ToContainTextAsync(second);
        var secondSelection = await ReadRuntimeSelectionAsync(page);
        Assert.AreEqual("TableCell", secondSelection.Region);
        Assert.AreNotEqual(firstSelection.ActiveTableCellId, secondSelection.ActiveTableCellId);
    }

    [TestMethod]
    public async Task Phase12_AddRowBeforeAndUndoAreJsOwned()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var tableId = await InsertTableThroughRuntimeAsync(page);
        await PlaceCaretInTableCellAsync(page, tableId, rowIndex: 0, cellIndex: 0);
        var fullRenderBefore = await ReadFullRenderCountAsync(page);

        await ExecuteRuntimeCommandAsync(page, "insertTableRowBefore");
        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}'] tr")).ToHaveCountAsync(3);

        await ExecuteRuntimeUndoAsync(page);
        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}'] tr")).ToHaveCountAsync(2);
        Assert.AreEqual(fullRenderBefore, await ReadFullRenderCountAsync(page), "Table row undo should restore the JS-owned DOM without a Blazor full render.");
    }

    [TestMethod]
    public async Task Phase12_GridPickerKeyboardInsertsFourByFiveTable()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        await WaitForWysiwygBodyAsync(page);
        await PlaceCaretInBodyAsync(page);

        await page.Locator("[data-testid='document-ribbon-tab-insert']").ClickAsync();
        await page.Locator("[data-testid='document-toolbar-table']").ClickAsync();
        var picker = page.Locator("[data-testid='document-table-grid-picker']");
        await Assertions.Expect(picker).ToBeVisibleAsync(new() { Timeout = 5000 });
        await picker.PressAsync("ArrowRight");
        await picker.PressAsync("ArrowRight");
        await picker.PressAsync("ArrowRight");
        await picker.PressAsync("ArrowDown");
        await picker.PressAsync("ArrowDown");
        await picker.PressAsync("Enter");

        var table = page.Locator("[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table").Last;
        await Assertions.Expect(table.Locator("tr")).ToHaveCountAsync(4, new() { Timeout = 10000 });
        await Assertions.Expect(table.Locator("tr").First.Locator("td, th")).ToHaveCountAsync(5);
    }

    [TestMethod]
    public async Task Phase12_ContextualToolbarInsertsRow()
    {
        var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
        var tableId = await InsertTableThroughRuntimeAsync(page);
        await PlaceCaretInTableCellAsync(page, tableId, rowIndex: 0, cellIndex: 0);

        await Assertions.Expect(page.Locator("[data-testid='document-table-toolbar']")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid='document-table-toolbar-insert-row-after']").ClickAsync();

        await Assertions.Expect(page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}'] tr"))
            .ToHaveCountAsync(3);
    }

    [TestMethod]
    public async Task Phase12_TableAndCellPropertiesPersistAfterSaveReload()
    {
        var original = await LoadDemoDocumentAsync("contract-demo");
        Assert.IsNotNull(original?.Document);

        try
        {
            var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
            var marker = $"phase12-props-{DateTimeOffset.UtcNow:HHmmssfff}";

            var tableId = await InsertTableThroughRuntimeAsync(page);
            await page.Keyboard.InsertTextAsync(marker);
            await PlaceCaretInTableCellAsync(page, tableId, rowIndex: 0, cellIndex: 0);
            await ApplyTableAndCellPropertiesAsync(page, tableId);
            var editedTable = page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}']");
            var editedCell = editedTable.Locator("td[data-cell-id], th[data-cell-id]").First;
            await Assertions.Expect(editedTable).ToHaveAttributeAsync("data-table-width", "480");
            await Assertions.Expect(editedTable).ToHaveAttributeAsync("data-table-alignment", "center");
            await Assertions.Expect(editedCell).ToHaveAttributeAsync("data-cell-vertical-align", "middle");

            await WaitForDirtyStateAsync(page, expectedDirty: true);
            await page.WaitForTimeoutAsync(500);
            await page.GetByTestId("document-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Saved", new() { Timeout = 10000 });
            await WaitForDirtyStateAsync(page, expectedDirty: false);

            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await WaitForDocumentEditorReadyAsync(page);

            var table = page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}']");
            var firstCell = table.Locator("td[data-cell-id], th[data-cell-id]").First;
            await Assertions.Expect(firstCell).ToContainTextAsync(marker);
            await Assertions.Expect(firstCell).ToHaveAttributeAsync("data-cell-background", "#ff0000");
        }
        finally
        {
            if (original?.Document is not null)
            {
                await SaveDemoDocumentAsync(original.Document);
            }
        }
    }

    [TestMethod]
    public async Task Phase12_SaveReloadKeepsTableContentAndCellMetadata()
    {
        var original = await LoadDemoDocumentAsync("contract-demo");
        Assert.IsNotNull(original?.Document);

        try
        {
            var page = await OpenDocumentEditorAsync(width: 1440, height: 900);
            var marker = $"phase12-save-{DateTimeOffset.UtcNow:HHmmssfff}";

            var tableId = await InsertTableThroughRuntimeAsync(page);
            await page.Keyboard.InsertTextAsync(marker);
            await ApplyFirstCellMetadataAsync(page, tableId);
            await WaitForDirtyStateAsync(page, expectedDirty: true);

            await page.GetByTestId("document-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("document-save-message")).ToContainTextAsync("Saved");
            await WaitForDirtyStateAsync(page, expectedDirty: false);

            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await WaitForDocumentEditorReadyAsync(page);

            var firstCell = page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}'] td[data-cell-id]").First;
            await Assertions.Expect(firstCell).ToContainTextAsync(marker);
            await Assertions.Expect(firstCell).ToHaveAttributeAsync("data-cell-width", "180");
            await Assertions.Expect(firstCell).ToHaveAttributeAsync("data-cell-background", "rgb(255, 242, 204)");
        }
        finally
        {
            if (original?.Document is not null)
            {
                await SaveDemoDocumentAsync(original.Document);
            }
        }
    }

    private static async Task<string> InsertTableThroughRuntimeAsync(IPage page)
    {
        var tableId = await page.EvaluateAsync<string>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const beforeIds = new Set(Array.from(host?.querySelectorAll('.tm-wysiwyg-table[data-block-id]') || [])
                    .map(table => table.getAttribute('data-block-id'))
                    .filter(Boolean));
                const body = Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body[contenteditable]') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 0
                            && rect.height > 0
                            && style.display !== 'none'
                            && style.visibility !== 'hidden'
                            && !element.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                const anchor = Array.from(body?.querySelectorAll('.tm-wysiwyg-block[data-block-id]:not(table):not(figure):not(hr)') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                body?.focus();
                if (anchor) {
                    const range = document.createRange();
                    range.selectNodeContents(anchor);
                    range.collapse(false);
                    const selection = window.getSelection();
                    selection?.removeAllRanges();
                    selection?.addRange(range);
                    document.dispatchEvent(new Event('selectionchange'));
                }

                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, 'insertTable');
                const tables = Array.from(host?.querySelectorAll('.tm-wysiwyg-table[data-block-id]') || []);
                const inserted = tables.find(table => !beforeIds.has(table.getAttribute('data-block-id')))
                    || tables.find(table => table.contains(window.getSelection()?.anchorNode || null))
                    || tables[tables.length - 1];
                const tableId = inserted?.getAttribute('data-block-id') || '';
                if (!tableId) throw new Error('Inserted table id was not found.');
                return tableId;
            }
            """);

        var table = page.Locator($"[data-testid='document-wysiwyg-host'] .tm-wysiwyg-table[data-block-id='{tableId}']");
        await Assertions.Expect(table).ToBeVisibleAsync();
        return tableId;
    }

    private static Task PlaceCaretInBodyAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const body = Array.from(host?.querySelectorAll('.tm-wysiwyg-page__body[contenteditable]') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        const style = getComputedStyle(element);
                        return rect.width > 0
                            && rect.height > 0
                            && style.display !== 'none'
                            && style.visibility !== 'hidden'
                            && !element.closest('[aria-hidden="true"], .tm-wysiwyg-page--virtual');
                    });
                const anchor = Array.from(body?.querySelectorAll('.tm-wysiwyg-block[data-block-id]:not(table):not(figure):not(hr)') || [])
                    .find(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                if (!body || !anchor) throw new Error('Editable document body was not found.');
                body.focus();
                const range = document.createRange();
                range.selectNodeContents(anchor);
                range.collapse(false);
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));
            }
            """);
    }

    private static Task PlaceCaretInTableCellAsync(IPage page, string tableId, int rowIndex, int cellIndex)
    {
        return page.EvaluateAsync(
            """
            ({ tableId, rowIndex, cellIndex }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const table = host?.querySelector(`.tm-wysiwyg-table[data-block-id="${CSS.escape(tableId)}"]`);
                const row = table?.querySelectorAll('tr')[rowIndex];
                const cell = row?.querySelectorAll('td[data-cell-id], th[data-cell-id]')[cellIndex];
                if (!cell) throw new Error('Table cell was not found.');
                const text = firstTextNode(cell);
                const range = document.createRange();
                if (text) {
                    range.setStart(text, text.textContent.length);
                    range.collapse(true);
                } else {
                    range.selectNodeContents(cell);
                    range.collapse(false);
                }
                cell.closest('[contenteditable="true"]')?.focus();
                const selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                document.dispatchEvent(new Event('selectionchange'));

                function firstTextNode(root) {
                    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                    return walker.nextNode();
                }
            }
            """,
            new { tableId, rowIndex, cellIndex });
    }

    private static Task ExecuteRuntimeCommandAsync(IPage page, string command)
    {
        return page.EvaluateAsync(
            """
            command => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, command);
            }
            """,
            command);
    }

    private static Task ExecuteRuntimeCommandAsync(IPage page, string command, object payload)
    {
        return page.EvaluateAsync(
            """
            ({ command, payload }) => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, command, payload);
            }
            """,
            new { command, payload });
    }

    private static Task ExecuteRuntimeUndoAsync(IPage page)
    {
        return page.EvaluateAsync(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                window.tmDocumentEditorRuntime?.undo?.(instanceId);
            }
            """);
    }

    private static Task ApplyFirstCellMetadataAsync(IPage page, string tableId)
    {
        return page.EvaluateAsync(
            """
            tableId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const table = host?.querySelector(`.tm-wysiwyg-table[data-block-id="${CSS.escape(tableId)}"]`);
                const cell = table?.querySelector('td[data-cell-id], th[data-cell-id]');
                if (!table || !cell) throw new Error('Table cell was not found.');
                cell.style.width = '180px';
                cell.setAttribute('data-cell-width', '180');
                cell.style.backgroundColor = 'rgb(255, 242, 204)';
                cell.setAttribute('data-cell-background', 'rgb(255, 242, 204)');
                cell.style.borderTop = '2px solid rgb(191, 144, 0)';
                cell.setAttribute('data-cell-border-top', '2px solid rgb(191, 144, 0)');
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, 'insertTableColumnAfter');
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, 'deleteTableColumn');
            }
            """,
            tableId);
    }

    private static Task ApplyTableAndCellPropertiesAsync(IPage page, string tableId)
    {
        return page.EvaluateAsync(
            """
            tableId => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const table = host?.querySelector(`.tm-wysiwyg-table[data-block-id="${CSS.escape(tableId)}"]`);
                const cell = table?.querySelector('td[data-cell-id], th[data-cell-id]');
                if (!table || !cell) throw new Error('Table cell was not found.');
                table.style.width = '480px';
                table.setAttribute('data-table-width', '480');
                table.setAttribute('data-table-alignment', 'center');
                table.style.marginLeft = 'auto';
                table.style.marginRight = 'auto';
                table.setAttribute('data-table-cell-padding', '14');
                table.style.setProperty('--tm-document-table-cell-padding', '14px');
                table.style.backgroundColor = '#f7fafc';
                table.setAttribute('data-table-background', '#f7fafc');
                cell.style.backgroundColor = '#ff0000';
                cell.setAttribute('data-cell-background', '#ff0000');
                cell.style.verticalAlign = 'middle';
                cell.setAttribute('data-cell-vertical-align', 'middle');
                cell.style.padding = '12px';
                cell.setAttribute('data-cell-padding', '12');
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, 'insertTableColumnAfter');
                window.tmDocumentEditorRuntime?.executeCommand?.(instanceId, 'deleteTableColumn');
            }
            """,
            tableId);
    }

    private static Task<RuntimeSelectionSnapshot> ReadRuntimeSelectionAsync(IPage page)
    {
        return page.EvaluateAsync<RuntimeSelectionSnapshot>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                return window.tmDocumentEditorRuntime?.getRuntimeSelection?.(instanceId) || {};
            }
            """);
    }

    private static Task<int> CountTextOccurrencesAsync(IPage page, string text)
    {
        return page.EvaluateAsync<int>(
            """
            text => {
                const content = document.querySelector('[data-testid="document-wysiwyg-host"]')?.innerText || '';
                return content.split(text).length - 1;
            }
            """,
            text);
    }

    private static Task<int> ReadFullRenderCountAsync(IPage page)
    {
        return page.EvaluateAsync<int>(
            """
            () => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const stats = window.tmDocumentEditorDebug?.getRenderStats?.(instanceId) || {};
                return Number(stats.FullRenderCount || 0);
            }
            """);
    }

    private static Task WaitForDirtyStateAsync(IPage page, bool expectedDirty)
    {
        return page.WaitForFunctionAsync(
            """
            expected => {
                const host = document.querySelector('[data-testid="document-wysiwyg-host"]');
                const instanceId = host?.getAttribute('data-instance-id') || '';
                const state = window.tmDocumentEditorRuntime?.getDirtyState?.(instanceId);
                return !!state && !!(state.IsDirty ?? state.isDirty) === expected;
            }
            """,
            expectedDirty);
    }

    private sealed class RuntimeSelectionSnapshot
    {
        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("activeTableCellId")]
        public string? ActiveTableCellId { get; set; }
    }

    private static async Task<DocumentEditorLoadResult?> LoadDemoDocumentAsync(string documentId)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        return await http.GetFromJsonAsync<DocumentEditorLoadResult>($"api/document-editor/{Uri.EscapeDataString(documentId)}");
    }

    private static async Task SaveDemoDocumentAsync(DocumentEditorDocument document)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.PutAsJsonAsync(
            $"api/document-editor/{Uri.EscapeDataString(document.DocumentId)}",
            new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            });
        response.EnsureSuccessStatusCode();
    }
}
