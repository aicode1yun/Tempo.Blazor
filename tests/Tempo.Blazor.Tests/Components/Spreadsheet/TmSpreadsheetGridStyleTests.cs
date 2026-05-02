using Bunit;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetGridStyleTests : LocalizationTestBase
{
    [Fact]
    public void Render_CellWithBoldStyle_HasBoldFontWeight()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("A1");
        cell.Value = "BoldText";
        cell.Style.Bold = true;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cellDiv = cut.Find(".tm-spreadsheet-cell");
        cellDiv.GetAttribute("style").Should().Contain("font-weight: bold");
    }

    [Fact]
    public void Render_CellWithBackgroundColor_HasBackgroundColor()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("B2");
        cell.Value = "Colored";
        cell.Style.BackgroundColor = "#FFFF00";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var coloredCell = cells.First(c => c.TextContent.Trim() == "Colored");
        coloredCell.GetAttribute("style").Should().Contain("background-color: #FFFF00");
    }

    [Fact]
    public void Render_CellWithBorder_StyleContainsBorder()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("C3");
        cell.Value = "Bordered";
        cell.Style.BorderTop = new SpreadsheetBorder(SpreadsheetBorderStyle.Thin, "#000000");
        cell.Style.BorderRight = new SpreadsheetBorder(SpreadsheetBorderStyle.Medium, "#FF0000");
        cell.Style.BorderBottom = new SpreadsheetBorder(SpreadsheetBorderStyle.Dashed, "#0000FF");
        cell.Style.BorderLeft = new SpreadsheetBorder(SpreadsheetBorderStyle.Dotted, "#00FF00");

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var borderedCell = cells.First(c => c.TextContent.Trim() == "Bordered");
        var style = borderedCell.GetAttribute("style");
        style.Should().Contain("border-top: 1px solid #000000");
        style.Should().Contain("border-right: 2px solid #FF0000");
        style.Should().Contain("border-bottom: 1px dashed #0000FF");
        style.Should().Contain("border-left: 1px dotted #00FF00");
    }

    [Fact]
    public void Render_CellWithNumberFormat_FormatsValue()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("D4");
        cell.Value = 1234.567;
        cell.Style.NumberFormat = "#,##0.00";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var formattedCell = cells.First(c => c.TextContent.Trim() == "1,234.57");
        formattedCell.Should().NotBeNull();
    }

    [Fact]
    public void Render_CellWithPercentageFormat_FormatsValue()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("E5");
        cell.Value = 0.1555;
        cell.Style.NumberFormat = "0.00%";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var formattedCell = cells.First(c => c.TextContent.Trim() == "15.55%");
        formattedCell.Should().NotBeNull();
    }

    [Fact]
    public void Render_CellWithCurrencyFormat_FormatsValue()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("F6");
        cell.Value = 1234.5;
        cell.Style.NumberFormat = "$#,##0.00";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var formattedCell = cells.First(c => c.TextContent.Trim() == "$1,234.50");
        formattedCell.Should().NotBeNull();
    }

    [Fact]
    public void Render_CellWithDateFormat_FormatsDateTime()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("G7");
        cell.Value = new DateTime(2024, 6, 15);
        cell.Style.NumberFormat = "yyyy-MM-dd";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var formattedCell = cells.First(c => c.TextContent.Trim() == "2024-06-15");
        formattedCell.Should().NotBeNull();
    }

    [Fact]
    public void Render_CellWithAlignment_HasJustifyContent()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("H8");
        cell.Value = "Centered";
        cell.Style.HorizontalAlign = SpreadsheetHorizontalAlign.Center;
        cell.Style.VerticalAlign = SpreadsheetVerticalAlign.Middle;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var alignedCell = cells.First(c => c.TextContent.Trim() == "Centered");
        var style = alignedCell.GetAttribute("style");
        style.Should().Contain("justify-content: center");
        style.Should().Contain("align-items: center");
    }

    [Fact]
    public void Render_CellWithTextWrap_HasWhiteSpaceNormal()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("I9");
        cell.Value = "Wrapped";
        cell.Style.TextWrap = true;

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var wrapCell = cells.First(c => c.TextContent.Trim() == "Wrapped");
        wrapCell.GetAttribute("style").Should().Contain("white-space: normal");
    }

    [Fact]
    public void Render_CellWithoutBorder_NoBorderInStyle()
    {
        var sheet = new SpreadsheetSheet();
        var cell = sheet.GetOrCreateCell("J10");
        cell.Value = "NoBorder";

        var cut = RenderComponent<TmSpreadsheetGrid>(parameters => parameters
            .Add(p => p.Sheet, sheet));

        var cells = cut.FindAll(".tm-spreadsheet-cell");
        var noBorderCell = cells.First(c => c.TextContent.Trim() == "NoBorder");
        var style = noBorderCell.GetAttribute("style") ?? "";
        style.Should().NotContain("border-top");
        style.Should().NotContain("border-right");
        style.Should().NotContain("border-bottom");
        style.Should().NotContain("border-left");
    }
}
