using System.Globalization;
using Tempo.Blazor.Components.Spreadsheet.Format;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Xlsx;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

/// <summary>1.16 — values detected by <see cref="SpreadsheetValueParser"/> round-trip through XLSX with the correct types.</summary>
public class SpreadsheetTypedXlsxRoundTripTests
{
    private static CultureInfo En => CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void Xlsx_TypedValues_RoundTrip()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;

        void Set(string cellRef, string raw)
        {
            var parsed = SpreadsheetValueParser.Parse(raw, En);
            sheet.Cells[cellRef] = new SpreadsheetCell
            {
                Value = parsed.Value,
                DataType = parsed.Type,
                Style = new SpreadsheetCellStyle { NumberFormat = parsed.ImpliedNumberFormat ?? "General" }
            };
        }

        Set("A1", "1234.56"); // number
        Set("A2", "50%");     // percentage -> 0.5
        Set("A3", "$10");     // currency -> 10
        Set("A4", "TRUE");    // boolean
        Set("A5", "2024-02-01"); // date

        var imported = XlsxImporter.Import(XlsxExporter.Export(workbook));
        var s = imported.Sheets[0];

        s.Cells["A1"].Value.Should().Be(1234.56);
        ((double)s.Cells["A2"].Value!).Should().BeApproximately(0.5, 1e-9);
        ((double)s.Cells["A3"].Value!).Should().BeApproximately(10, 1e-9);
        s.Cells["A4"].Value.Should().Be(true);

        var a5 = s.Cells["A5"].Value;
        var date = a5 is DateTime dt ? dt : DateTime.FromOADate((double)a5!);
        date.Date.Should().Be(new DateTime(2024, 2, 1));
    }
}
