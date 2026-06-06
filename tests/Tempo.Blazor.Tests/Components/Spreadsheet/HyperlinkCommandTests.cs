using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class HyperlinkCommandTests
{
    [Fact]
    public void SetHyperlinkCommand_AttachesLink_AndFillsDisplayValue()
    {
        var sheet = new SpreadsheetSheet();
        var link = new SpreadsheetHyperlink
        {
            Kind = SpreadsheetHyperlinkKind.Web,
            Target = "https://example.com",
            Display = "Example"
        };

        var cmd = new SetHyperlinkCommand(sheet, "A1", link);
        cmd.Execute();

        sheet.Cells["A1"].Hyperlink.Should().NotBeNull();
        sheet.Cells["A1"].Hyperlink!.Target.Should().Be("https://example.com");
        sheet.Cells["A1"].Value.Should().Be("Example");

        cmd.Undo();

        sheet.Cells["A1"].Hyperlink.Should().BeNull();
        sheet.Cells["A1"].Value.Should().BeNull();
    }

    [Fact]
    public void SetHyperlinkCommand_PreservesExistingValue()
    {
        var sheet = new SpreadsheetSheet();
        sheet.SetCellValue(0, 0, "Keep me");

        var link = new SpreadsheetHyperlink
        {
            Kind = SpreadsheetHyperlinkKind.Web,
            Target = "https://example.com",
            Display = "Example"
        };

        var cmd = new SetHyperlinkCommand(sheet, "A1", link);
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("Keep me");

        cmd.Undo();

        sheet.Cells["A1"].Value.Should().Be("Keep me");
        sheet.Cells["A1"].Hyperlink.Should().BeNull();
    }

    [Fact]
    public void RemoveHyperlinkCommand_RemovesLink_AndUndoRestores()
    {
        var sheet = new SpreadsheetSheet();
        sheet.Cells["A1"] = new SpreadsheetCell
        {
            Value = "Click",
            Hyperlink = new SpreadsheetHyperlink
            {
                Kind = SpreadsheetHyperlinkKind.Web,
                Target = "https://example.com"
            }
        };

        var cmd = new RemoveHyperlinkCommand(sheet, "A1");
        cmd.Execute();

        sheet.Cells["A1"].Hyperlink.Should().BeNull();
        sheet.Cells["A1"].Value.Should().Be("Click");

        cmd.Undo();

        sheet.Cells["A1"].Hyperlink.Should().NotBeNull();
        sheet.Cells["A1"].Hyperlink!.Target.Should().Be("https://example.com");
    }

    [Fact]
    public void RemoveHyperlinkCommand_OnCellWithoutLink_DoesNothing()
    {
        var sheet = new SpreadsheetSheet();
        sheet.SetCellValue(0, 0, "Text");

        var cmd = new RemoveHyperlinkCommand(sheet, "A1");
        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("Text");
        sheet.Cells["A1"].Hyperlink.Should().BeNull();
    }
}
