using Tempo.Blazor.Components.Spreadsheet.AutoFill;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetAutoFillTests
{
    [Fact]
    public void AutoFill_NumberIncrement()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 1.0 };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 2.0 };

        var engine = new SpreadsheetAutoFillEngine(sheet);
        engine.Fill("A1:A2", "A1:A5");

        sheet.Cells["A3"].Value.Should().Be(3.0);
        sheet.Cells["A4"].Value.Should().Be(4.0);
        sheet.Cells["A5"].Value.Should().Be(5.0);
    }

    [Fact]
    public void AutoFill_NumberDecrement()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 10.0 };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 8.0 };

        var engine = new SpreadsheetAutoFillEngine(sheet);
        engine.Fill("A1:A2", "A1:A5");

        sheet.Cells["A3"].Value.Should().Be(6.0);
        sheet.Cells["A4"].Value.Should().Be(4.0);
        sheet.Cells["A5"].Value.Should().Be(2.0);
    }

    [Fact]
    public void AutoFill_DateIncrement()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 45458.0 }; // 2024-06-15
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 45459.0 }; // 2024-06-16

        var engine = new SpreadsheetAutoFillEngine(sheet);
        engine.Fill("A1:A2", "A1:A5");

        sheet.Cells["A3"].Value.Should().Be(45460.0);
        sheet.Cells["A4"].Value.Should().Be(45461.0);
        sheet.Cells["A5"].Value.Should().Be(45462.0);
    }

    [Fact]
    public void AutoFill_TextWithNumber()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Item1" };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "Item2" };

        var engine = new SpreadsheetAutoFillEngine(sheet);
        engine.Fill("A1:A2", "A1:A5");

        sheet.Cells["A3"].Value.Should().Be("Item3");
        sheet.Cells["A4"].Value.Should().Be("Item4");
        sheet.Cells["A5"].Value.Should().Be("Item5");
    }

    [Fact]
    public void AutoFill_SingleValueRepeat()
    {
        var sheet = new SpreadsheetSheet { RowCount = 5, ColumnCount = 3 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "X" };

        var engine = new SpreadsheetAutoFillEngine(sheet);
        engine.Fill("A1:A1", "A1:A5");

        sheet.Cells["A2"].Value.Should().Be("X");
        sheet.Cells["A3"].Value.Should().Be("X");
        sheet.Cells["A4"].Value.Should().Be("X");
        sheet.Cells["A5"].Value.Should().Be("X");
    }

    [Fact]
    public void AutoFill_Horizontal()
    {
        var sheet = new SpreadsheetSheet { RowCount = 3, ColumnCount = 5 };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 1.0 };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = 2.0 };

        var engine = new SpreadsheetAutoFillEngine(sheet);
        engine.Fill("A1:B1", "A1:E1");

        sheet.Cells["C1"].Value.Should().Be(3.0);
        sheet.Cells["D1"].Value.Should().Be(4.0);
        sheet.Cells["E1"].Value.Should().Be(5.0);
    }
}
