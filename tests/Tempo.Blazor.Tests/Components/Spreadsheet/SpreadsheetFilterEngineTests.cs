using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFilterEngineTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static SpreadsheetSheet BuildSheet()
    {
        // Header in row 0 (A1:B1), data rows 1..5.
        // Column A (0): text fruit, Column B (1): numbers
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Fruit");
        SetText(sheet, 0, 1, "Qty");
        SetText(sheet, 1, 0, "Apple"); SetNumber(sheet, 1, 1, 50);
        SetText(sheet, 2, 0, "Banana"); SetNumber(sheet, 2, 1, 150);
        SetText(sheet, 3, 0, "Apple"); SetNumber(sheet, 3, 1, 200);
        SetText(sheet, 4, 0, "Cherry"); SetNumber(sheet, 4, 1, 75);
        // row 5 col A blank, col B 300
        SetNumber(sheet, 5, 1, 300);
        return sheet;
    }

    private static void SetText(SpreadsheetSheet sheet, int row, int col, string text)
    {
        var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        sheet.Cells[cellRef] = new SpreadsheetCell { Value = text, DisplayValue = text, DataType = SpreadsheetDataType.Text };
    }

    private static void SetNumber(SpreadsheetSheet sheet, int row, int col, double value)
    {
        var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        sheet.Cells[cellRef] = new SpreadsheetCell
        {
            Value = value,
            DisplayValue = value.ToString(CultureInfo.InvariantCulture),
            DataType = SpreadsheetDataType.Number
        };
    }

    private static SpreadsheetAutoFilter Filter(SpreadsheetSheet sheet)
        => new(new SpreadsheetRange(0, 0, 5, 1));

    [Fact]
    public void DistinctValues_ReturnsSortedUnique_WithBlankLast()
    {
        var sheet = BuildSheet();
        var values = SpreadsheetFilterEngine.DistinctValues(sheet, Filter(sheet), 0, Culture);

        values.Where(v => !v.IsBlank).Select(v => v.Display)
            .Should().Equal("Apple", "Banana", "Cherry");
        values[^1].IsBlank.Should().BeTrue();
    }

    [Fact]
    public void DistinctValues_NumbersSortNumerically()
    {
        var sheet = BuildSheet();
        var values = SpreadsheetFilterEngine.DistinctValues(sheet, Filter(sheet), 1, Culture);

        values.Select(v => v.Display).Should().Equal("50", "75", "150", "200", "300");
    }

    [Fact]
    public void Apply_ValuesFilter_HidesNonMatching()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["Apple"]
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);

        // Rows 2 (Banana), 4 (Cherry), 5 (blank) hidden; 1 and 3 (Apple) kept.
        hidden.Should().BeEquivalentTo(new[] { 2, 4, 5 });
    }

    [Fact]
    public void Apply_TextContains()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Text,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.Contains, "an")
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);

        // Only Banana (row 2) contains "an"; others hidden.
        hidden.Should().BeEquivalentTo(new[] { 1, 3, 4, 5 });
    }

    [Fact]
    public void Apply_NumberGreaterThan()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.GreaterThan, "100")
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);

        // Kept: 150 (row2), 200 (row3), 300 (row5); hidden: 50 (row1), 75 (row4).
        hidden.Should().BeEquivalentTo(new[] { 1, 4 });
    }

    [Fact]
    public void Apply_NumberBetween()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.Between, "70", "200")
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);

        // Kept: 150,200,75 → rows 2,3,4; hidden: 50 (row1), 300 (row5).
        hidden.Should().BeEquivalentTo(new[] { 1, 5 });
    }

    [Fact]
    public void Apply_AboveAverage()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.AboveAverage)
        });

        // values 50,150,200,75,300 → average 155. Above: 200,300 → rows 3,5 kept.
        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);
        hidden.Should().BeEquivalentTo(new[] { 1, 2, 4 });
    }

    [Fact]
    public void Apply_Top10_WithN2()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.Top10, "2")
        });

        // Top 2 of 50,150,200,75,300 → 300,200 → rows 5,3 kept.
        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);
        hidden.Should().BeEquivalentTo(new[] { 1, 2, 4 });
    }

    [Fact]
    public void Apply_ColorFilter_Background()
    {
        var sheet = BuildSheet();
        sheet.Cells["A2"].Style.BackgroundColor = "#FFFF00";
        sheet.Cells["A4"].Style.BackgroundColor = "#FFFF00";
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Color,
            ColorFilter = new SpreadsheetColorFilter { Target = SpreadsheetColorTarget.Background, Color = "#FFFF00" }
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);

        // Yellow on rows 1 (A2) and 3 (A4) kept; others hidden.
        hidden.Should().BeEquivalentTo(new[] { 2, 4, 5 });
    }

    [Fact]
    public void Apply_TwoColumns_LogicalAnd()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["Apple"]
        });
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.GreaterThan, "100")
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);

        // Apple rows: 1 (50) and 3 (200). Qty>100 → only row 3 kept.
        hidden.Should().BeEquivalentTo(new[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void Apply_CustomFilter_TwoConditionsOr()
    {
        var sheet = BuildSheet();
        var filter = Filter(sheet);
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = new SpreadsheetFilterCriteria
            {
                Join = SpreadsheetFilterJoin.Or,
                Conditions =
                [
                    new SpreadsheetFilterCondition { Operator = SpreadsheetFilterOperator.LessThan, Operand = "60" },
                    new SpreadsheetFilterCondition { Operator = SpreadsheetFilterOperator.GreaterThan, Operand = "250" }
                ]
            }
        });

        // Kept: <60 (50→row1) OR >250 (300→row5). Hidden: 2,3,4.
        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);
        hidden.Should().BeEquivalentTo(new[] { 2, 3, 4 });
    }
}
