using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Format;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetValueParserTests
{
    private static CultureInfo En => CultureInfo.GetCultureInfo("en-US");
    private static CultureInfo Cs => CultureInfo.GetCultureInfo("cs-CZ");

    // ── 1.1 Data type ──────────────────────────────────────────────
    [Fact]
    public void DataType_HasPercentageAndCurrency()
    {
        Enum.IsDefined(SpreadsheetDataType.Percentage).Should().BeTrue();
        Enum.IsDefined(SpreadsheetDataType.Currency).Should().BeTrue();
    }

    // ── 1.3 Null / empty ───────────────────────────────────────────
    [Fact]
    public void Parse_Null_ReturnsEmptyText()
    {
        var r = SpreadsheetValueParser.Parse(null, En);
        r.Type.Should().Be(SpreadsheetDataType.Text);
        r.Value.Should().BeNull();
        r.Formula.Should().BeNull();
    }

    [Fact]
    public void Parse_Empty_ReturnsText()
    {
        SpreadsheetValueParser.Parse("", En).Type.Should().Be(SpreadsheetDataType.Text);
    }

    // ── 1.4 Formula ────────────────────────────────────────────────
    [Fact]
    public void Parse_StartsWithEquals_ReturnsFormula()
    {
        var r = SpreadsheetValueParser.Parse("=A1+1", En);
        r.Formula.Should().Be("=A1+1");
        r.Value.Should().BeNull();
    }

    [Fact]
    public void Parse_LoneEquals_IsText()
    {
        SpreadsheetValueParser.Parse("=", En).Type.Should().Be(SpreadsheetDataType.Text);
    }

    // ── 1.5 Forced text ────────────────────────────────────────────
    [Fact]
    public void Parse_LeadingApostrophe_ForcesText_KeepsLeadingZeros()
    {
        var r = SpreadsheetValueParser.Parse("'0123", En);
        r.Type.Should().Be(SpreadsheetDataType.Text);
        r.Value.Should().Be("0123");
        r.IsForcedText.Should().BeTrue();
    }

    [Fact]
    public void Parse_LeadingApostrophe_FormulaLikeStaysText()
    {
        var r = SpreadsheetValueParser.Parse("'=SUM(A1)", En);
        r.Type.Should().Be(SpreadsheetDataType.Text);
        r.Value.Should().Be("=SUM(A1)");
        r.Formula.Should().BeNull();
    }

    // ── 1.6 Boolean ────────────────────────────────────────────────
    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("true", true)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void Parse_Boolean_CaseInsensitive(string input, bool expected)
    {
        var r = SpreadsheetValueParser.Parse(input, En);
        r.Type.Should().Be(SpreadsheetDataType.Boolean);
        r.Value.Should().Be(expected);
    }

    // ── 1.7 Percentage ─────────────────────────────────────────────
    [Fact]
    public void Parse_Percentage_StoresFraction()
    {
        var r = SpreadsheetValueParser.Parse("50%", En);
        r.Type.Should().Be(SpreadsheetDataType.Percentage);
        r.Value.Should().Be(0.5);
        r.ImpliedNumberFormat.Should().Be("0%");
    }

    [Theory]
    [InlineData("12.5%")]
    public void Parse_Percentage_Decimals_Invariant(string input)
    {
        var r = SpreadsheetValueParser.Parse(input, En);
        r.Type.Should().Be(SpreadsheetDataType.Percentage);
        ((double)r.Value!).Should().BeApproximately(0.125, 1e-9);
        r.ImpliedNumberFormat.Should().Be("0.0%");
    }

    [Fact]
    public void Parse_Percentage_Decimals_Czech()
    {
        var r = SpreadsheetValueParser.Parse("12,5%", Cs);
        r.Type.Should().Be(SpreadsheetDataType.Percentage);
        ((double)r.Value!).Should().BeApproximately(0.125, 1e-9);
        r.ImpliedNumberFormat.Should().Be("0.0%");
    }

    // ── 1.8 Number ─────────────────────────────────────────────────
    [Fact]
    public void Parse_Integer()
    {
        var r = SpreadsheetValueParser.Parse("123", En);
        r.Type.Should().Be(SpreadsheetDataType.Number);
        r.Value.Should().Be(123.0);
        r.ImpliedNumberFormat.Should().BeNull();
    }

    [Fact]
    public void Parse_Decimal_Invariant()
    {
        SpreadsheetValueParser.Parse("1234.56", En).Value.Should().Be(1234.56);
    }

    [Fact]
    public void Parse_Decimal_Czech()
    {
        var r = SpreadsheetValueParser.Parse("1234,56", Cs);
        r.Type.Should().Be(SpreadsheetDataType.Number);
        ((double)r.Value!).Should().BeApproximately(1234.56, 1e-9);
    }

    [Fact]
    public void Parse_Thousands_Czech()
    {
        var r = SpreadsheetValueParser.Parse("1 234,56", Cs);
        r.Type.Should().Be(SpreadsheetDataType.Number);
        ((double)r.Value!).Should().BeApproximately(1234.56, 1e-9);
        r.ImpliedNumberFormat.Should().Be("#,##0.00");
    }

    [Fact]
    public void Parse_Thousands_Invariant()
    {
        var r = SpreadsheetValueParser.Parse("1,234.56", En);
        ((double)r.Value!).Should().BeApproximately(1234.56, 1e-9);
        r.ImpliedNumberFormat.Should().Be("#,##0.00");
    }

    [Theory]
    [InlineData("-42", -42.0)]
    [InlineData("+42", 42.0)]
    public void Parse_Signed(string input, double expected)
    {
        SpreadsheetValueParser.Parse(input, En).Value.Should().Be(expected);
    }

    [Fact]
    public void Parse_ScientificNotation()
    {
        SpreadsheetValueParser.Parse("1.5E3", En).Value.Should().Be(1500.0);
    }

    [Fact]
    public void Parse_AmbiguousDotDecimal_StaysNumber_InCzech()
    {
        var r = SpreadsheetValueParser.Parse("1.5", Cs);
        r.Type.Should().Be(SpreadsheetDataType.Number);
        ((double)r.Value!).Should().BeApproximately(1.5, 1e-9);
    }

    // ── 1.9 Currency ───────────────────────────────────────────────
    [Fact]
    public void Parse_Currency_Dollar()
    {
        var r = SpreadsheetValueParser.Parse("$10", En);
        r.Type.Should().Be(SpreadsheetDataType.Currency);
        r.Value.Should().Be(10.0);
        r.ImpliedNumberFormat.Should().StartWith("$");
    }

    [Fact]
    public void Parse_Currency_DollarWithThousands()
    {
        var r = SpreadsheetValueParser.Parse("$1,234.50", En);
        r.Type.Should().Be(SpreadsheetDataType.Currency);
        ((double)r.Value!).Should().BeApproximately(1234.50, 1e-9);
    }

    [Fact]
    public void Parse_Currency_Czech()
    {
        var r = SpreadsheetValueParser.Parse("1 500 Kč", Cs);
        r.Type.Should().Be(SpreadsheetDataType.Currency);
        ((double)r.Value!).Should().BeApproximately(1500, 1e-9);
        r.ImpliedNumberFormat.Should().Contain("Kč");
    }

    // ── 1.10 Date / time ───────────────────────────────────────────
    [Fact]
    public void Parse_Date_Czech()
    {
        var r = SpreadsheetValueParser.Parse("1.2.2024", Cs);
        r.Type.Should().Be(SpreadsheetDataType.Date);
        r.Value.Should().Be(new DateTime(2024, 2, 1));
    }

    [Fact]
    public void Parse_Date_Iso()
    {
        var r = SpreadsheetValueParser.Parse("2024-01-31", En);
        r.Type.Should().Be(SpreadsheetDataType.Date);
        r.Value.Should().Be(new DateTime(2024, 1, 31));
    }

    [Fact]
    public void Parse_Time()
    {
        var r = SpreadsheetValueParser.Parse("12:30", En);
        r.Type.Should().Be(SpreadsheetDataType.Time);
        ((DateTime)r.Value!).TimeOfDay.Should().Be(new TimeSpan(12, 30, 0));
    }

    [Fact]
    public void Parse_DateTime_Czech()
    {
        var r = SpreadsheetValueParser.Parse("1.2.2024 12:30", Cs);
        r.Type.Should().Be(SpreadsheetDataType.DateTime);
        r.Value.Should().Be(new DateTime(2024, 2, 1, 12, 30, 0));
    }

    // ── 1.11 Round-trip via SpreadsheetNumberFormatter ─────────────
    [Fact]
    public void RoundTrip_Number_Thousands()
    {
        var r = SpreadsheetValueParser.Parse("1 234,56", Cs);
        SpreadsheetNumberFormatter.Format(r.Value, r.ImpliedNumberFormat!).Should().Be("1,234.56");
    }

    [Fact]
    public void RoundTrip_Percentage()
    {
        var r = SpreadsheetValueParser.Parse("50%", En);
        SpreadsheetNumberFormatter.Format(r.Value, r.ImpliedNumberFormat!).Should().Be("50%");
    }

    [Fact]
    public void RoundTrip_Date_RendersAsDate_NotMinutes()
    {
        var r = SpreadsheetValueParser.Parse("1.2.2024", Cs);
        var formatted = SpreadsheetNumberFormatter.Format(r.Value, r.ImpliedNumberFormat!);
        // The month token must render as February (2), not as minutes (00).
        DateTime.Parse(formatted, Cs).Date.Should().Be(new DateTime(2024, 2, 1));
        formatted.Should().Contain("2024");
    }

    [Fact]
    public void RoundTrip_Time_RendersHoursMinutes()
    {
        var r = SpreadsheetValueParser.Parse("13:45", En);
        SpreadsheetNumberFormatter.Format(r.Value, r.ImpliedNumberFormat!).Should().Be("13:45");
    }

    [Theory]
    [InlineData(true, "TRUE")]
    [InlineData(false, "FALSE")]
    public void RoundTrip_Boolean_DisplaysUppercase(bool value, string expected)
    {
        SpreadsheetNumberFormatter.Format(value, "General").Should().Be(expected);
    }
}
