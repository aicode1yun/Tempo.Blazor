using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetResizeCommandTests
{
    // ── ResizeColumnCommand ──

    [Fact]
    public void ResizeColumnCommand_Execute_SetsColumnWidth()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new ResizeColumnCommand(sheet, 2, 120.0);

        cmd.Execute();

        sheet.Columns[2].Width.Should().Be(120.0);
    }

    [Fact]
    public void ResizeColumnCommand_Execute_CreatesColumnEntryIfMissing()
    {
        var sheet = new SpreadsheetSheet();

        new ResizeColumnCommand(sheet, 5, 80.0).Execute();

        sheet.Columns.Should().ContainKey(5);
        sheet.Columns[5].Width.Should().Be(80.0);
    }

    [Fact]
    public void ResizeColumnCommand_Undo_RestoresPreviousWidth()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Columns[1] = new SpreadsheetColumn { Index = 1, Width = 64.0 };
        var cmd = new ResizeColumnCommand(sheet, 1, 150.0);

        cmd.Execute();
        sheet.Columns[1].Width.Should().Be(150.0);

        cmd.Undo();
        sheet.Columns[1].Width.Should().Be(64.0);
    }

    [Fact]
    public void ResizeColumnCommand_Undo_ClearsWidthWhenColumnDidNotExist()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new ResizeColumnCommand(sheet, 3, 100.0);

        cmd.Execute();
        sheet.Columns[3].Width.Should().Be(100.0);

        cmd.Undo();
        sheet.Columns[3].Width.Should().BeNull();
    }

    [Fact]
    public void ResizeColumnCommand_Undo_DoesNothingWhenNeitherExistedNorCreated()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new ResizeColumnCommand(sheet, 9, 80.0);
        // Execute and undo – then undo again should not throw
        cmd.Execute();
        cmd.Undo();
        sheet.Columns[9].Width.Should().BeNull();
    }

    // ── ResizeRowCommand ──

    [Fact]
    public void ResizeRowCommand_Execute_SetsRowHeight()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new ResizeRowCommand(sheet, 0, 40.0);

        cmd.Execute();

        sheet.Rows[0].Height.Should().Be(40.0);
    }

    [Fact]
    public void ResizeRowCommand_Execute_CreatesRowEntryIfMissing()
    {
        var sheet = new SpreadsheetSheet();

        new ResizeRowCommand(sheet, 7, 30.0).Execute();

        sheet.Rows.Should().ContainKey(7);
        sheet.Rows[7].Height.Should().Be(30.0);
    }

    [Fact]
    public void ResizeRowCommand_Undo_RestoresPreviousHeight()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Rows[2] = new SpreadsheetRow { Index = 2, Height = 20.0 };
        var cmd = new ResizeRowCommand(sheet, 2, 60.0);

        cmd.Execute();
        sheet.Rows[2].Height.Should().Be(60.0);

        cmd.Undo();
        sheet.Rows[2].Height.Should().Be(20.0);
    }

    [Fact]
    public void ResizeRowCommand_Undo_ClearsHeightWhenRowDidNotExist()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new ResizeRowCommand(sheet, 4, 50.0);

        cmd.Execute();
        sheet.Rows[4].Height.Should().Be(50.0);

        cmd.Undo();
        sheet.Rows[4].Height.Should().BeNull();
    }

    // ── CommandManager integration ──

    [Fact]
    public void CommandManager_ResizeColumn_CanUndoRedo()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Columns[0] = new SpreadsheetColumn { Index = 0, Width = 64.0 };
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new ResizeColumnCommand(sheet, 0, 200.0));
        sheet.Columns[0].Width.Should().Be(200.0);
        mgr.CanUndo.Should().BeTrue();

        mgr.Undo();
        sheet.Columns[0].Width.Should().Be(64.0);
        mgr.CanRedo.Should().BeTrue();

        mgr.Redo();
        sheet.Columns[0].Width.Should().Be(200.0);
    }

    [Fact]
    public void CommandManager_ResizeRow_CanUndoRedo()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Rows[0] = new SpreadsheetRow { Index = 0, Height = 20.0 };
        var mgr = new SpreadsheetCommandManager(sheet);

        mgr.Execute(new ResizeRowCommand(sheet, 0, 80.0));
        sheet.Rows[0].Height.Should().Be(80.0);
        mgr.CanUndo.Should().BeTrue();

        mgr.Undo();
        sheet.Rows[0].Height.Should().Be(20.0);

        mgr.Redo();
        sheet.Rows[0].Height.Should().Be(80.0);
    }
}
