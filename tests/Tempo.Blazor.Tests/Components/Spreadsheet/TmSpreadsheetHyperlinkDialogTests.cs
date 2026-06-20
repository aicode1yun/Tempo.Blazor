using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Dialogs;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetHyperlinkDialogTests : LocalizationTestBase
{
    [Fact]
    public void Renders_TitleAndTypeOptions()
    {
        var workbook = new SpreadsheetWorkbook();
        var cut = RenderComponent<TmSpreadsheetHyperlinkDialog>(
            parameters => parameters.Add(p => p.Workbook, workbook));

        cut.Find(".tm-spreadsheet-hyperlink__title").TextContent.Should().Be("Hyperlink");
        var options = cut.FindAll("#hl-type option");
        options.Count.Should().Be(4);
    }

    [Fact]
    public void EditMode_PrePopulatesFields()
    {
        var workbook = new SpreadsheetWorkbook();
        var link = new SpreadsheetHyperlink
        {
            Kind = SpreadsheetHyperlinkKind.Web,
            Target = "https://example.com",
            Display = "Example",
            Tooltip = "Click"
        };

        var cut = RenderComponent<TmSpreadsheetHyperlinkDialog>(
            parameters => parameters
                .Add(p => p.Workbook, workbook)
                .Add(p => p.Hyperlink, link));

        cut.Find("#hl-target").GetAttribute("value").Should().Be("https://example.com");
    }

    [Fact]
    public void Save_RaisesOnSave_WithHyperlink()
    {
        var workbook = new SpreadsheetWorkbook();
        SpreadsheetHyperlink? saved = null;

        var cut = RenderComponent<TmSpreadsheetHyperlinkDialog>(
            parameters => parameters
                .Add(p => p.Workbook, workbook)
                .Add(p => p.OnSave, h => saved = h));

        cut.Find("#hl-target").Input("https://test.com");
        cut.Find("#hl-display").Change("Test");
        cut.Find(".tm-spreadsheet-hyperlink__btn--ok").Click();

        saved.Should().NotBeNull();
        saved!.Kind.Should().Be(SpreadsheetHyperlinkKind.Web);
        saved.Target.Should().Be("https://test.com");
        saved.Display.Should().Be("Test");
    }

    [Fact]
    public void Cancel_RaisesOnCancel()
    {
        var workbook = new SpreadsheetWorkbook();
        var fired = false;

        var cut = RenderComponent<TmSpreadsheetHyperlinkDialog>(
            parameters => parameters
                .Add(p => p.Workbook, workbook)
                .Add(p => p.OnCancel, () => fired = true));

        cut.Find(".tm-spreadsheet-hyperlink__btn--cancel").Click();
        fired.Should().BeTrue();
    }
}
