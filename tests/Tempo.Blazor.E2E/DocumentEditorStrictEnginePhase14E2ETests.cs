using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for table model, layout, commands, and cell editing quality.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase14E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_Tables_NormalizeFullModelAndRoundtrip()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TableModelProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase14-model',
                    Blocks: [{
                        Id: 'tbl1',
                        Type: 'Table',
                        Content: {
                            Style: { width: 420, borderCollapse: 'collapse' },
                            Rows: [{
                                Id: 'row1',
                                Height: 44,
                                Cells: [{
                                    Id: 'cell1',
                                    RowSpan: 1,
                                    ColSpan: 2,
                                    Width: 240,
                                    Height: 44,
                                    Style: { background: '#eef6ff', border: '1px solid #94a3b8', padding: 8 },
                                    Blocks: [{ Id: 'cell-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'cell-r1', Text: 'Header' }] } }]
                                }]
                            }]
                        }
                    }]
                });
                const table = model.body.blocks[0];
                const exported = engine.model.exportToCSharpJson(model).Blocks[0].Content.Rows[0].Cells[0];
                return {
                    tableId: table.id,
                    rowId: table.content.rows[0].id,
                    cellId: table.content.rows[0].cells[0].id,
                    blockId: table.content.rows[0].cells[0].blocks[0].id,
                    rowSpan: table.content.rows[0].cells[0].rowSpan,
                    colSpan: table.content.rows[0].cells[0].colSpan,
                    width: table.content.rows[0].cells[0].width,
                    height: table.content.rows[0].cells[0].height,
                    background: table.content.rows[0].cells[0].style.background,
                    border: table.content.rows[0].cells[0].style.border,
                    padding: table.content.rows[0].cells[0].style.padding,
                    exportedColSpan: exported.ColSpan,
                    exportedBackground: exported.Style.background
                };
            }
            """);

        result.TableId.Should().Be("tbl1");
        result.RowId.Should().Be("row1");
        result.CellId.Should().Be("cell1");
        result.BlockId.Should().Be("cell-p1");
        result.RowSpan.Should().Be(1);
        result.ColSpan.Should().Be(2);
        result.Width.Should().Be(240);
        result.Height.Should().Be(44);
        result.Background.Should().Be("#eef6ff");
        result.Border.Should().Be("1px solid #94a3b8");
        result.Padding.Should().Be(8);
        result.ExportedColSpan.Should().Be(2);
        result.ExportedBackground.Should().Be("#eef6ff");
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Tables_LayoutCellsWithParagraphEngineAndHitTest()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TableLayoutProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase14-layout',
                    Blocks: [{
                        Id: 'tbl1',
                        Type: 'Table',
                        Content: {
                            Style: { width: 360 },
                            Rows: [
                                { Id: 'row1', Cells: [
                                    { Id: 'cell1', Width: 180, Blocks: [{ Id: 'cell-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Left cell text wraps safely' }] } }] },
                                    { Id: 'cell2', Width: 180, Blocks: [{ Id: 'cell-p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'Right cell text' }] } }] }
                                ] },
                                { Id: 'row2', Cells: [
                                    { Id: 'cell3', Blocks: [{ Id: 'cell-p3', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r3', Text: 'Bottom left' }] } }] },
                                    { Id: 'cell4', Blocks: [{ Id: 'cell-p4', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r4', Text: 'Bottom right' }] } }] }
                                ] }
                            ]
                        }
                    }]
                });
                const layout = engine.textLayout.createParagraphLayoutEngine(null, {
                    pageWidth: 520,
                    pageHeight: 420,
                    margin: { top: 40, right: 40, bottom: 40, left: 40 }
                }).layoutDocument(model);
                const table = layout.blocks.find(block => block.blockId === 'tbl1');
                const cell1 = table.cells.find(cell => cell.cellId === 'cell1');
                const cell2 = table.cells.find(cell => cell.cellId === 'cell2');
                const hit = engine.selection.pointerHitTest(model, layout, cell2.rect.x + 8, cell2.rect.y + 12);
                const overlap = table.cells.some((a, index) => table.cells.some((b, other) =>
                    other > index &&
                    a.rect.x < b.rect.x + b.rect.width &&
                    a.rect.x + a.rect.width > b.rect.x &&
                    a.rect.y < b.rect.y + b.rect.height &&
                    a.rect.y + a.rect.height > b.rect.y));
                const textInsideBorders = table.cells.every(cell => cell.blockLayouts.every(block =>
                    block.rect.x >= cell.contentFrame.x &&
                    block.rect.x + block.rect.width <= cell.contentFrame.x + cell.contentFrame.width &&
                    block.rect.y >= cell.contentFrame.y &&
                    block.rect.y + block.rect.height <= cell.contentFrame.y + cell.contentFrame.height + 0.5));
                return {
                    layoutOk: layout.ok,
                    tableType: table.type,
                    cellCount: table.cells.length,
                    cellContentBlockId: cell1.blockLayouts[0].blockId,
                    samePage: table.pageIndex === 0,
                    hitType: hit.type,
                    hitCellId: hit.cellId,
                    hitBlockId: hit.position.blockId,
                    overlap,
                    textInsideBorders,
                    cell1Width: cell1.rect.width,
                    cell2X: cell2.rect.x
                };
            }
            """);

        result.LayoutOk.Should().BeTrue();
        result.TableType.Should().Be("table");
        result.CellCount.Should().Be(4);
        result.CellContentBlockId.Should().Be("cell-p1");
        result.SamePage.Should().BeTrue();
        result.HitType.Should().Be("tableCell");
        result.HitCellId.Should().Be("cell2");
        result.HitBlockId.Should().Be("cell-p2");
        result.Overlap.Should().BeFalse();
        result.TextInsideBorders.Should().BeTrue();
        result.Cell1Width.Should().BeGreaterThan(100);
        result.Cell2X.Should().BeGreaterThan(100);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Tables_CommandsMutateRowsColumnsMergeSplitStyleAndResize()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TableCommandProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase14-commands',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'pr1', Text: 'Before' }] } }]
                });
                const dispatcher = engine.commands.createCommandDispatcher(model, {
                    selection: { blockId: 'p1', offset: 6, isCollapsed: true }
                });
                const insert = dispatcher.executeCommand('insertTable', { rows: 2, columns: 2, tableId: 'tbl1' });
                dispatcher.setSelection({ blockId: 'tbl1', cellId: 'tbl1-r0-c0', isCellSelection: true });
                const rowBelow = dispatcher.executeCommand('insertRowBelow');
                const rowAbove = dispatcher.executeCommand('insertRowAbove');
                const columnRight = dispatcher.executeCommand('insertColumnRight');
                const columnLeft = dispatcher.executeCommand('insertColumnLeft');
                const bg = dispatcher.executeCommand('cellBackground', { color: '#fff59d' });
                const border = dispatcher.executeCommand('cellBorder', { border: '2px solid #111827' });
                const merge = dispatcher.executeCommand('mergeCells', { cellIds: ['tbl1-r0-c0', 'tbl1-r0-c1'] });
                const split = dispatcher.executeCommand('splitCell', { cellId: 'tbl1-r0-c0' });
                const deleteRow = dispatcher.executeCommand('deleteRow', { rowIndex: 0 });
                const deleteColumn = dispatcher.executeCommand('deleteColumn', { columnIndex: 0 });
                const resize = dispatcher.executeCommand('resizeTable', { width: 480 });
                const table = model.body.blocks.find(block => block.id === 'tbl1');
                const firstCell = table.content.rows[0].cells[0];
                return {
                    allOk: [insert, rowBelow, rowAbove, columnRight, columnLeft, bg, border, merge, split, deleteRow, deleteColumn, resize].every(item => item.ok === true),
                    tableId: table.id,
                    rowCount: table.content.rows.length,
                    columnCount: table.content.rows[0].cells.length,
                    firstCellBackground: firstCell.style.background,
                    firstCellBorder: firstCell.style.border,
                    firstCellColSpan: firstCell.colSpan,
                    tableWidth: table.content.style.width,
                    operationTypes: dispatcher.getCommittedOperations().map(operation => operation.type),
                    caretCellId: dispatcher.getSelection().cellId
                };
            }
            """);

        result.AllOk.Should().BeTrue();
        result.TableId.Should().Be("tbl1");
        result.RowCount.Should().BeGreaterThanOrEqualTo(2);
        result.ColumnCount.Should().BeGreaterThanOrEqualTo(2);
        result.FirstCellBackground.Should().Be("#fff59d");
        result.FirstCellBorder.Should().Be("2px solid #111827");
        result.FirstCellColSpan.Should().Be(1);
        result.TableWidth.Should().Be(480);
        result.OperationTypes.Should().Contain("InsertTable");
        result.OperationTypes.Should().Contain("UpdateTableCell");
        result.CaretCellId.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_Tables_CellEditingContextMenuResizeAndReloadStayStable()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<TableQualityProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase14-quality',
                    Blocks: [{
                        Id: 'tbl1',
                        Type: 'Table',
                        Content: { Rows: [{ Id: 'row1', Cells: [
                            { Id: 'cell1', Blocks: [{ Id: 'cell-p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'A' }] } }] },
                            { Id: 'cell2', Blocks: [{ Id: 'cell-p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: 'B', Marks: [{ Type: 'Bold' }] }] } }] }
                        ] }] }
                    }]
                });
                const tables = engine.tables.createTableController(model);
                const layoutBefore = engine.textLayout.createParagraphLayoutEngine(null, {
                    pageWidth: 520,
                    pageHeight: 420,
                    margin: { top: 40, right: 40, bottom: 40, left: 40 }
                }).layoutDocument(model);
                const tableLayout = layoutBefore.blocks.find(block => block.blockId === 'tbl1');
                const cell2 = tableLayout.cells.find(cell => cell.cellId === 'cell2');
                const click = tables.hitTest(layoutBefore, cell2.rect.x + 8, cell2.rect.y + 10);
                const typed = tables.insertTextInCell(click.selection, ' typed');
                const dispatcher = engine.commands.createCommandDispatcher(model, { selection: typed.selection });
                const toolbar = dispatcher.getFormattingSnapshot();
                const contextMenu = tables.createContextMenu(click.selection, { viewport: { width: 1280, height: 720 } });
                tables.resizeTable('tbl1', 460);
                const layoutAfterResize = engine.textLayout.createParagraphLayoutEngine(null, {
                    pageWidth: 520,
                    pageHeight: 420,
                    margin: { top: 40, right: 40, bottom: 40, left: 40 }
                }).layoutDocument(model);
                const exported = engine.model.exportToCSharpJson(model);
                const reloaded = engine.model.importFromCSharpJson(exported);
                const layoutReloaded = engine.textLayout.createParagraphLayoutEngine(null, {
                    pageWidth: 520,
                    pageHeight: 420,
                    margin: { top: 40, right: 40, bottom: 40, left: 40 }
                }).layoutDocument(reloaded);
                const resizedTable = layoutAfterResize.blocks.find(block => block.blockId === 'tbl1');
                const hasOverlap = resizedTable.cells.some((a, index) => resizedTable.cells.some((b, other) =>
                    other > index &&
                    a.rect.x < b.rect.x + b.rect.width &&
                    a.rect.x + a.rect.width > b.rect.x &&
                    a.rect.y < b.rect.y + b.rect.height &&
                    a.rect.y + a.rect.height > b.rect.y));
                return {
                    clickType: click.type,
                    caretCellId: click.selection.cellId,
                    typedText: reloaded.body.blocks[0].content.rows[0].cells[1].blocks[0].content.runs.map(run => run.text).join(''),
                    typedSelectionCell: typed.selection.cellId,
                    toolbarBold: toolbar.commandValues.bold,
                    contextMenuReadable: contextMenu.isReadable,
                    contextMenuItems: contextMenu.items.map(item => item.commandId),
                    resizeNoOverlap: !hasOverlap,
                    reloadedWidth: reloaded.body.blocks[0].content.style.width,
                    reloadedCellCount: layoutReloaded.blocks.find(block => block.blockId === 'tbl1').cells.length,
                    selectionStableInCell: typed.selection.blockId === 'cell-p2' && typed.selection.cellId === 'cell2'
                };
            }
            """);

        result.ClickType.Should().Be("tableCell");
        result.CaretCellId.Should().Be("cell2");
        result.TypedText.Should().Contain("typed");
        result.TypedSelectionCell.Should().Be("cell2");
        result.ToolbarBold.Should().BeTrue();
        result.ContextMenuReadable.Should().BeTrue();
        result.ContextMenuItems.Should().Contain(["insertRowBelow", "insertColumnRight", "cellBackground", "cellBorder"]);
        result.ResizeNoOverlap.Should().BeTrue();
        result.ReloadedWidth.Should().Be(460);
        result.ReloadedCellCount.Should().Be(2);
        result.SelectionStableInCell.Should().BeTrue();
    }

    private sealed class TableModelProbe
    {
        [JsonPropertyName("tableId")] public string TableId { get; set; } = string.Empty;
        [JsonPropertyName("rowId")] public string RowId { get; set; } = string.Empty;
        [JsonPropertyName("cellId")] public string CellId { get; set; } = string.Empty;
        [JsonPropertyName("blockId")] public string BlockId { get; set; } = string.Empty;
        [JsonPropertyName("rowSpan")] public int RowSpan { get; set; }
        [JsonPropertyName("colSpan")] public int ColSpan { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("background")] public string Background { get; set; } = string.Empty;
        [JsonPropertyName("border")] public string Border { get; set; } = string.Empty;
        [JsonPropertyName("padding")] public int Padding { get; set; }
        [JsonPropertyName("exportedColSpan")] public int ExportedColSpan { get; set; }
        [JsonPropertyName("exportedBackground")] public string ExportedBackground { get; set; } = string.Empty;
    }

    private sealed class TableLayoutProbe
    {
        [JsonPropertyName("layoutOk")] public bool LayoutOk { get; set; }
        [JsonPropertyName("tableType")] public string TableType { get; set; } = string.Empty;
        [JsonPropertyName("cellCount")] public int CellCount { get; set; }
        [JsonPropertyName("cellContentBlockId")] public string CellContentBlockId { get; set; } = string.Empty;
        [JsonPropertyName("samePage")] public bool SamePage { get; set; }
        [JsonPropertyName("hitType")] public string HitType { get; set; } = string.Empty;
        [JsonPropertyName("hitCellId")] public string HitCellId { get; set; } = string.Empty;
        [JsonPropertyName("hitBlockId")] public string HitBlockId { get; set; } = string.Empty;
        [JsonPropertyName("overlap")] public bool Overlap { get; set; }
        [JsonPropertyName("textInsideBorders")] public bool TextInsideBorders { get; set; }
        [JsonPropertyName("cell1Width")] public double Cell1Width { get; set; }
        [JsonPropertyName("cell2X")] public double Cell2X { get; set; }
    }

    private sealed class TableCommandProbe
    {
        [JsonPropertyName("allOk")] public bool AllOk { get; set; }
        [JsonPropertyName("tableId")] public string TableId { get; set; } = string.Empty;
        [JsonPropertyName("rowCount")] public int RowCount { get; set; }
        [JsonPropertyName("columnCount")] public int ColumnCount { get; set; }
        [JsonPropertyName("firstCellBackground")] public string FirstCellBackground { get; set; } = string.Empty;
        [JsonPropertyName("firstCellBorder")] public string FirstCellBorder { get; set; } = string.Empty;
        [JsonPropertyName("firstCellColSpan")] public int FirstCellColSpan { get; set; }
        [JsonPropertyName("tableWidth")] public int TableWidth { get; set; }
        [JsonPropertyName("operationTypes")] public string[] OperationTypes { get; set; } = [];
        [JsonPropertyName("caretCellId")] public string CaretCellId { get; set; } = string.Empty;
    }

    private sealed class TableQualityProbe
    {
        [JsonPropertyName("clickType")] public string ClickType { get; set; } = string.Empty;
        [JsonPropertyName("caretCellId")] public string CaretCellId { get; set; } = string.Empty;
        [JsonPropertyName("typedText")] public string TypedText { get; set; } = string.Empty;
        [JsonPropertyName("typedSelectionCell")] public string TypedSelectionCell { get; set; } = string.Empty;
        [JsonPropertyName("toolbarBold")] public bool ToolbarBold { get; set; }
        [JsonPropertyName("contextMenuReadable")] public bool ContextMenuReadable { get; set; }
        [JsonPropertyName("contextMenuItems")] public string[] ContextMenuItems { get; set; } = [];
        [JsonPropertyName("resizeNoOverlap")] public bool ResizeNoOverlap { get; set; }
        [JsonPropertyName("reloadedWidth")] public int ReloadedWidth { get; set; }
        [JsonPropertyName("reloadedCellCount")] public int ReloadedCellCount { get; set; }
        [JsonPropertyName("selectionStableInCell")] public bool SelectionStableInCell { get; set; }
    }
}
