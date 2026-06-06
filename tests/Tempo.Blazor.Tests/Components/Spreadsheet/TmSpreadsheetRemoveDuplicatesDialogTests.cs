using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet.Dialogs;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetRemoveDuplicatesDialogTests : LocalizationTestBase
{
    [Fact]
    public void Renders_Columns_AndOptions_Localized()
    {
        var cut = RenderComponent<TmSpreadsheetRemoveDuplicatesDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 2)));

        var markup = cut.Markup;
        markup.Should().Contain("Remove duplicates");
        markup.Should().Contain("My data has headers");
        markup.Should().Contain("Select all");
        // 3 columns in range
        cut.FindAll(".tm-spreadsheet-dedup__column").Count.Should().Be(3);
    }

    [Fact]
    public void UsesHeaderLabels_WhenProvided()
    {
        var headers = new Dictionary<int, string> { [0] = "Name", [1] = "City" };
        var cut = RenderComponent<TmSpreadsheetRemoveDuplicatesDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 1))
            .Add(c => c.HeaderLabels, headers));

        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("City");
    }

    [Fact]
    public void DeselectAll_DisablesOk()
    {
        var cut = RenderComponent<TmSpreadsheetRemoveDuplicatesDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 1)));

        cut.FindAll(".tm-spreadsheet-dedup__link")[1].Click(); // Deselect all

        cut.Find(".tm-spreadsheet-dedup__btn--ok").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Apply_ReturnsSelectedColumns()
    {
        SpreadsheetRemoveDuplicatesOptions? applied = null;
        var cut = RenderComponent<TmSpreadsheetRemoveDuplicatesDialog>(p => p
            .Add(c => c.Range, new SpreadsheetRange(0, 0, 9, 2))
            .Add(c => c.OnApply, EventCallback.Factory.Create<SpreadsheetRemoveDuplicatesOptions>(this, o => applied = o)));

        cut.Find(".tm-spreadsheet-dedup__btn--ok").Click();

        applied.Should().NotBeNull();
        applied!.KeyColumns.Should().Equal(0, 1, 2); // all selected by default
        applied.HasHeader.Should().BeTrue();
    }
}
