using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class RemoveDuplicatesCommandTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Ref(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private static void Set(SpreadsheetSheet sheet, int row, int col, object value)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = value, DataType = SpreadsheetDataType.Text };

    [Fact]
    public void Execute_RemovesDuplicateRows_AndCompactsUpward()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Apple"); Set(sheet, 0, 1, "Red");
        Set(sheet, 1, 0, "Banana"); Set(sheet, 1, 1, "Yellow");
        Set(sheet, 2, 0, "Apple"); Set(sheet, 2, 1, "Red");   // dup
        Set(sheet, 3, 0, "Cherry"); Set(sheet, 3, 1, "Dark");

        var range = new SpreadsheetRange(0, 0, 3, 1);
        var cmd = new RemoveDuplicatesCommand(sheet, range, [0, 1], hasHeader: false, caseSensitive: false, Culture);
        cmd.Execute();

        cmd.RemovedCount.Should().Be(1);
        cmd.RemainingCount.Should().Be(3);

        sheet.Cells[Ref(0, 0)].Value.Should().Be("Apple");
        sheet.Cells[Ref(1, 0)].Value.Should().Be("Banana");
        sheet.Cells[Ref(2, 0)].Value.Should().Be("Cherry");  // compacted up into row 2
        sheet.Cells.ContainsKey(Ref(3, 0)).Should().BeFalse(); // tail cleared
    }

    [Fact]
    public void Undo_RestoresOriginalLayoutExactly()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Apple");
        Set(sheet, 1, 0, "Banana");
        Set(sheet, 2, 0, "Apple");
        Set(sheet, 3, 0, "Cherry");

        var range = new SpreadsheetRange(0, 0, 3, 0);
        var cmd = new RemoveDuplicatesCommand(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);
        cmd.Execute();
        cmd.Undo();

        sheet.Cells[Ref(0, 0)].Value.Should().Be("Apple");
        sheet.Cells[Ref(1, 0)].Value.Should().Be("Banana");
        sheet.Cells[Ref(2, 0)].Value.Should().Be("Apple");
        sheet.Cells[Ref(3, 0)].Value.Should().Be("Cherry");
    }

    [Fact]
    public void Execute_KeepsHeaderRow_WhenHasHeader()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Fruit"); // header
        Set(sheet, 1, 0, "Apple");
        Set(sheet, 2, 0, "Apple"); // dup

        var range = new SpreadsheetRange(0, 0, 2, 0);
        var cmd = new RemoveDuplicatesCommand(sheet, range, [0], hasHeader: true, caseSensitive: false, Culture);
        cmd.Execute();

        cmd.RemovedCount.Should().Be(1);
        sheet.Cells[Ref(0, 0)].Value.Should().Be("Fruit");
        sheet.Cells[Ref(1, 0)].Value.Should().Be("Apple");
        sheet.Cells.ContainsKey(Ref(2, 0)).Should().BeFalse();
    }

    [Fact]
    public void Execute_NoDuplicates_LeavesDataUnchanged()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "A");
        Set(sheet, 1, 0, "B");

        var range = new SpreadsheetRange(0, 0, 1, 0);
        var cmd = new RemoveDuplicatesCommand(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);
        cmd.Execute();

        cmd.RemovedCount.Should().Be(0);
        cmd.RemainingCount.Should().Be(2);
        sheet.Cells[Ref(0, 0)].Value.Should().Be("A");
        sheet.Cells[Ref(1, 0)].Value.Should().Be("B");
    }

    [Fact]
    public void Execute_WithMergeConflict_DoesNothing()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "A");
        Set(sheet, 1, 0, "A");
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 0));

        var range = new SpreadsheetRange(0, 0, 1, 0);
        var cmd = new RemoveDuplicatesCommand(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        cmd.HasMergeConflict.Should().BeTrue();
        cmd.Execute();

        sheet.Cells[Ref(1, 0)].Value.Should().Be("A"); // unchanged
    }
}
