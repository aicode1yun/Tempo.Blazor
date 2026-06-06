using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TextToColumnsCommandTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Ref(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private static void SetText(SpreadsheetSheet sheet, int row, int col, string text)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = text, DataType = SpreadsheetDataType.Text };

    [Fact]
    public void Execute_SplitsSingleColumn_IntoMultipleColumns()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Jan;Novak;Praha");

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var cmd = new TextToColumnsCommand(sheet, sourceCol: 0, startRow: 0, endRow: 0, options, formats: [], Culture);
        cmd.Execute();

        cmd.ColumnsProduced.Should().Be(3);
        sheet.Cells[Ref(0, 0)].Value.Should().Be("Jan");
        sheet.Cells[Ref(0, 1)].Value.Should().Be("Novak");
        sheet.Cells[Ref(0, 2)].Value.Should().Be("Praha");
    }

    [Fact]
    public void Execute_WithTypeDetection_ParsesNumbers()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Jan;42");

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var cmd = new TextToColumnsCommand(sheet, 0, 0, 0, options, formats: [], Culture);
        cmd.Execute();

        sheet.Cells[Ref(0, 0)].Value.Should().Be("Jan");
        sheet.Cells[Ref(0, 1)].Value.Should().Be(42d);
        sheet.Cells[Ref(0, 1)].DataType.Should().Be(SpreadsheetDataType.Number);
    }

    [Fact]
    public void Execute_TextFormat_KeepsValueAsLiteralText()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "007;42");

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var formats = new[] { SpreadsheetColumnFormat.Text, SpreadsheetColumnFormat.General };
        var cmd = new TextToColumnsCommand(sheet, 0, 0, 0, options, formats, Culture);
        cmd.Execute();

        sheet.Cells[Ref(0, 0)].Value.Should().Be("007"); // not parsed to 7
        sheet.Cells[Ref(0, 0)].DataType.Should().Be(SpreadsheetDataType.Text);
        sheet.Cells[Ref(0, 1)].Value.Should().Be(42d);
    }

    [Fact]
    public void Execute_SkipColumn_DropsField_AndShiftsRemaining()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "A;B;C");

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var formats = new[] { SpreadsheetColumnFormat.General, SpreadsheetColumnFormat.Skip, SpreadsheetColumnFormat.General };
        var cmd = new TextToColumnsCommand(sheet, 0, 0, 0, options, formats, Culture);
        cmd.Execute();

        cmd.ColumnsProduced.Should().Be(2);
        sheet.Cells[Ref(0, 0)].Value.Should().Be("A");
        sheet.Cells[Ref(0, 1)].Value.Should().Be("C"); // B skipped, C shifted left
        sheet.Cells.ContainsKey(Ref(0, 2)).Should().BeFalse();
    }

    [Fact]
    public void Execute_OverwritesCellsToTheRight()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "A;B");
        SetText(sheet, 0, 1, "OLD"); // will be overwritten

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var cmd = new TextToColumnsCommand(sheet, 0, 0, 0, options, formats: [], Culture);
        cmd.Execute();

        sheet.Cells[Ref(0, 1)].Value.Should().Be("B");
    }

    [Fact]
    public void Undo_RestoresOriginalSingleColumn()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "A;B;C");
        SetText(sheet, 0, 1, "OLD");

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var cmd = new TextToColumnsCommand(sheet, 0, 0, 0, options, formats: [], Culture);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells[Ref(0, 0)].Value.Should().Be("A;B;C");
        sheet.Cells[Ref(0, 1)].Value.Should().Be("OLD");
        sheet.Cells.ContainsKey(Ref(0, 2)).Should().BeFalse();
    }

    [Fact]
    public void Execute_MultipleRows_SplitsEach()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "A;B");
        SetText(sheet, 1, 0, "C;D");

        var options = new SpreadsheetSeparatorOptions { Semicolon = true };
        var cmd = new TextToColumnsCommand(sheet, 0, 0, 1, options, formats: [], Culture);
        cmd.Execute();

        sheet.Cells[Ref(0, 0)].Value.Should().Be("A");
        sheet.Cells[Ref(0, 1)].Value.Should().Be("B");
        sheet.Cells[Ref(1, 0)].Value.Should().Be("C");
        sheet.Cells[Ref(1, 1)].Value.Should().Be("D");
    }
}
