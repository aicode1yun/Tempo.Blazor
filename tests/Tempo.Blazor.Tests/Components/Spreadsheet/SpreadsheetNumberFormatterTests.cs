using Tempo.Blazor.Components.Spreadsheet.Format;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetNumberFormatterTests
{
    [Theory]
    [InlineData(null, "General", "")]
    [InlineData("", "General", "")]
    [InlineData("Hello", "General", "Hello")]
    [InlineData(42, "General", "42")]
    [InlineData(3.14159, "General", "3.14159")]
    public void Format_General_ReturnsExpected(object? value, string format, string expected)
    {
        SpreadsheetNumberFormatter.Format(value, format).Should().Be(expected);
    }

    [Theory]
    [InlineData(42.0, "0", "42")]
    [InlineData(3.14159, "0.00", "3.14")]
    [InlineData(1234.5, "#,##0", "1,234")]
    [InlineData(1234.5, "#,##0.00", "1,234.50")]
    public void Format_Number_ReturnsExpected(object? value, string format, string expected)
    {
        SpreadsheetNumberFormatter.Format(value, format).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.15, "0%", "15%")]
    [InlineData(0.1555, "0.00%", "15.55%")]
    public void Format_Percentage_ReturnsExpected(object? value, string format, string expected)
    {
        SpreadsheetNumberFormatter.Format(value, format).Should().Be(expected);
    }

    [Theory]
    [InlineData(1234.5, "$#,##0.00", "$1,234.50")]
    [InlineData(99.9, "€#,##0.00", "€99.90")]
    public void Format_Currency_ReturnsExpected(object? value, string format, string expected)
    {
        SpreadsheetNumberFormatter.Format(value, format).Should().Be(expected);
    }

    [Theory]
    [InlineData("Hello", "@", "Hello")]
    [InlineData(42, "@", "42")]
    public void Format_Text_ReturnsExpected(object? value, string format, string expected)
    {
        SpreadsheetNumberFormatter.Format(value, format).Should().Be(expected);
    }

    [Fact]
    public void Format_Date_FromDateTime_ReturnsFormatted()
    {
        var dt = new DateTime(2024, 6, 15);
        SpreadsheetNumberFormatter.Format(dt, "yyyy-MM-dd").Should().Be("2024-06-15");
    }

    [Fact]
    public void Format_Date_FromExcelSerial_ReturnsFormatted()
    {
        // Excel serial 45458 = 2024-06-15 (since 1899-12-30)
        SpreadsheetNumberFormatter.Format(45458.0, "yyyy-MM-dd").Should().Be("2024-06-15");
    }

    [Fact]
    public void Format_Time_ReturnsFormatted()
    {
        var dt = new DateTime(2024, 6, 15, 14, 30, 0);
        SpreadsheetNumberFormatter.Format(dt, "HH:mm").Should().Be("14:30");
    }

    [Fact]
    public void Format_Scientific_ReturnsFormatted()
    {
        SpreadsheetNumberFormatter.Format(12345.0, "0.00E+00").Should().Contain("E");
    }

    [Fact]
    public void Format_NumberWithThousandsSeparator_AddsCommas()
    {
        SpreadsheetNumberFormatter.Format(1234567.89, "#,##0.00").Should().Be("1,234,567.89");
    }

    [Fact]
    public void Format_NonNumericValue_WithNumberFormat_ReturnsStringRepresentation()
    {
        SpreadsheetNumberFormatter.Format("abc", "0.00").Should().Be("abc");
    }
}
