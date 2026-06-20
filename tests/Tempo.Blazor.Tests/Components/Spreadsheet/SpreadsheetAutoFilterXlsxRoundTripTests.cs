using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Xlsx;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetAutoFilterXlsxRoundTripTests
{
    [Fact]
    public void AutoFilter_DefinitionAndHiddenRows_SurviveRoundTrip()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Cells["A1"] = new SpreadsheetCell { Value = "Fruit", DataType = SpreadsheetDataType.Text };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = "Apple", DataType = SpreadsheetDataType.Text };
        sheet.Cells["A3"] = new SpreadsheetCell { Value = "Banana", DataType = SpreadsheetDataType.Text };
        sheet.AutoFilter = new SpreadsheetAutoFilter(new SpreadsheetRange(0, 0, 2, 0));
        sheet.Rows[2] = new SpreadsheetRow { Index = 2, IsHidden = true }; // Banana hidden by filter

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        var importedSheet = imported.Sheets[0];
        importedSheet.AutoFilter.Should().NotBeNull();
        importedSheet.AutoFilter!.Range.ToString().Should().Be("A1:A3");
        importedSheet.Rows[2].IsHidden.Should().BeTrue();
    }
}
