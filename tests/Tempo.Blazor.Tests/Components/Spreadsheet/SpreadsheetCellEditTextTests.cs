using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetCellEditTextTests
{
    private static CultureInfo En => CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void GetEditText_Null_ReturnsEmpty()
        => SpreadsheetCellEditText.GetEditText(null, En).Should().BeEmpty();

    [Fact]
    public void GetEditText_Formula_ReturnsExpression()
    {
        var cell = new SpreadsheetCell { Formula = "=A1+1" };
        SpreadsheetCellEditText.GetEditText(cell, En).Should().Be("=A1+1");
    }

    [Fact]
    public void GetEditText_Percentage_ReturnsPercentText()
    {
        var cell = new SpreadsheetCell { Value = 0.5, DataType = SpreadsheetDataType.Percentage };
        SpreadsheetCellEditText.GetEditText(cell, En).Should().Be("50%");
    }

    [Fact]
    public void GetEditText_Number_IsCanonical_NoThousands()
    {
        var cell = new SpreadsheetCell { Value = 1234.56, DataType = SpreadsheetDataType.Number };
        cell.Style.NumberFormat = "#,##0.00";
        SpreadsheetCellEditText.GetEditText(cell, En).Should().Be("1234.56");
    }

    [Fact]
    public void GetEditText_Boolean_ReturnsUpper()
    {
        var cell = new SpreadsheetCell { Value = true, DataType = SpreadsheetDataType.Boolean };
        SpreadsheetCellEditText.GetEditText(cell, En).Should().Be("TRUE");
    }

    [Fact]
    public void GetEditText_Date_IsReParseable()
    {
        var cell = new SpreadsheetCell { Value = new DateTime(2024, 2, 1), DataType = SpreadsheetDataType.Date };
        cell.Style.NumberFormat = "m/d/yyyy";
        var text = SpreadsheetCellEditText.GetEditText(cell, En);
        DateTime.Parse(text, En).Date.Should().Be(new DateTime(2024, 2, 1));
    }
}
