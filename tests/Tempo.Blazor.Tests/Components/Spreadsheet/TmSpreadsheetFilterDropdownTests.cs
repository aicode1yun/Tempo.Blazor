using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetFilterDropdownTests : LocalizationTestBase
{
    private static SpreadsheetSheet BuildSheet()
    {
        var sheet = new SpreadsheetSheet();
        void Set(int row, string text)
            => sheet.Cells[$"A{row + 1}"] = new SpreadsheetCell { Value = text, DisplayValue = text, DataType = SpreadsheetDataType.Text };
        Set(0, "Fruit");
        Set(1, "Apple");
        Set(2, "Banana");
        Set(3, "Apple");
        return sheet;
    }

    private static SpreadsheetAutoFilter Filter() => new(new SpreadsheetRange(0, 0, 3, 0));

    [Fact]
    public void Renders_SortOptions_SearchAndValues_Localized()
    {
        var sheet = BuildSheet();
        var cut = RenderComponent<TmSpreadsheetFilterDropdown>(p => p
            .Add(c => c.Sheet, sheet)
            .Add(c => c.Filter, Filter())
            .Add(c => c.ColumnIndex, 0)
            .Add(c => c.Culture, CultureInfo.InvariantCulture));

        var markup = cut.Markup;
        markup.Should().Contain("Sort A → Z");
        markup.Should().Contain("Sort Z → A");
        markup.Should().Contain("(Select all)");
        // distinct values listed once
        markup.Should().Contain("Apple");
        markup.Should().Contain("Banana");
        cut.FindAll("input[type=checkbox]").Count.Should().BeGreaterThanOrEqualTo(3); // select-all + 2 values
    }

    [Fact]
    public void Apply_WithSubset_RaisesValuesFilter()
    {
        var sheet = BuildSheet();
        SpreadsheetColumnFilter? applied = null;
        var applyHandled = false;

        var cut = RenderComponent<TmSpreadsheetFilterDropdown>(p => p
            .Add(c => c.Sheet, sheet)
            .Add(c => c.Filter, Filter())
            .Add(c => c.ColumnIndex, 0)
            .Add(c => c.Culture, CultureInfo.InvariantCulture)
            .Add(c => c.OnApply, EventCallback.Factory.Create<SpreadsheetColumnFilter?>(this, f =>
            {
                applied = f;
                applyHandled = true;
            })));

        // Uncheck "Banana" (find its label by text)
        var bananaCheckbox = cut.FindAll(".tm-spreadsheet-filter-dropdown__check")
            .First(e => e.TextContent.Contains("Banana"))
            .QuerySelector("input")!;
        bananaCheckbox.Change(false);

        cut.Find(".tm-spreadsheet-filter-dropdown__btn--ok").Click();

        applyHandled.Should().BeTrue();
        applied.Should().NotBeNull();
        applied!.Kind.Should().Be(SpreadsheetFilterKind.Values);
        applied.AllowedValues.Should().Contain("Apple");
        applied.AllowedValues.Should().NotContain("Banana");
    }

    [Fact]
    public void SortAscending_RaisesCallbackWithColumn()
    {
        var sheet = BuildSheet();
        int? sortedColumn = null;

        var cut = RenderComponent<TmSpreadsheetFilterDropdown>(p => p
            .Add(c => c.Sheet, sheet)
            .Add(c => c.Filter, Filter())
            .Add(c => c.ColumnIndex, 0)
            .Add(c => c.OnSortAscending, EventCallback.Factory.Create<int>(this, c => sortedColumn = c)));

        cut.FindAll(".tm-spreadsheet-filter-dropdown__item")
            .First(e => e.TextContent.Contains("Sort A → Z"))
            .Click();

        sortedColumn.Should().Be(0);
    }
}
