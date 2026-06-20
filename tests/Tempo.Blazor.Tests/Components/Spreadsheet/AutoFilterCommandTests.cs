using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class AutoFilterCommandTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static SpreadsheetSheet BuildSheet()
    {
        var sheet = new SpreadsheetSheet();
        Set(sheet, 0, 0, "Fruit", SpreadsheetDataType.Text);
        Set(sheet, 1, 0, "Apple", SpreadsheetDataType.Text);
        Set(sheet, 2, 0, "Banana", SpreadsheetDataType.Text);
        Set(sheet, 3, 0, "Cherry", SpreadsheetDataType.Text);
        return sheet;
    }

    private static void Set(SpreadsheetSheet sheet, int row, int col, object value, SpreadsheetDataType type)
    {
        var cellRef = $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";
        sheet.Cells[cellRef] = new SpreadsheetCell { Value = value, DisplayValue = value.ToString(), DataType = type };
    }

    [Fact]
    public void SetAutoFilter_EnablesFilter_UndoRemoves()
    {
        var sheet = BuildSheet();
        var cmd = new SetAutoFilterCommand(sheet, new SpreadsheetRange(0, 0, 3, 0));

        cmd.Execute();
        sheet.AutoFilter.Should().NotBeNull();
        sheet.AutoFilter!.Range.EndRow.Should().Be(3);

        cmd.Undo();
        sheet.AutoFilter.Should().BeNull();
    }

    [Fact]
    public void UpdateColumnFilter_HidesRows_UndoRestores()
    {
        var sheet = BuildSheet();
        new SetAutoFilterCommand(sheet, new SpreadsheetRange(0, 0, 3, 0)).Execute();

        var columnFilter = new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["Apple"]
        };
        var cmd = new UpdateColumnFilterCommand(sheet, columnFilter, Culture);
        cmd.Execute();

        sheet.Rows[2].IsHidden.Should().BeTrue();  // Banana
        sheet.Rows[3].IsHidden.Should().BeTrue();  // Cherry
        (sheet.Rows.TryGetValue(1, out var r) && r.IsHidden).Should().BeFalse(); // Apple visible

        cmd.Undo();
        (sheet.Rows.TryGetValue(2, out var r2) && r2.IsHidden).Should().BeFalse();
        sheet.AutoFilter!.Columns.Should().BeEmpty();
    }

    [Fact]
    public void UpdateColumnFilter_Reapply_NarrowsThenUndoWidens()
    {
        var sheet = BuildSheet();
        new SetAutoFilterCommand(sheet, new SpreadsheetRange(0, 0, 3, 0)).Execute();

        var first = new UpdateColumnFilterCommand(sheet, new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["Apple", "Banana"]
        }, Culture);
        first.Execute();
        sheet.Rows[3].IsHidden.Should().BeTrue(); // Cherry hidden

        var second = new UpdateColumnFilterCommand(sheet, new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["Apple"]
        }, Culture);
        second.Execute();
        sheet.Rows[2].IsHidden.Should().BeTrue(); // Banana now hidden too

        second.Undo();
        (sheet.Rows.TryGetValue(2, out var r) && r.IsHidden).Should().BeFalse(); // Banana visible again
        sheet.Rows[3].IsHidden.Should().BeTrue(); // Cherry still hidden
    }

    [Fact]
    public void ClearAutoFilter_RevealsRows_UndoRestores()
    {
        var sheet = BuildSheet();
        new SetAutoFilterCommand(sheet, new SpreadsheetRange(0, 0, 3, 0)).Execute();
        new UpdateColumnFilterCommand(sheet, new SpreadsheetColumnFilter
        {
            ColumnIndex = 0,
            Kind = SpreadsheetFilterKind.Values,
            AllowedValues = ["Apple"]
        }, Culture).Execute();

        sheet.Rows[2].IsHidden.Should().BeTrue();

        var clear = new ClearAutoFilterCommand(sheet);
        clear.Execute();
        sheet.AutoFilter.Should().BeNull();
        sheet.Rows[2].IsHidden.Should().BeFalse();

        clear.Undo();
        sheet.AutoFilter.Should().NotBeNull();
        sheet.Rows[2].IsHidden.Should().BeTrue();
    }
}
