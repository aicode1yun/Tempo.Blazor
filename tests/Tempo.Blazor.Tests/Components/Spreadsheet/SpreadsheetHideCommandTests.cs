using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetHideCommandTests
{
    // ── HideRowsCommand ──

    [Fact]
    public void HideRowsCommand_Execute_SetsIsHiddenTrue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5 };
        var cmd = new HideRowsCommand(sheet, [1, 2]);

        cmd.Execute();

        sheet.Rows[1].IsHidden.Should().BeTrue();
        sheet.Rows[2].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void HideRowsCommand_Execute_CreatesRowEntryIfMissing()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5 };
        var cmd = new HideRowsCommand(sheet, [3]);

        cmd.Execute();

        sheet.Rows.Should().ContainKey(3);
        sheet.Rows[3].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void HideRowsCommand_Undo_RestoresPreviousHiddenState()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5 };
        sheet.Rows[0] = new SpreadsheetRow { Index = 0, IsHidden = false };
        var cmd = new HideRowsCommand(sheet, [0]);

        cmd.Execute();
        sheet.Rows[0].IsHidden.Should().BeTrue();

        cmd.Undo();
        sheet.Rows[0].IsHidden.Should().BeFalse();
    }

    [Fact]
    public void HideRowsCommand_WithHiddenFalse_Unhides()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5 };
        sheet.Rows[1] = new SpreadsheetRow { Index = 1, IsHidden = true };
        var cmd = new HideRowsCommand(sheet, [1], hidden: false);

        cmd.Execute();

        sheet.Rows[1].IsHidden.Should().BeFalse();
    }

    [Fact]
    public void HideRowsCommand_Undo_RestoresWhenPreviouslyHidden()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5 };
        sheet.Rows[2] = new SpreadsheetRow { Index = 2, IsHidden = true };
        var cmd = new HideRowsCommand(sheet, [2], hidden: false);

        cmd.Execute();
        sheet.Rows[2].IsHidden.Should().BeFalse();

        cmd.Undo();
        sheet.Rows[2].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void HideRowsCommand_Execute_HidesMultipleRows()
    {
        var sheet = new SpreadsheetSheet { RowCount = 10 };
        var cmd = new HideRowsCommand(sheet, [0, 1, 2, 3]);

        cmd.Execute();

        for (int i = 0; i <= 3; i++)
            sheet.Rows[i].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void HideRowsCommand_CommandManager_CanUndoRedo()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5 };
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new HideRowsCommand(sheet, [0]));
        sheet.Rows[0].IsHidden.Should().BeTrue();
        mgr.CanUndo.Should().BeTrue();

        mgr.Undo();
        sheet.Rows[0].IsHidden.Should().BeFalse();
        mgr.CanRedo.Should().BeTrue();

        mgr.Redo();
        sheet.Rows[0].IsHidden.Should().BeTrue();
    }

    // ── HideColumnsCommand ──

    [Fact]
    public void HideColumnsCommand_Execute_SetsIsHiddenTrue()
    {
        var sheet = new SpreadsheetSheet { ColumnCount = 5 };
        var cmd = new HideColumnsCommand(sheet, [1, 2]);

        cmd.Execute();

        sheet.Columns[1].IsHidden.Should().BeTrue();
        sheet.Columns[2].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void HideColumnsCommand_Execute_CreatesColumnEntryIfMissing()
    {
        var sheet = new SpreadsheetSheet { ColumnCount = 5 };
        var cmd = new HideColumnsCommand(sheet, [3]);

        cmd.Execute();

        sheet.Columns.Should().ContainKey(3);
        sheet.Columns[3].IsHidden.Should().BeTrue();
    }

    [Fact]
    public void HideColumnsCommand_Undo_RestoresPreviousHiddenState()
    {
        var sheet = new SpreadsheetSheet { ColumnCount = 5 };
        sheet.Columns[0] = new SpreadsheetColumn { Index = 0, IsHidden = false };
        var cmd = new HideColumnsCommand(sheet, [0]);

        cmd.Execute();
        sheet.Columns[0].IsHidden.Should().BeTrue();

        cmd.Undo();
        sheet.Columns[0].IsHidden.Should().BeFalse();
    }

    [Fact]
    public void HideColumnsCommand_WithHiddenFalse_Unhides()
    {
        var sheet = new SpreadsheetSheet { ColumnCount = 5 };
        sheet.Columns[1] = new SpreadsheetColumn { Index = 1, IsHidden = true };
        var cmd = new HideColumnsCommand(sheet, [1], hidden: false);

        cmd.Execute();

        sheet.Columns[1].IsHidden.Should().BeFalse();
    }

    [Fact]
    public void HideColumnsCommand_CommandManager_CanUndoRedo()
    {
        var sheet = new SpreadsheetSheet { ColumnCount = 5 };
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new HideColumnsCommand(sheet, [0]));
        sheet.Columns[0].IsHidden.Should().BeTrue();
        mgr.CanUndo.Should().BeTrue();

        mgr.Undo();
        sheet.Columns[0].IsHidden.Should().BeFalse();

        mgr.Redo();
        sheet.Columns[0].IsHidden.Should().BeTrue();
    }
}
