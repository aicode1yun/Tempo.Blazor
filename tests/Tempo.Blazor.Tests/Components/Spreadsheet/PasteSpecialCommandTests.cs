using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

[Collection("SpreadsheetClipboard")]
public class PasteSpecialCommandTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Ref(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private static void Num(SpreadsheetSheet sheet, string cellRef, double value)
        => sheet.Cells[cellRef] = new SpreadsheetCell { Value = value, DataType = SpreadsheetDataType.Number };

    private static void CopyToClipboard(params (string Ref, SpreadsheetCell Cell)[] cells)
    {
        var dict = cells.ToDictionary(c => c.Ref, c => c.Cell);
        SpreadsheetClipboard.Copy(dict, $"{cells.Min(c => c.Ref)}:{cells.Max(c => c.Ref)}");
    }

    [Fact]
    public void ValuesOnly_PastesValue_WithoutFormulaOrStyle()
    {
        var sheet = new SpreadsheetSheet();
        CopyToClipboard(("A1", new SpreadsheetCell { Formula = "=1+1", Value = 2d, Style = { Bold = true } }));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.Values }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Value.Should().Be(2d);
        sheet.Cells["C3"].Formula.Should().BeNull();
        sheet.Cells["C3"].Style.Bold.Should().BeFalse();
    }

    [Fact]
    public void FormulasOnly_PastesFormula_ShiftedByOffset()
    {
        var sheet = new SpreadsheetSheet();
        CopyToClipboard(("B2", new SpreadsheetCell { Formula = "=A2*2" }));

        // Paste to C3 → offset (+1 row, +1 col): A2 → B3.
        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.Formulas }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Formula.Should().Be("=B3*2");
    }

    [Fact]
    public void FormatsOnly_KeepsTargetValue_AppliesStyle()
    {
        var sheet = new SpreadsheetSheet();
        Num(sheet, "C3", 99);
        CopyToClipboard(("A1", new SpreadsheetCell { Value = 1d, Style = { Bold = true, BackgroundColor = "#00FF00" } }));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.Formats }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Value.Should().Be(99d); // value untouched
        sheet.Cells["C3"].Style.Bold.Should().BeTrue();
        sheet.Cells["C3"].Style.BackgroundColor.Should().Be("#00FF00");
    }

    [Fact]
    public void ValuesAndFormats_PastesBoth_NoFormula()
    {
        var sheet = new SpreadsheetSheet();
        CopyToClipboard(("A1", new SpreadsheetCell { Formula = "=5", Value = 5d, Style = { Bold = true } }));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.ValuesAndFormats }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Value.Should().Be(5d);
        sheet.Cells["C3"].Formula.Should().BeNull();
        sheet.Cells["C3"].Style.Bold.Should().BeTrue();
    }

    [Fact]
    public void AllExceptBorders_CopiesStyleButClearsBorders()
    {
        var sheet = new SpreadsheetSheet();
        var src = new SpreadsheetCell { Value = 1d, Style = { Bold = true } };
        src.Style.BorderTop = new SpreadsheetBorder(SpreadsheetBorderStyle.Thick, "#000000");
        CopyToClipboard(("A1", src));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.AllExceptBorders }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Style.Bold.Should().BeTrue();
        sheet.Cells["C3"].Style.BorderTop.Style.Should().Be(SpreadsheetBorderStyle.None);
    }

    [Fact]
    public void Transpose_SwapsRowsAndColumns()
    {
        var sheet = new SpreadsheetSheet();
        // Source row A1,B1,C1 → transposed into a column at C3,C4,C5.
        CopyToClipboard(
            ("A1", new SpreadsheetCell { Value = 1d, DataType = SpreadsheetDataType.Number }),
            ("B1", new SpreadsheetCell { Value = 2d, DataType = SpreadsheetDataType.Number }),
            ("C1", new SpreadsheetCell { Value = 3d, DataType = SpreadsheetDataType.Number }));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.All, Transpose = true }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Value.Should().Be(1d);
        sheet.Cells["C4"].Value.Should().Be(2d);
        sheet.Cells["C5"].Value.Should().Be(3d);
    }

    [Theory]
    [InlineData(SpreadsheetPasteOperation.Add, 13d)]
    [InlineData(SpreadsheetPasteOperation.Subtract, 7d)]
    [InlineData(SpreadsheetPasteOperation.Multiply, 30d)]
    [InlineData(SpreadsheetPasteOperation.Divide, 10d / 3d)]
    public void Operation_CombinesSourceWithTarget(SpreadsheetPasteOperation op, double expected)
    {
        var sheet = new SpreadsheetSheet();
        Num(sheet, "C3", 10); // target
        CopyToClipboard(("A1", new SpreadsheetCell { Value = 3d, DataType = SpreadsheetDataType.Number }));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.Values, Operation = op }, Culture);
        cmd.Execute();

        ((double)sheet.Cells["C3"].Value!).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void SkipBlanks_DoesNotOverwriteTargetWithBlankSource()
    {
        var sheet = new SpreadsheetSheet();
        Num(sheet, "C3", 1);
        Num(sheet, "C4", 2);
        CopyToClipboard(
            ("A1", new SpreadsheetCell { Value = 9d, DataType = SpreadsheetDataType.Number }),
            ("A2", new SpreadsheetCell())); // blank source

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.Values, SkipBlanks = true }, Culture);
        cmd.Execute();

        sheet.Cells["C3"].Value.Should().Be(9d); // overwritten
        sheet.Cells["C4"].Value.Should().Be(2d); // preserved (blank skipped)
    }

    [Fact]
    public void Undo_RestoresOverwrittenCells()
    {
        var sheet = new SpreadsheetSheet();
        Num(sheet, "C3", 100);
        CopyToClipboard(("A1", new SpreadsheetCell { Value = 5d, DataType = SpreadsheetDataType.Number }));

        var cmd = new PasteSpecialCommand(sheet, "C3",
            new SpreadsheetPasteSpecialOptions { Content = SpreadsheetPasteContent.Values }, Culture);
        cmd.Execute();
        sheet.Cells["C3"].Value.Should().Be(5d);

        cmd.Undo();
        sheet.Cells["C3"].Value.Should().Be(100d);
    }
}
