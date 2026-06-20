using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetValidationEngineTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ── Any ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Any_AlwaysValid()
    {
        var rule = Rule(SpreadsheetValidationType.Any);
        Validate("anything", rule).IsValid.Should().BeTrue();
        Validate(null, rule).IsValid.Should().BeTrue();
    }

    // ── AllowBlank ────────────────────────────────────────────────────────────

    [Fact]
    public void AllowBlank_True_EmptyPassesRule()
    {
        var rule = Rule(SpreadsheetValidationType.Whole) with { AllowBlank = true, Formula1 = "1", Formula2 = "10" };
        Validate(null, rule).IsValid.Should().BeTrue();
        Validate("", rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AllowBlank_False_EmptyFails()
    {
        var rule = Rule(SpreadsheetValidationType.Whole) with { AllowBlank = false, Formula1 = "1", Formula2 = "10" };
        Validate(null, rule).IsValid.Should().BeFalse();
    }

    // ── Whole ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5, "1", "10", SpreadsheetValidationOperator.Between, true)]
    [InlineData(15, "1", "10", SpreadsheetValidationOperator.Between, false)]
    [InlineData(5, "5", null, SpreadsheetValidationOperator.Equal, true)]
    [InlineData(6, "5", null, SpreadsheetValidationOperator.Equal, false)]
    [InlineData(6, "5", null, SpreadsheetValidationOperator.NotEqual, true)]
    [InlineData(3, "5", null, SpreadsheetValidationOperator.GreaterThan, false)]
    [InlineData(7, "5", null, SpreadsheetValidationOperator.GreaterThan, true)]
    [InlineData(3, "5", null, SpreadsheetValidationOperator.GreaterOrEqual, false)]
    [InlineData(5, "5", null, SpreadsheetValidationOperator.GreaterOrEqual, true)]
    [InlineData(3, "5", null, SpreadsheetValidationOperator.LessThan, true)]
    [InlineData(7, "5", null, SpreadsheetValidationOperator.LessThan, false)]
    public void Whole_Operators(double value, string f1, string? f2, SpreadsheetValidationOperator op, bool expected)
    {
        var rule = Rule(SpreadsheetValidationType.Whole) with
        {
            Operator = op, Formula1 = f1, Formula2 = f2, AllowBlank = false
        };
        Validate((double)value, rule).IsValid.Should().Be(expected);
    }

    [Fact]
    public void Whole_RejectsDecimal()
    {
        var rule = Rule(SpreadsheetValidationType.Whole) with
        {
            Operator = SpreadsheetValidationOperator.Between, Formula1 = "1", Formula2 = "10"
        };
        Validate(3.5, rule).IsValid.Should().BeFalse();
    }

    // ── Decimal ───────────────────────────────────────────────────────────────

    [Fact]
    public void Decimal_AcceptsDecimalValue()
    {
        var rule = Rule(SpreadsheetValidationType.Decimal) with
        {
            Operator = SpreadsheetValidationOperator.Between, Formula1 = "1", Formula2 = "10"
        };
        Validate(3.5, rule).IsValid.Should().BeTrue();
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public void List_LiteralList_ValidValue()
    {
        var rule = Rule(SpreadsheetValidationType.List) with { Formula1 = "Apple,Banana,Cherry" };
        Validate("Banana", rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void List_LiteralList_InvalidValue()
    {
        var rule = Rule(SpreadsheetValidationType.List) with { Formula1 = "Apple,Banana,Cherry" };
        Validate("Mango", rule).IsValid.Should().BeFalse();
    }

    [Fact]
    public void List_CaseInsensitiveMatch()
    {
        var rule = Rule(SpreadsheetValidationType.List) with { Formula1 = "Yes,No" };
        Validate("yes", rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void List_FromRange_ReadsSheetCells()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["E1"] = new SpreadsheetCell { Value = "Red" };
        sheet.Cells["E2"] = new SpreadsheetCell { Value = "Green" };
        sheet.Cells["E3"] = new SpreadsheetCell { Value = "Blue" };

        var rule = Rule(SpreadsheetValidationType.List) with { Formula1 = "=$E$1:$E$3" };
        Validate("Green", rule, sheet).IsValid.Should().BeTrue();
        Validate("Yellow", rule, sheet).IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetListItems_FromLiteral_ReturnsSplitItems()
    {
        var rule = Rule(SpreadsheetValidationType.List) with { Formula1 = "A,B,C" };
        var items = SpreadsheetValidationEngine.GetListItems(rule, new SpreadsheetSheet());
        items.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void GetListItems_FromRange_ReturnsDistinctNonNull()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["E1"] = new SpreadsheetCell { Value = "X" };
        sheet.Cells["E2"] = new SpreadsheetCell { Value = "X" };
        sheet.Cells["E3"] = new SpreadsheetCell { Value = "Y" };

        var rule = Rule(SpreadsheetValidationType.List) with { Formula1 = "=$E$1:$E$3" };
        var items = SpreadsheetValidationEngine.GetListItems(rule, sheet);
        items.Should().Equal("X", "Y");
    }

    // ── Date ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Date_ValidDateInRange()
    {
        var rule = Rule(SpreadsheetValidationType.Date) with
        {
            Operator = SpreadsheetValidationOperator.Between,
            Formula1 = "2024-01-01",
            Formula2 = "2024-12-31"
        };
        Validate(new DateTime(2024, 6, 15), rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Date_DateOutOfRange()
    {
        var rule = Rule(SpreadsheetValidationType.Date) with
        {
            Operator = SpreadsheetValidationOperator.Between,
            Formula1 = "2024-01-01",
            Formula2 = "2024-12-31"
        };
        Validate(new DateTime(2025, 1, 1), rule).IsValid.Should().BeFalse();
    }

    // ── Time ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Time_ValidTimeInRange()
    {
        var rule = Rule(SpreadsheetValidationType.Time) with
        {
            Operator = SpreadsheetValidationOperator.Between,
            Formula1 = "08:00",
            Formula2 = "18:00"
        };
        Validate(new TimeSpan(12, 0, 0), rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Time_TimeOutOfRange()
    {
        var rule = Rule(SpreadsheetValidationType.Time) with
        {
            Operator = SpreadsheetValidationOperator.Between,
            Formula1 = "08:00",
            Formula2 = "18:00"
        };
        Validate(new TimeSpan(20, 0, 0), rule).IsValid.Should().BeFalse();
    }

    // ── TextLength ────────────────────────────────────────────────────────────

    [Fact]
    public void TextLength_WithinRange()
    {
        var rule = Rule(SpreadsheetValidationType.TextLength) with
        {
            Operator = SpreadsheetValidationOperator.Between, Formula1 = "3", Formula2 = "10"
        };
        Validate("Hello", rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TextLength_TooLong()
    {
        var rule = Rule(SpreadsheetValidationType.TextLength) with
        {
            Operator = SpreadsheetValidationOperator.LessOrEqual, Formula1 = "5"
        };
        Validate("TooLong", rule).IsValid.Should().BeFalse();
    }

    // ── Custom ────────────────────────────────────────────────────────────────

    [Fact]
    public void Custom_FormulaReturnsTrueWhenValid()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 5.0 };
        var rule = Rule(SpreadsheetValidationType.Custom) with { Formula1 = "=TRUE" };
        Validate(5.0, rule, sheet).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Custom_FormulaReturnsFalseWhenInvalid()
    {
        var sheet = new SpreadsheetSheet();
        var rule = Rule(SpreadsheetValidationType.Custom) with { Formula1 = "=FALSE" };
        Validate("anything", rule, sheet).IsValid.Should().BeFalse();
    }

    // ── Error style ───────────────────────────────────────────────────────────

    [Fact]
    public void FailResult_CarriesErrorStyle()
    {
        var rule = Rule(SpreadsheetValidationType.Whole) with
        {
            AllowBlank = false,
            Formula1 = "1", Formula2 = "10",
            ErrorAlert = new SpreadsheetValidationErrorAlert { Style = SpreadsheetValidationErrorStyle.Warning }
        };
        var result = Validate(99.0, rule);
        result.IsValid.Should().BeFalse();
        result.ErrorStyle.Should().Be(SpreadsheetValidationErrorStyle.Warning);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpreadsheetDataValidation Rule(SpreadsheetValidationType type) => new()
    {
        Range = new SpreadsheetRange(0, 0, 0, 0),
        Type = type,
        AllowBlank = true
    };

    private static ValidationResult Validate(object? value, SpreadsheetDataValidation rule, SpreadsheetSheet? sheet = null)
        => SpreadsheetValidationEngine.Validate(value, rule, sheet ?? new SpreadsheetSheet(), Inv);
}
