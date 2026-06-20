using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

[Collection("SpreadsheetClipboard")]
public class SpreadsheetClipboardTests
{
    [Fact]
    public void Copy_HoldsValuesFormulasStylesAndShape_NotJustText()
    {
        var cells = new Dictionary<string, SpreadsheetCell>
        {
            ["A1"] = new() { Value = 5d, DataType = SpreadsheetDataType.Number, Style = { Bold = true, BackgroundColor = "#FF0000" } },
            ["B1"] = new() { Formula = "=A1*2", Value = 10d },
        };

        SpreadsheetClipboard.Copy(cells, "A1:B1");

        SpreadsheetClipboard.Cells.Should().NotBeNull();
        SpreadsheetClipboard.Cells!.Should().ContainKeys("A1", "B1");
        SpreadsheetClipboard.SourceRangeRef.Should().Be("A1:B1");
        SpreadsheetClipboard.IsCut.Should().BeFalse();

        // Values
        SpreadsheetClipboard.Cells["A1"].Value.Should().Be(5d);
        // Formulas
        SpreadsheetClipboard.Cells["B1"].Formula.Should().Be("=A1*2");
        // Styles
        SpreadsheetClipboard.Cells["A1"].Style.Bold.Should().BeTrue();
        SpreadsheetClipboard.Cells["A1"].Style.BackgroundColor.Should().Be("#FF0000");
    }

    [Fact]
    public void Copy_StoresDefensiveClones()
    {
        var cell = new SpreadsheetCell { Value = "x" };
        var cells = new Dictionary<string, SpreadsheetCell> { ["A1"] = cell };

        SpreadsheetClipboard.Copy(cells, "A1");
        cell.Value = "mutated";

        SpreadsheetClipboard.Cells!["A1"].Value.Should().Be("x");
    }
}
