using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SortRangeCommandTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static string Ref(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    private static void SetNumber(SpreadsheetSheet sheet, int row, int col, double value)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = value, DataType = SpreadsheetDataType.Number };

    private static void SetText(SpreadsheetSheet sheet, int row, int col, string text)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = text, DataType = SpreadsheetDataType.Text };

    [Fact]
    public void Sort_ReordersRows_MovesValuesAndStyles()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Charlie"); SetNumber(sheet, 0, 1, 3);
        SetText(sheet, 1, 0, "Alpha"); SetNumber(sheet, 1, 1, 1);
        SetText(sheet, 2, 0, "Bravo"); SetNumber(sheet, 2, 1, 2);
        sheet.Cells[Ref(1, 0)].Style.BackgroundColor = "#00FF00"; // Alpha marker

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 1));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });
        var cmd = new SortRangeCommand(sheet, spec, Culture);

        cmd.Execute();

        sheet.Cells[Ref(0, 0)].Value.Should().Be("Alpha");
        sheet.Cells[Ref(1, 0)].Value.Should().Be("Bravo");
        sheet.Cells[Ref(2, 0)].Value.Should().Be("Charlie");
        sheet.Cells[Ref(0, 1)].Value.Should().Be(1d);
        // The green style followed Alpha to row 0.
        sheet.Cells[Ref(0, 0)].Style.BackgroundColor.Should().Be("#00FF00");
    }

    [Fact]
    public void Sort_Undo_RestoresOriginalLayout()
    {
        var sheet = new SpreadsheetSheet();
        SetNumber(sheet, 0, 0, 3);
        SetNumber(sheet, 1, 0, 1);
        SetNumber(sheet, 2, 0, 2);

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });
        var cmd = new SortRangeCommand(sheet, spec, Culture);

        cmd.Execute();
        cmd.Undo();

        sheet.Cells[Ref(0, 0)].Value.Should().Be(3d);
        sheet.Cells[Ref(1, 0)].Value.Should().Be(1d);
        sheet.Cells[Ref(2, 0)].Value.Should().Be(2d);
    }

    [Fact]
    public void Sort_PreservesFormulasRelative()
    {
        var sheet = new SpreadsheetSheet();
        // col0 sort key, col1 has a relative formula referencing col0 of same row.
        SetNumber(sheet, 0, 0, 30); sheet.Cells[Ref(0, 1)] = new SpreadsheetCell { Formula = "=A1*2" };
        SetNumber(sheet, 1, 0, 10); sheet.Cells[Ref(1, 1)] = new SpreadsheetCell { Formula = "=A2*2" };
        SetNumber(sheet, 2, 0, 20); sheet.Cells[Ref(2, 1)] = new SpreadsheetCell { Formula = "=A3*2" };

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 1));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });
        new SortRangeCommand(sheet, spec, Culture).Execute();

        // After sort by col0 ascending: 10,20,30. Row0 should hold A1*2 referencing its own A1=10.
        sheet.Cells[Ref(0, 0)].Value.Should().Be(10d);
        sheet.Cells[Ref(0, 1)].Formula.Should().Be("=A1*2");
        sheet.Cells[Ref(1, 1)].Formula.Should().Be("=A2*2");
        sheet.Cells[Ref(2, 1)].Formula.Should().Be("=A3*2");
    }

    [Fact]
    public void Sort_WithMergeConflict_DoesNothing()
    {
        var sheet = new SpreadsheetSheet();
        SetNumber(sheet, 0, 0, 3);
        SetNumber(sheet, 1, 0, 1);
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 0));

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 1, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0 });
        var cmd = new SortRangeCommand(sheet, spec, Culture);

        cmd.HasMergeConflict.Should().BeTrue();
        cmd.Execute();

        sheet.Cells[Ref(0, 0)].Value.Should().Be(3d); // unchanged
    }
}
