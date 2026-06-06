using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetAutoFilterModelTests
{
    [Fact]
    public void AutoFilter_HeaderAndDataRows_Computed()
    {
        var filter = new SpreadsheetAutoFilter(new SpreadsheetRange(2, 0, 10, 3));

        filter.HeaderRow.Should().Be(2);
        filter.FirstDataRow.Should().Be(3);
    }

    [Fact]
    public void ColumnFilter_Values_IsActiveOnlyWithAllowedValues()
    {
        var col = new SpreadsheetColumnFilter { ColumnIndex = 1, Kind = SpreadsheetFilterKind.Values };
        col.IsActive.Should().BeFalse();

        col.AllowedValues = ["A", "B"];
        col.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ColumnFilter_Criteria_IsActiveWithConditions()
    {
        var col = new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Number,
            Criteria = SpreadsheetFilterCriteria.Single(SpreadsheetFilterOperator.GreaterThan, "100")
        };

        col.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ColumnFilter_Color_IsActiveWithColor()
    {
        var col = new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Color,
            ColorFilter = new SpreadsheetColorFilter { Color = "#FF0000" }
        };

        col.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AutoFilter_Clone_IsDeepCopy()
    {
        var filter = new SpreadsheetAutoFilter(new SpreadsheetRange(0, 0, 5, 2));
        filter.Columns.Add(new SpreadsheetColumnFilter
        {
            ColumnIndex = 1,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["X"]
        });

        var clone = filter.Clone();
        clone.Columns[0].AllowedValues!.Add("Y");

        filter.Columns[0].AllowedValues.Should().NotContain("Y");
        clone.Range.EndRow.Should().Be(5);
    }

    [Fact]
    public void Sheet_Clone_CopiesAutoFilter()
    {
        var sheet = new SpreadsheetSheet { AutoFilter = new SpreadsheetAutoFilter(new SpreadsheetRange(0, 0, 3, 1)) };
        sheet.AutoFilter.Columns.Add(new SpreadsheetColumnFilter { ColumnIndex = 0, AllowedValues = ["A"] });

        var clone = sheet.Clone();
        clone.AutoFilter.Should().NotBeNull();
        clone.AutoFilter!.Columns.Should().HaveCount(1);
        clone.AutoFilter.Columns[0].AllowedValues!.Add("B");

        sheet.AutoFilter!.Columns[0].AllowedValues.Should().NotContain("B");
    }

    [Fact]
    public void SortSpec_Clone_IsDeepCopy()
    {
        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 9, 2)) { HasHeader = true };
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Descending });

        var clone = spec.Clone();
        clone.Levels[0].KeyIndex = 5;

        spec.Levels[0].KeyIndex.Should().Be(0);
        clone.HasHeader.Should().BeTrue();
    }
}
