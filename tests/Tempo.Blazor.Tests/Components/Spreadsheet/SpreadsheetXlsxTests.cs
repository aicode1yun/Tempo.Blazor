using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Xlsx;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetXlsxTests
{
    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesCellValues()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Hello" };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = 42.5 };
        sheet.Cells["C1"] = new SpreadsheetCell { Value = true };
        sheet.Cells["A2"] = new SpreadsheetCell { Formula = "=SUM(B1:C1)" };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.Sheets.Count.Should().Be(1);
        var importedSheet = imported.Sheets[0];
        importedSheet.Cells["A1"].Value.Should().Be("Hello");
        importedSheet.Cells["B1"].Value.Should().Be(42.5);
        importedSheet.Cells["C1"].Value.Should().Be(true);
        importedSheet.Cells["A2"].Formula.Should().Be("=SUM(B1:C1)");
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesStyles()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell
        {
            Value = "Styled",
            Style = new SpreadsheetCellStyle
            {
                Bold = true,
                Italic = true,
                FontSize = 14,
                ForeColor = "#FF0000",
                BackgroundColor = "#00FF00",
                HorizontalAlign = SpreadsheetHorizontalAlign.Center,
                NumberFormat = "0.00"
            }
        };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        var cell = imported.Sheets[0].Cells["A1"];
        cell.Style.Bold.Should().BeTrue();
        cell.Style.Italic.Should().BeTrue();
        cell.Style.FontSize.Should().Be(14);
        cell.Style.ForeColor.Should().Be("#FF0000");
        cell.Style.BackgroundColor.Should().Be("#00FF00");
        cell.Style.HorizontalAlign.Should().Be(SpreadsheetHorizontalAlign.Center);
        cell.Style.NumberFormat.Should().Be("0.00");
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesMergedCells()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Merged" };
        sheet.MergedCells.Add(new SpreadsheetRange(0, 0, 1, 1)); // A1:B2

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.Sheets[0].MergedCells.Should().ContainSingle();
        imported.Sheets[0].MergedCells[0].ToString().Should().Be("A1:B2");
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesMultipleSheets()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear();
        var sheet1 = workbook.AddSheet("First");
        sheet1.Cells["A1"] = new SpreadsheetCell { Value = "Sheet1" };
        var sheet2 = workbook.AddSheet("Second");
        sheet2.Cells["A1"] = new SpreadsheetCell { Value = "Sheet2" };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.Sheets.Count.Should().Be(2);
        imported.Sheets[0].Name.Should().Be("First");
        imported.Sheets[1].Name.Should().Be("Second");
        imported.Sheets[0].Cells["A1"].Value.Should().Be("Sheet1");
        imported.Sheets[1].Cells["A1"].Value.Should().Be("Sheet2");
    }

    [Fact]
    public void XlsxImport_EmptyWorkbook_CreatesDefaultSheet()
    {
        // Export empty workbook (which has one default sheet)
        var workbook = new SpreadsheetWorkbook();
        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.Sheets.Count.Should().BeGreaterThanOrEqualTo(1);
    }
}
