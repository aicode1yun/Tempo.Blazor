using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetCellModelTests
{
    [Fact]
    public void Cell_DefaultValues_AreCorrect()
    {
        var cell = new SpreadsheetCell();

        cell.Value.Should().BeNull();
        cell.Formula.Should().BeNull();
        cell.DisplayValue.Should().BeNull();
        cell.DataType.Should().Be(SpreadsheetDataType.Text);
        cell.IsReadOnly.Should().BeFalse();
        cell.Style.Should().NotBeNull();
    }

    [Fact]
    public void Cell_SetValue_UpdatesCorrectly()
    {
        var cell = new SpreadsheetCell();

        cell.Value = 42;
        cell.Value.Should().Be(42);
    }

    [Fact]
    public void Cell_SetFormula_UpdatesCorrectly()
    {
        var cell = new SpreadsheetCell();

        cell.Formula = "=SUM(A1:A10)";
        cell.Formula.Should().Be("=SUM(A1:A10)");
    }

    [Fact]
    public void Cell_SetStyle_UpdatesCorrectly()
    {
        var cell = new SpreadsheetCell
        {
            Style = new SpreadsheetCellStyle
            {
                Bold = true,
                FontSize = 14,
                ForeColor = "#FF0000"
            }
        };

        cell.Style.Bold.Should().BeTrue();
        cell.Style.FontSize.Should().Be(14);
        cell.Style.ForeColor.Should().Be("#FF0000");
    }

    [Fact]
    public void Cell_SetDataType_UpdatesCorrectly()
    {
        var cell = new SpreadsheetCell { DataType = SpreadsheetDataType.Number };

        cell.DataType.Should().Be(SpreadsheetDataType.Number);
    }

    [Fact]
    public void Cell_Clone_CreatesIndependentCopy()
    {
        var original = new SpreadsheetCell
        {
            Value = 100,
            Formula = "=A1+B1",
            DataType = SpreadsheetDataType.Number,
            Style = new SpreadsheetCellStyle { Bold = true, FontSize = 16 }
        };

        var clone = original.Clone();

        clone.Value.Should().Be(100);
        clone.Formula.Should().Be("=A1+B1");
        clone.DataType.Should().Be(SpreadsheetDataType.Number);
        clone.Style.Bold.Should().BeTrue();
        clone.Style.FontSize.Should().Be(16);

        // Modify clone and verify original is unaffected
        clone.Value = 200;
        clone.Style.Bold = false;

        original.Value.Should().Be(100);
        original.Style.Bold.Should().BeTrue();
    }

    [Fact]
    public void CellStyle_DefaultValues_AreCorrect()
    {
        var style = new SpreadsheetCellStyle();

        style.FontFamily.Should().Be("Calibri");
        style.FontSize.Should().Be(11);
        style.Bold.Should().BeFalse();
        style.Italic.Should().BeFalse();
        style.Underline.Should().BeFalse();
        style.ForeColor.Should().Be("#000000");
        style.BackgroundColor.Should().Be("transparent");
        style.HorizontalAlign.Should().Be(SpreadsheetHorizontalAlign.General);
        style.VerticalAlign.Should().Be(SpreadsheetVerticalAlign.Bottom);
        style.TextWrap.Should().BeFalse();
        style.NumberFormat.Should().Be("General");
        style.BorderTop.Style.Should().Be(SpreadsheetBorderStyle.None);
        style.BorderRight.Style.Should().Be(SpreadsheetBorderStyle.None);
        style.BorderBottom.Style.Should().Be(SpreadsheetBorderStyle.None);
        style.BorderLeft.Style.Should().Be(SpreadsheetBorderStyle.None);
    }

    [Fact]
    public void CellStyle_Clone_CreatesIndependentCopy()
    {
        var original = new SpreadsheetCellStyle
        {
            FontFamily = "Arial",
            FontSize = 14,
            Bold = true,
            ForeColor = "#3366FF",
            BorderTop = new SpreadsheetBorder(SpreadsheetBorderStyle.Thin, "#000000")
        };

        var clone = original.Clone();

        clone.FontFamily.Should().Be("Arial");
        clone.Bold.Should().BeTrue();
        clone.BorderTop.Style.Should().Be(SpreadsheetBorderStyle.Thin);

        clone.FontFamily = "Times New Roman";
        clone.BorderTop.Style = SpreadsheetBorderStyle.Thick;

        original.FontFamily.Should().Be("Arial");
        original.BorderTop.Style.Should().Be(SpreadsheetBorderStyle.Thin);
    }

    [Fact]
    public void Border_Constructor_SetsProperties()
    {
        var border = new SpreadsheetBorder(SpreadsheetBorderStyle.Medium, "#FF0000");

        border.Style.Should().Be(SpreadsheetBorderStyle.Medium);
        border.Color.Should().Be("#FF0000");
    }
}
