using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetSortEngineTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static void SetNumber(SpreadsheetSheet sheet, int row, int col, double value)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = value, DataType = SpreadsheetDataType.Number };

    private static void SetText(SpreadsheetSheet sheet, int row, int col, string text)
        => sheet.Cells[Ref(row, col)] = new SpreadsheetCell { Value = text, DataType = SpreadsheetDataType.Text };

    private static string Ref(int row, int col) => $"{SpreadsheetRange.ColumnIndexToLetters(col)}{row + 1}";

    [Fact]
    public void SingleKey_Ascending()
    {
        var sheet = new SpreadsheetSheet();
        SetNumber(sheet, 0, 0, 30);
        SetNumber(sheet, 1, 0, 10);
        SetNumber(sheet, 2, 0, 20);

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        order.Should().Equal(1, 2, 0); // rows with 10,20,30
    }

    [Fact]
    public void SingleKey_Descending()
    {
        var sheet = new SpreadsheetSheet();
        SetNumber(sheet, 0, 0, 30);
        SetNumber(sheet, 1, 0, 10);
        SetNumber(sheet, 2, 0, 20);

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Descending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        order.Should().Equal(0, 2, 1); // 30,20,10
    }

    [Fact]
    public void TypeOrder_NumbersBeforeText_BlanksLast()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Zebra");
        SetNumber(sheet, 1, 0, 5);
        // row 2 blank
        SetText(sheet, 3, 0, "Apple");

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 3, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        // number(5)=row1, then Apple=row3, Zebra=row0, blank=row2 last
        order.Should().Equal(1, 3, 0, 2);
    }

    [Fact]
    public void TypeOrder_Descending_BlanksStillLast()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Apple");
        SetNumber(sheet, 1, 0, 5);
        // row 2 blank

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Descending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        // Descending: text > number → Apple(row0), then 5(row1), blank(row2) last
        order.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void MultiLevel_StableSecondaryKey()
    {
        var sheet = new SpreadsheetSheet();
        // col0 = group, col1 = tiebreak
        SetText(sheet, 0, 0, "B"); SetNumber(sheet, 0, 1, 2);
        SetText(sheet, 1, 0, "A"); SetNumber(sheet, 1, 1, 9);
        SetText(sheet, 2, 0, "A"); SetNumber(sheet, 2, 1, 1);
        SetText(sheet, 3, 0, "B"); SetNumber(sheet, 3, 1, 1);

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 3, 1));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 1, Direction = SpreadsheetSortDirection.Ascending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        // A:1(row2), A:9(row1), B:1(row3), B:2(row0)
        order.Should().Equal(2, 1, 3, 0);
    }

    [Fact]
    public void WithHeader_KeepsHeaderRowExcludedFromOrder()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "Header");
        SetNumber(sheet, 1, 0, 30);
        SetNumber(sheet, 2, 0, 10);

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 0)) { HasHeader = true };
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, Direction = SpreadsheetSortDirection.Ascending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        order.Should().Equal(2, 1); // header (row0) excluded, 10 before 30
    }

    [Fact]
    public void CaseSensitive_OrdersLowercaseAfterUppercase()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "banana");
        SetText(sheet, 1, 0, "Apple");

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 1, 0));
        spec.Levels.Add(new SpreadsheetSortLevel { KeyIndex = 0, CaseSensitive = true, Direction = SpreadsheetSortDirection.Ascending });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        // Ordinal: 'A'(65) < 'b'(98) → Apple(row1) first
        order.Should().Equal(1, 0);
    }

    [Fact]
    public void ByColor_PinsMatchingColorToTop()
    {
        var sheet = new SpreadsheetSheet();
        SetText(sheet, 0, 0, "x"); sheet.Cells[Ref(0, 0)].Style.BackgroundColor = "transparent";
        SetText(sheet, 1, 0, "y"); sheet.Cells[Ref(1, 0)].Style.BackgroundColor = "#FFFF00";
        SetText(sheet, 2, 0, "z"); sheet.Cells[Ref(2, 0)].Style.BackgroundColor = "transparent";

        var spec = new SpreadsheetSortSpec(new SpreadsheetRange(0, 0, 2, 0));
        spec.Levels.Add(new SpreadsheetSortLevel
        {
            KeyIndex = 0,
            SortOn = SpreadsheetSortOn.CellColor,
            ColorKey = "#FFFF00",
            Direction = SpreadsheetSortDirection.Ascending
        });

        var order = SpreadsheetSortEngine.ComputeOrder(sheet, spec, Culture);
        order[0].Should().Be(1); // yellow row pinned to top
    }
}
