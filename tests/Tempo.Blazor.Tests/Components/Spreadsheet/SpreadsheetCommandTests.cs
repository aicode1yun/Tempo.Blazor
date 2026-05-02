using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetCommandTests
{
    // ── CommandManager ──

    [Fact]
    public void CommandManager_Execute_AddsToUndoStack()
    {
        var sheet = new SpreadsheetSheet();
        var mgr = new SpreadsheetCommandManager(sheet);
        mgr.CanUndo.Should().BeFalse();

        var cmd = new SetCellValueCommand(sheet, "A1", "hello", null, null);
        mgr.Execute(cmd);

        mgr.CanUndo.Should().BeTrue();
        sheet.Cells["A1"].Value.Should().Be("hello");
    }

    [Fact]
    public void CommandManager_Undo_RestoresPreviousValue()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "original" };
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new SetCellValueCommand(sheet, "A1", "new", null, null));
        sheet.Cells["A1"].Value.Should().Be("new");

        mgr.Undo();
        sheet.Cells["A1"].Value.Should().Be("original");
        mgr.CanUndo.Should().BeFalse();
        mgr.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void CommandManager_Redo_ReappliesValue()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "original" };
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new SetCellValueCommand(sheet, "A1", "new", null, null));
        mgr.Undo();
        sheet.Cells["A1"].Value.Should().Be("original");

        mgr.Redo();
        sheet.Cells["A1"].Value.Should().Be("new");
        mgr.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_NewCommand_ClearsRedoStack()
    {
        var sheet = new SpreadsheetSheet();
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new SetCellValueCommand(sheet, "A1", "first", null, null));
        mgr.Undo();
        mgr.CanRedo.Should().BeTrue();

        mgr.Execute(new SetCellValueCommand(sheet, "A1", "second", null, null));
        mgr.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CommandManager_BatchCommand_UndoAllAtOnce()
    {
        var sheet = new SpreadsheetSheet();
        var mgr = new SpreadsheetCommandManager(sheet);

        var batch = new BatchCommand();
        batch.Add(new SetCellValueCommand(sheet, "A1", "x", null, null));
        batch.Add(new SetCellValueCommand(sheet, "B1", "y", null, null));
        mgr.Execute(batch);

        sheet.Cells["A1"].Value.Should().Be("x");
        sheet.Cells["B1"].Value.Should().Be("y");

        mgr.Undo();
        sheet.Cells.Should().NotContainKey("A1");
        sheet.Cells.Should().NotContainKey("B1");
    }

    // ── SetCellValueCommand ──

    [Fact]
    public void SetCellValueCommand_SetsValue()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new SetCellValueCommand(sheet, "A1", "test", null, null);
        cmd.Execute();
        sheet.Cells["A1"].Value.Should().Be("test");
        sheet.Cells["A1"].Formula.Should().BeNull();
    }

    [Fact]
    public void SetCellValueCommand_SetsFormula()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new SetCellValueCommand(sheet, "A1", null, "=SUM(A2:A10)", null);
        cmd.Execute();
        sheet.Cells["A1"].Formula.Should().Be("=SUM(A2:A10)");
        sheet.Cells["A1"].Value.Should().BeNull();
    }

    [Fact]
    public void SetCellValueCommand_Undo_RestoresValueAndFormula()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 42, Formula = "=A2+1" };
        var cmd = new SetCellValueCommand(sheet, "A1", "new", null, null);
        cmd.Execute();
        cmd.Undo();
        sheet.Cells["A1"].Value.Should().Be(42);
        sheet.Cells["A1"].Formula.Should().Be("=A2+1");
    }

    [Fact]
    public void SetCellValueCommand_Undo_DeletesCellIfNotExisted()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new SetCellValueCommand(sheet, "A1", "temp", null, null);
        cmd.Execute();
        sheet.Cells.ContainsKey("A1").Should().BeTrue();
        cmd.Undo();
        sheet.Cells.ContainsKey("A1").Should().BeFalse();
    }

    // ── SetCellStyleCommand ──

    [Fact]
    public void SetCellStyleCommand_SetsStyle()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new SetCellStyleCommand(sheet, ["A1"], s => s.Bold = true);
        cmd.Execute();
        sheet.Cells["A1"].Style.Bold.Should().BeTrue();
    }

    [Fact]
    public void SetCellStyleCommand_Undo_RestoresStyle()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Style = new SpreadsheetCellStyle { Bold = false, Italic = true } };
        var cmd = new SetCellStyleCommand(sheet, ["A1"], s => s.Bold = true);
        cmd.Execute();
        sheet.Cells["A1"].Style.Bold.Should().BeTrue();
        cmd.Undo();
        sheet.Cells["A1"].Style.Bold.Should().BeFalse();
        sheet.Cells["A1"].Style.Italic.Should().BeTrue();
    }

    // ── MergeCellsCommand / UnmergeCellsCommand ──

    [Fact]
    public void MergeCellsCommand_AddsRange()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new MergeCellsCommand(sheet, 0, 0, 1, 1); // A1:B2
        cmd.Execute();
        sheet.MergedCells.Should().ContainSingle();
        sheet.MergedCells[0].StartRow.Should().Be(0);
        sheet.MergedCells[0].EndRow.Should().Be(1);
    }

    [Fact]
    public void MergeCellsCommand_Undo_RemovesRange()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new MergeCellsCommand(sheet, 0, 0, 1, 1);
        cmd.Execute();
        cmd.Undo();
        sheet.MergedCells.Should().BeEmpty();
    }

    [Fact]
    public void UnmergeCellsCommand_RemovesRange()
    {
        var sheet = new SpreadsheetSheet();
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1));
        var cmd = new UnmergeCellsCommand(sheet, 0, 0, 1, 1);
        cmd.Execute();
        sheet.MergedCells.Should().BeEmpty();
    }

    [Fact]
    public void UnmergeCellsCommand_Undo_RestoresRange()
    {
        var sheet = new SpreadsheetSheet();
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1));
        var cmd = new UnmergeCellsCommand(sheet, 0, 0, 1, 1);
        cmd.Execute();
        cmd.Undo();
        sheet.MergedCells.Should().ContainSingle();
    }

    // ── InsertRowCommand / DeleteRowCommand ──

    [Fact]
    public void InsertRowCommand_InsertsRow()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "top" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "middle" };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = "bottom" };

        var cmd = new InsertRowCommand(sheet, 1); // insert at row 1 (0-based) -> before A2
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("top");
        sheet.Cells.ContainsKey("A2").Should().BeFalse("new empty row has no cells");
        sheet.Cells["A3"].Value.Should().Be("middle");
        sheet.Cells["A4"].Value.Should().Be("bottom");
    }

    [Fact]
    public void InsertRowCommand_Undo_RemovesInsertedRow()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "top" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "middle" };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = "bottom" };

        var cmd = new InsertRowCommand(sheet, 1);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("top");
        sheet.Cells["A2"].Value.Should().Be("middle");
        sheet.Cells["A3"].Value.Should().Be("bottom");
    }

    [Fact]
    public void DeleteRowCommand_DeletesRow()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "top" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "middle" };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = "bottom" };

        var cmd = new DeleteRowCommand(sheet, 1); // delete row 1 (0-based) -> A2
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("top");
        sheet.Cells["A2"].Value.Should().Be("bottom");
        sheet.Cells.ContainsKey("A3").Should().BeFalse();
    }

    [Fact]
    public void DeleteRowCommand_Undo_RestoresRow()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "top" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "middle" };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = "bottom" };

        var cmd = new DeleteRowCommand(sheet, 1);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("top");
        sheet.Cells["A2"].Value.Should().Be("middle");
        sheet.Cells["A3"].Value.Should().Be("bottom");
    }

    // ── CopyCommand / CutCommand / PasteCommand ──

    [Fact]
    public void CopyCommand_CopiesCellsToClipboard()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "world" };

        SpreadsheetClipboard.Clear();
        var cmd = new CopyCommand(sheet, ["A1", "B1"]);
        cmd.Execute();

        SpreadsheetClipboard.Cells.Should().NotBeNull();
        SpreadsheetClipboard.Cells!.Should().ContainKey("A1");
        SpreadsheetClipboard.Cells["A1"].Value.Should().Be("hello");
        SpreadsheetClipboard.IsCut.Should().BeFalse();
    }

    [Fact]
    public void CutCommand_CopiesCellsToClipboardAndRemovesFromSheet()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "world" };

        SpreadsheetClipboard.Clear();
        var cmd = new CutCommand(sheet, ["A1", "B1"]);
        cmd.Execute();

        SpreadsheetClipboard.Cells.Should().NotBeNull();
        SpreadsheetClipboard.IsCut.Should().BeTrue();
        sheet.Cells.ContainsKey("A1").Should().BeFalse();
        sheet.Cells.ContainsKey("B1").Should().BeFalse();
    }

    [Fact]
    public void CutCommand_Undo_RestoresCells()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };

        SpreadsheetClipboard.Clear();
        var cmd = new CutCommand(sheet, ["A1"]);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("hello");
    }

    [Fact]
    public void PasteCommand_PastesCellsAtTarget()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "world" };

        SpreadsheetClipboard.Clear();
        new CopyCommand(sheet, ["A1", "A2"]).Execute();

        var paste = new PasteCommand(sheet, "B1");
        paste.Execute();

        sheet.Cells["B1"].Value.Should().Be("hello");
        sheet.Cells["B2"].Value.Should().Be("world");
    }

    [Fact]
    public void PasteCommand_CutClearsClipboard()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };

        SpreadsheetClipboard.Clear();
        new CutCommand(sheet, ["A1"]).Execute();

        var paste = new PasteCommand(sheet, "B1");
        paste.Execute();

        SpreadsheetClipboard.Cells.Should().BeNull();
        sheet.Cells.ContainsKey("A1").Should().BeFalse();
        sheet.Cells["B1"].Value.Should().Be("hello");
    }

    [Fact]
    public void PasteCommand_Undo_RestoresPreviousCells()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "hello" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "old" };

        SpreadsheetClipboard.Clear();
        new CopyCommand(sheet, ["A1"]).Execute();

        var paste = new PasteCommand(sheet, "B1");
        paste.Execute();
        paste.Undo();

        sheet.Cells["B1"].Value.Should().Be("old");
    }

    // ── DeleteCellsCommand ──

    [Fact]
    public void DeleteCellsCommand_RemovesSelectedCells()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "x" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "y" };

        var cmd = new DeleteCellsCommand(sheet, ["A1", "A2"]);
        cmd.Execute();

        sheet.Cells.ContainsKey("A1").Should().BeFalse();
        sheet.Cells.ContainsKey("A2").Should().BeFalse();
    }

    [Fact]
    public void DeleteCellsCommand_Undo_RestoresCells()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "x" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "y" };

        var cmd = new DeleteCellsCommand(sheet, ["A1", "A2"]);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("x");
        sheet.Cells["A2"].Value.Should().Be("y");
    }

    // ── AddSheetCommand / DeleteSheetCommand / RenameSheetCommand ──

    [Fact]
    public void AddSheetCommand_AddsSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear(); // remove default
        var cmd = new AddSheetCommand(workbook, "NewSheet");
        cmd.Execute();

        workbook.Sheets.Should().HaveCount(1);
        workbook.Sheets[0].Name.Should().Be("NewSheet");
    }

    [Fact]
    public void AddSheetCommand_Undo_RemovesSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear();
        var cmd = new AddSheetCommand(workbook, "NewSheet");
        cmd.Execute();
        cmd.Undo();

        workbook.Sheets.Should().BeEmpty();
    }

    [Fact]
    public void DeleteSheetCommand_RemovesSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear();
        workbook.AddSheet("Sheet1");
        workbook.AddSheet("Sheet2");
        workbook.ActiveSheetIndex = 1;

        var cmd = new DeleteSheetCommand(workbook, 0);
        cmd.Execute();

        workbook.Sheets.Should().HaveCount(1);
        workbook.Sheets[0].Name.Should().Be("Sheet2");
    }

    [Fact]
    public void DeleteSheetCommand_Undo_RestoresSheet()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear();
        workbook.AddSheet("Sheet1");
        workbook.AddSheet("Sheet2");

        var cmd = new DeleteSheetCommand(workbook, 0);
        cmd.Execute();
        cmd.Undo();

        workbook.Sheets.Should().HaveCount(2);
        workbook.Sheets[0].Name.Should().Be("Sheet1");
        workbook.Sheets[1].Name.Should().Be("Sheet2");
    }

    [Fact]
    public void RenameSheetCommand_RenamesSheet()
    {
        var sheet = new SpreadsheetSheet { Name = "Old" };
        var cmd = new RenameSheetCommand(sheet, "New");
        cmd.Execute();

        sheet.Name.Should().Be("New");
    }

    [Fact]
    public void RenameSheetCommand_Undo_RestoresName()
    {
        var sheet = new SpreadsheetSheet { Name = "Old" };
        var cmd = new RenameSheetCommand(sheet, "New");
        cmd.Execute();
        cmd.Undo();

        sheet.Name.Should().Be("Old");
    }

    // ── InsertColumnCommand / DeleteColumnCommand ──

    [Fact]
    public void InsertColumnCommand_InsertsColumn()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "left" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "center" };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = "right" };

        var cmd = new InsertColumnCommand(sheet, 1); // insert at col 1 (0-based) -> before B
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("left");
        sheet.Cells.ContainsKey("B1").Should().BeFalse("new empty col has no cells");
        sheet.Cells["C1"].Value.Should().Be("center");
        sheet.Cells["D1"].Value.Should().Be("right");
    }

    [Fact]
    public void InsertColumnCommand_Undo_RemovesInsertedColumn()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "left" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "center" };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = "right" };

        var cmd = new InsertColumnCommand(sheet, 1);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("left");
        sheet.Cells["B1"].Value.Should().Be("center");
        sheet.Cells["C1"].Value.Should().Be("right");
    }

    [Fact]
    public void DeleteColumnCommand_DeletesColumn()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "left" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "center" };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = "right" };

        var cmd = new DeleteColumnCommand(sheet, 1); // delete col 1 (0-based) -> B
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("left");
        sheet.Cells["B1"].Value.Should().Be("right");
        sheet.Cells.ContainsKey("C1").Should().BeFalse();
    }

    [Fact]
    public void DeleteColumnCommand_Undo_RestoresColumn()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "left" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = "center" };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = "right" };

        var cmd = new DeleteColumnCommand(sheet, 1);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("left");
        sheet.Cells["B1"].Value.Should().Be("center");
        sheet.Cells["C1"].Value.Should().Be("right");
    }
}
