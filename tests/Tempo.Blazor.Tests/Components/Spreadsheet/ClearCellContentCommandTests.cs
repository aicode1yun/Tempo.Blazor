using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class ClearCellContentCommandTests
{
    [Fact]
    public void Execute_ClearsValueButPreservesStyle()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cell = sheet.GetOrCreateCell("A1");
        cell.Value = "Hello";
        cell.Style.Bold = true;

        var cmd = new ClearCellContentCommand(sheet, ["A1"]);
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().BeNull();
        sheet.Cells["A1"].Style.Bold.Should().BeTrue();
    }

    [Fact]
    public void Execute_ClearsFormula()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cell = sheet.GetOrCreateCell("B2");
        cell.Formula = "=1+1";

        var cmd = new ClearCellContentCommand(sheet, ["B2"]);
        cmd.Execute();

        sheet.Cells["B2"].Formula.Should().BeNull();
    }

    [Fact]
    public void Execute_SkipsCellsThatDontExist()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };

        var cmd = new ClearCellContentCommand(sheet, ["C3"]);
        var act = () => cmd.Execute();

        act.Should().NotThrow();
        sheet.Cells.Should().NotContainKey("C3");
    }

    [Fact]
    public void Undo_RestoresValue()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cell = sheet.GetOrCreateCell("A1");
        cell.Value = "Hello";

        var cmd = new ClearCellContentCommand(sheet, ["A1"]);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("Hello");
    }

    [Fact]
    public void Undo_RestoresFormula()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        var cell = sheet.GetOrCreateCell("B2");
        cell.Formula = "=SUM(A1:A10)";

        var cmd = new ClearCellContentCommand(sheet, ["B2"]);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells["B2"].Formula.Should().Be("=SUM(A1:A10)");
    }

    [Fact]
    public void Undo_DoesNotRestoreCellsThatWereAlreadyEmpty()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };

        var cmd = new ClearCellContentCommand(sheet, ["A1"]);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells.Should().NotContainKey("A1");
    }

    [Fact]
    public void Execute_WorksOnMultipleCells()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 3 };
        sheet.GetOrCreateCell("A1").Value = 1.0;
        sheet.GetOrCreateCell("B1").Value = 2.0;

        var cmd = new ClearCellContentCommand(sheet, ["A1", "B1"]);
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().BeNull();
        sheet.Cells["B1"].Value.Should().BeNull();
    }
}
