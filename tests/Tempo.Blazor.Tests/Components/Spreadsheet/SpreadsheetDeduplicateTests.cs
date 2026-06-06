using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetDeduplicateTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Ref(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private static void Set(SpreadsheetSheet sheet, int row, int col, object value, SpreadsheetDataType type = SpreadsheetDataType.Text)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = value, DataType = type };

    [Fact]
    public void KeepsFirstOccurrence_RemovesLaterDuplicates()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Apple");
        Set(sheet, 1, 0, "Banana");
        Set(sheet, 2, 0, "Apple");   // dup of row 0
        Set(sheet, 3, 0, "Banana");  // dup of row 1

        var range = new SpreadsheetRange(0, 0, 3, 0);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        result.Should().Equal(2, 3);
    }

    [Fact]
    public void NoDuplicates_ReturnsEmpty()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Apple");
        Set(sheet, 1, 0, "Banana");
        Set(sheet, 2, 0, "Cherry");

        var range = new SpreadsheetRange(0, 0, 2, 0);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllIdentical_RemovesAllButFirst()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "X");
        Set(sheet, 1, 0, "X");
        Set(sheet, 2, 0, "X");

        var range = new SpreadsheetRange(0, 0, 2, 0);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        result.Should().Equal(1, 2);
    }

    [Fact]
    public void Header_IsExcludedFromComparison()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Name");   // header
        Set(sheet, 1, 0, "Name");   // same text as header but it is a data row, unique so far
        Set(sheet, 2, 0, "Name");   // dup of row 1

        var range = new SpreadsheetRange(0, 0, 2, 0);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: true, caseSensitive: false, Culture);

        result.Should().Equal(2);
    }

    [Fact]
    public void MultipleKeyColumns_BothMustMatch()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Jan"); Set(sheet, 0, 1, "Novak");
        Set(sheet, 1, 0, "Jan"); Set(sheet, 1, 1, "Svoboda"); // same first name, different last → unique
        Set(sheet, 2, 0, "Jan"); Set(sheet, 2, 1, "Novak");   // dup of row 0

        var range = new SpreadsheetRange(0, 0, 2, 1);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0, 1], hasHeader: false, caseSensitive: false, Culture);

        result.Should().Equal(2);
    }

    [Fact]
    public void SubsetKeyColumn_IgnoresOtherColumns()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Jan"); Set(sheet, 0, 1, "A");
        Set(sheet, 1, 0, "Jan"); Set(sheet, 1, 1, "B"); // dup on column 0 only

        var range = new SpreadsheetRange(0, 0, 1, 1);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        result.Should().Equal(1);
    }

    [Fact]
    public void CaseInsensitive_TreatsDifferentCaseAsDuplicate()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "apple");
        Set(sheet, 1, 0, "APPLE");

        var range = new SpreadsheetRange(0, 0, 1, 0);
        SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture)
            .Should().Equal(1);

        SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: true, Culture)
            .Should().BeEmpty();
    }

    [Fact]
    public void NumbersCompareByValue_NotFormattedText()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, 1d, SpreadsheetDataType.Number);
        Set(sheet, 1, 0, 1d, SpreadsheetDataType.Number);
        sheet.Cells[Ref(1, 0)].Style.NumberFormat = "0.00"; // different display, same value

        var range = new SpreadsheetRange(0, 0, 1, 0);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        result.Should().Equal(1);
    }

    [Fact]
    public void BlankCells_AreComparedAsEqual()
    {
        var sheet = new SpreadsheetSheet();
        // both rows entirely blank
        var range = new SpreadsheetRange(0, 0, 1, 0);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [0], hasHeader: false, caseSensitive: false, Culture);

        result.Should().Equal(1);
    }

    [Fact]
    public void EmptyKeyColumns_UsesAllColumnsInRange()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Jan"); Set(sheet, 0, 1, "A");
        Set(sheet, 1, 0, "Jan"); Set(sheet, 1, 1, "B"); // differs on col 1 → unique when all columns count

        var range = new SpreadsheetRange(0, 0, 1, 1);
        var result = SpreadsheetDeduplicate.ComputeRowsToRemove(sheet, range, [], hasHeader: false, caseSensitive: false, Culture);

        result.Should().BeEmpty();
    }
}
