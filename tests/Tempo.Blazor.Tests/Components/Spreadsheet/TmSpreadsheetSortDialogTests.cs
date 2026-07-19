using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Dialogs;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetSortDialogTests : LocalizationTestBase
{
    [Fact]
    public void Renders_LevelRow_AndOptions_Localized()
    {
        var cut = Render<TmSpreadsheetSortDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 2)));

        var markup = cut.Markup;
        markup.Should().Contain("Sort"); // title
        markup.Should().Contain("My data has headers");
        markup.Should().Contain("Add level");
        markup.Should().Contain("Values");
        // one level row present by default
        cut.FindAll(".tm-spreadsheet-sort__table tbody tr").Count.Should().Be(1);
    }

    [Fact]
    public void AddLevel_AddsRow()
    {
        var cut = Render<TmSpreadsheetSortDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 2)));

        cut.Find(".tm-spreadsheet-sort__tool").Click();

        cut.FindAll(".tm-spreadsheet-sort__table tbody tr").Count.Should().Be(2);
    }

    [Fact]
    public void Apply_BuildsSpec_WithRangeAndHeader()
    {
        SpreadsheetSortSpec? applied = null;
        var cut = Render<TmSpreadsheetSortDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 2))
            .Add(c => c.OnApply, EventCallback.Factory.Create<SpreadsheetSortSpec>(this, s => applied = s)));

        cut.Find(".tm-spreadsheet-sort__btn--ok").Click();

        applied.Should().NotBeNull();
        applied!.HasHeader.Should().BeTrue();
        applied.Levels.Should().HaveCount(1);
        applied.Range.EndRow.Should().Be(9);
    }
}
