using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetFilterEngineDateTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static void SetDate(SpreadsheetSheet sheet, int row, int col, DateTime value)
        => sheet.Cells[$"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}"]
            = new SpreadsheetCell { Value = value, DataType = SpreadsheetDataType.Date };

    [Fact]
    public void Apply_DateToday_KeepsOnlyToday()
    {
        var sheet = new SpreadsheetSheet();
        SetDate(sheet, 0, 0, new DateTime(2000, 1, 1)); // header-ish
        SetDate(sheet, 1, 0, DateTime.Today);
        SetDate(sheet, 2, 0, DateTime.Today.AddDays(-5));

        var filter = new SpreadsheetAutoFilter(new SpreadsheetRange(0, 0, 2, 0));
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Date,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.Today)
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);
        hidden.Should().BeEquivalentTo(new[] { 2 }); // only the -5 days row hidden (row1 today kept)
    }

    [Fact]
    public void Apply_DateBetween()
    {
        var sheet = new SpreadsheetSheet();
        SetDate(sheet, 0, 0, new DateTime(2024, 1, 1)); // header row, not filtered
        SetDate(sheet, 1, 0, new DateTime(2024, 6, 15));
        SetDate(sheet, 2, 0, new DateTime(2024, 12, 31));

        var filter = new SpreadsheetAutoFilter(new SpreadsheetRange(0, 0, 2, 0));
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Date,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.Between, "2024-06-01", "2024-07-01")
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);
        hidden.Should().BeEquivalentTo(new[] { 2 }); // Dec 31 hidden, June 15 kept
    }

    [Fact]
    public void Apply_DateThisYear()
    {
        var sheet = new SpreadsheetSheet();
        SetDate(sheet, 0, 0, DateTime.Today);
        SetDate(sheet, 1, 0, DateTime.Today);
        SetDate(sheet, 2, 0, DateTime.Today.AddYears(-2));

        var filter = new SpreadsheetAutoFilter(new SpreadsheetRange(0, 0, 2, 0));
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Date,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.ThisYear)
        });

        var hidden = SpreadsheetFilterEngine.ComputeHiddenRows(sheet, filter, Culture);
        hidden.Should().BeEquivalentTo(new[] { 2 });
    }
}
