using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class ReplaceCommandTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static SpreadsheetSheet TextSheet(params (string Ref, string Text)[] cells)
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        foreach (var (cellRef, text) in cells)
            sheet.Cells[cellRef] = new SpreadsheetCell { Value = text, DisplayValue = text, DataType = SpreadsheetDataType.Text };
        return sheet;
    }

    [Fact]
    public void Execute_ReplacesText_AndUndoRestores()
    {
        var sheet = TextSheet(("A1", "Hello World"));
        var options = new SpreadsheetSearchOptions { Query = "World" };
        var cmd = new ReplaceCommand(sheet, "A1", options, "There", Culture);

        cmd.Execute();

        cmd.DidReplace.Should().BeTrue();
        sheet.Cells["A1"].Value.Should().Be("Hello There");

        cmd.Undo();
        sheet.Cells["A1"].Value.Should().Be("Hello World");
    }

    [Fact]
    public void Execute_NoMatch_DoesNothing()
    {
        var sheet = TextSheet(("A1", "Hello"));
        var options = new SpreadsheetSearchOptions { Query = "xyz" };
        var cmd = new ReplaceCommand(sheet, "A1", options, "abc", Culture);

        cmd.Execute();

        cmd.DidReplace.Should().BeFalse();
        sheet.Cells["A1"].Value.Should().Be("Hello");
    }

    [Fact]
    public void Execute_AllInCell_ReplacesEveryOccurrence()
    {
        var sheet = TextSheet(("A1", "a-a-a"));
        var options = new SpreadsheetSearchOptions { Query = "a" };
        var cmd = new ReplaceCommand(sheet, "A1", options, "b", Culture, allInCell: true);

        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be("b-b-b");
    }

    [Fact]
    public void Execute_ValuesMode_SkipsFormulaCells()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        sheet.Cells["A1"] = new SpreadsheetCell { Formula = "=1+1", Value = 2.0, DisplayValue = "2" };
        var options = new SpreadsheetSearchOptions { Query = "2", SearchIn = SpreadsheetSearchIn.Values };
        var cmd = new ReplaceCommand(sheet, "A1", options, "9", Culture);

        cmd.Execute();

        cmd.DidReplace.Should().BeFalse();
        sheet.Cells["A1"].Formula.Should().Be("=1+1");
    }

    [Fact]
    public void Execute_FormulasMode_RewritesFormula_AndUndoRestores()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        sheet.Cells["A1"] = new SpreadsheetCell { Formula = "=A2+A3", Value = 0.0 };
        var options = new SpreadsheetSearchOptions { Query = "A3", SearchIn = SpreadsheetSearchIn.Formulas };
        var cmd = new ReplaceCommand(sheet, "A1", options, "A4", Culture);

        cmd.Execute();

        cmd.DidReplace.Should().BeTrue();
        sheet.Cells["A1"].Formula.Should().Be("=A2+A4");

        cmd.Undo();
        sheet.Cells["A1"].Formula.Should().Be("=A2+A3");
    }

    [Fact]
    public void Execute_NumericResult_ReparsedAsNumber()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 30.0, DisplayValue = "30", DataType = SpreadsheetDataType.Number };
        var options = new SpreadsheetSearchOptions { Query = "3" };
        var cmd = new ReplaceCommand(sheet, "A1", options, "4", Culture);

        cmd.Execute();

        sheet.Cells["A1"].Value.Should().Be(40.0);
        sheet.Cells["A1"].DataType.Should().Be(SpreadsheetDataType.Number);
    }

    [Fact]
    public void ReplaceAll_AcrossCells_AsBatch_UndoesInOneStep()
    {
        var sheet = TextSheet(("A1", "cat"), ("A2", "cat"), ("A3", "dog"));
        var options = new SpreadsheetSearchOptions { Query = "cat" };

        var batch = new BatchCommand();
        var manager = new SpreadsheetCommandManager(sheet);
        foreach (var cellRef in new[] { "A1", "A2" })
            batch.Add(new ReplaceCommand(sheet, cellRef, options, "fish", Culture, allInCell: true));
        manager.Execute(batch);

        sheet.Cells["A1"].Value.Should().Be("fish");
        sheet.Cells["A2"].Value.Should().Be("fish");
        sheet.Cells["A3"].Value.Should().Be("dog");

        manager.Undo();
        sheet.Cells["A1"].Value.Should().Be("cat");
        sheet.Cells["A2"].Value.Should().Be("cat");
    }
}
