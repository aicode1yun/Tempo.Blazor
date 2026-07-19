using Bunit;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

/// <summary>Unit tests for the typed-value / implied-format behaviour of <see cref="SetCellValueCommand"/>.</summary>
public class SetCellValueCommandTypedTests
{
    [Fact]
    public void Execute_AppliesImpliedFormat_OnGeneralCell()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new SetCellValueCommand(sheet, "A1", 1234.56, null,
            dataType: SpreadsheetDataType.Number, impliedNumberFormat: "#,##0.00");

        cmd.Execute();

        var cell = sheet.Cells["A1"];
        cell.Value.Should().Be(1234.56);
        cell.DataType.Should().Be(SpreadsheetDataType.Number);
        cell.Style.NumberFormat.Should().Be("#,##0.00");
    }

    [Fact]
    public void Execute_DoesNotOverrideExplicitFormat()
    {
        var sheet = new SpreadsheetSheet();
        var existing = sheet.GetOrCreateCell("A1");
        existing.Style.NumberFormat = "0.0";

        new SetCellValueCommand(sheet, "A1", 0.5, null,
            dataType: SpreadsheetDataType.Percentage, impliedNumberFormat: "0%").Execute();

        sheet.Cells["A1"].Style.NumberFormat.Should().Be("0.0");
    }

    [Fact]
    public void Undo_RestoresValueFormatAndDataType()
    {
        var sheet = new SpreadsheetSheet();
        var existing = sheet.GetOrCreateCell("A1");
        existing.Value = 5.0;
        existing.DataType = SpreadsheetDataType.Number; // General format (default)

        var cmd = new SetCellValueCommand(sheet, "A1", 0.5, null,
            dataType: SpreadsheetDataType.Percentage, impliedNumberFormat: "0%");
        cmd.Execute();
        sheet.Cells["A1"].Style.NumberFormat.Should().Be("0%");
        sheet.Cells["A1"].DataType.Should().Be(SpreadsheetDataType.Percentage);

        cmd.Undo();

        var cell = sheet.Cells["A1"];
        cell.Value.Should().Be(5.0);
        cell.DataType.Should().Be(SpreadsheetDataType.Number);
        cell.Style.NumberFormat.Should().Be("General");
    }
}

/// <summary>Integration tests: committing raw input through the component detects the value type (A5).</summary>
public class SpreadsheetCommitTypeDetectionTests : LocalizationTestBase
{
    [Fact]
    public async Task Commit_NumberInput_StoresDoubleAndNumberType()
    {
        var cut = Render<TmSpreadsheet>();
        await cut.InvokeAsync(() => cut.Instance.SetCellValue("A1", "123"));

        var cell = cut.Instance.Workbook.ActiveSheet!.Cells["A1"];
        cell.Value.Should().Be(123.0);
        cell.DataType.Should().Be(SpreadsheetDataType.Number);
    }

    [Fact]
    public async Task Commit_PercentInput_SetsImpliedFormat()
    {
        var cut = Render<TmSpreadsheet>();
        await cut.InvokeAsync(() => cut.Instance.SetCellValue("A1", "50%"));

        var cell = cut.Instance.Workbook.ActiveSheet!.Cells["A1"];
        cell.Value.Should().Be(0.5);
        cell.DataType.Should().Be(SpreadsheetDataType.Percentage);
        cell.Style.NumberFormat.Should().Be("0%");
    }

    [Fact]
    public async Task Commit_ForcedText_KeepsLeadingZeros()
    {
        var cut = Render<TmSpreadsheet>();
        await cut.InvokeAsync(() => cut.Instance.SetCellValue("A1", "'007"));

        var cell = cut.Instance.Workbook.ActiveSheet!.Cells["A1"];
        cell.Value.Should().Be("007");
        cell.DataType.Should().Be(SpreadsheetDataType.Text);
    }

    [Fact]
    public async Task Commit_FormulaInput_StillEvaluates()
    {
        var cut = Render<TmSpreadsheet>();
        await cut.InvokeAsync(() =>
        {
            cut.Instance.SetCellValue("A1", "10");
            cut.Instance.SetCellValue("A2", "=A1*2");
        });

        var cell = cut.Instance.Workbook.ActiveSheet!.Cells["A2"];
        cell.Formula.Should().Be("=A1*2");
        cell.Value.Should().Be(20.0);
    }
}
