using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Dialogs;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetNamedRangeEditDialogTests : LocalizationTestBase
{
    [Fact]
    public void NewMode_Renders_TitleAndEmptyFields()
    {
        var workbook = new SpreadsheetWorkbook();
        var cut = RenderComponent<TmSpreadsheetNamedRangeEditDialog>(
            parameters => parameters.Add(p => p.Workbook, workbook));

        cut.Find(".tm-spreadsheet-named-range-edit__title").TextContent.Should().Be("New Name");
        cut.Find("#nr-name").GetAttribute("value").Should().BeNullOrEmpty();
        cut.Find("#nr-refers").GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public void EditMode_Renders_PrePopulatedFields()
    {
        var workbook = new SpreadsheetWorkbook();
        var range = new SpreadsheetNamedRange { Name = "Sales", RefersTo = "A1:A10", Scope = NamedRangeScope.Sheet, SheetIndex = 0, Comment = "Q1" };

        var cut = RenderComponent<TmSpreadsheetNamedRangeEditDialog>(
            parameters => parameters
                .Add(p => p.Workbook, workbook)
                .Add(p => p.Range, range));

        cut.Find(".tm-spreadsheet-named-range-edit__title").TextContent.Should().Be("Edit Name");
        cut.Find("#nr-name").GetAttribute("value").Should().Be("Sales");
        cut.Find("#nr-refers").GetAttribute("value").Should().Be("A1:A10");
    }

    [Fact]
    public void Save_WithEmptyName_ShowsError()
    {
        var workbook = new SpreadsheetWorkbook();
        var cut = RenderComponent<TmSpreadsheetNamedRangeEditDialog>(
            parameters => parameters.Add(p => p.Workbook, workbook));

        cut.Find(".tm-spreadsheet-named-range-edit__btn--ok").Click();

        cut.FindAll(".tm-spreadsheet-named-range-edit__error").Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Save_WithValidData_RaisesOnSave()
    {
        var workbook = new SpreadsheetWorkbook();
        SpreadsheetNamedRange? saved = null;

        var cut = RenderComponent<TmSpreadsheetNamedRangeEditDialog>(
            parameters => parameters
                .Add(p => p.Workbook, workbook)
                .Add(p => p.OnSave, r => saved = r));

        cut.Find("#nr-name").Input("Total");
        cut.Find("#nr-refers").Input("B1:B10");
        cut.Find(".tm-spreadsheet-named-range-edit__btn--ok").Click();

        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Total");
        saved.RefersTo.Should().Be("B1:B10");
    }

    [Fact]
    public void Cancel_RaisesOnCancel()
    {
        var workbook = new SpreadsheetWorkbook();
        var fired = false;

        var cut = RenderComponent<TmSpreadsheetNamedRangeEditDialog>(
            parameters => parameters
                .Add(p => p.Workbook, workbook)
                .Add(p => p.OnCancel, () => fired = true));

        cut.Find(".tm-spreadsheet-named-range-edit__btn--cancel").Click();
        fired.Should().BeTrue();
    }
}
