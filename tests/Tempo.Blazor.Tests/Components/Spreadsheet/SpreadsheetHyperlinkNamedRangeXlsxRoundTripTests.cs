using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Components.Spreadsheet.Xlsx;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetHyperlinkNamedRangeXlsxRoundTripTests
{
    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesWorkbookNamedRange()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 10 };
        sheet.Cells["B1"] = new SpreadsheetCell { Value = 20 };

        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "MyRange",
            RefersTo = "=Sheet1!$A$1:$B$1",
            Scope = NamedRangeScope.Workbook,
            Comment = "Test comment"
        });

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.NamedRanges.Should().ContainSingle();
        var nr = imported.NamedRanges[0];
        nr.Name.Should().Be("MyRange");
        nr.RefersTo.Should().Be("=Sheet1!$A$1:$B$1");
        nr.Scope.Should().Be(NamedRangeScope.Workbook);
        nr.SheetIndex.Should().BeNull();
        nr.Comment.Should().Be("Test comment");
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesSheetScopedNamedRange()
    {
        var workbook = new SpreadsheetWorkbook();
        workbook.Sheets.Clear();
        var sheet1 = workbook.AddSheet("First");
        sheet1.Cells["A1"] = new SpreadsheetCell { Value = 5 };
        var sheet2 = workbook.AddSheet("Second");
        sheet2.Cells["A1"] = new SpreadsheetCell { Value = 7 };

        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "SheetLocal",
            RefersTo = "=First!$A$1",
            Scope = NamedRangeScope.Sheet,
            SheetIndex = 0
        });

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.NamedRanges.Should().ContainSingle();
        var nr = imported.NamedRanges[0];
        nr.Name.Should().Be("SheetLocal");
        nr.Scope.Should().Be(NamedRangeScope.Sheet);
        nr.SheetIndex.Should().Be(0);
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesWebHyperlink()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell
        {
            Value = "Click me",
            Hyperlink = new SpreadsheetHyperlink
            {
                Kind = SpreadsheetHyperlinkKind.Web,
                Target = "https://example.com",
                Display = "Example",
                Tooltip = "Go to example"
            }
        };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        var cell = imported.Sheets[0].Cells["A1"];
        cell.Hyperlink.Should().NotBeNull();
        cell.Hyperlink!.Kind.Should().Be(SpreadsheetHyperlinkKind.Web);
        cell.Hyperlink.Target.Should().Be("https://example.com/");
        cell.Hyperlink.Display.Should().Be("Example");
        cell.Hyperlink.Tooltip.Should().Be("Go to example");
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesEmailHyperlink()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["B2"] = new SpreadsheetCell
        {
            Value = "Mail us",
            Hyperlink = new SpreadsheetHyperlink
            {
                Kind = SpreadsheetHyperlinkKind.Email,
                Target = "support@example.com",
                EmailSubject = "Help needed"
            }
        };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        var cell = imported.Sheets[0].Cells["B2"];
        cell.Hyperlink.Should().NotBeNull();
        cell.Hyperlink!.Kind.Should().Be(SpreadsheetHyperlinkKind.Email);
        cell.Hyperlink.Target.Should().Be("support@example.com");
    }

    [Fact]
    public void XlsxExportImport_RoundTrip_PreservesInternalRefHyperlink()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["C3"] = new SpreadsheetCell
        {
            Value = "Jump",
            Hyperlink = new SpreadsheetHyperlink
            {
                Kind = SpreadsheetHyperlinkKind.InternalRef,
                Target = "Sheet1!A10"
            }
        };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        var cell = imported.Sheets[0].Cells["C3"];
        cell.Hyperlink.Should().NotBeNull();
        cell.Hyperlink!.Kind.Should().Be(SpreadsheetHyperlinkKind.InternalRef);
        cell.Hyperlink.Target.Should().Be("Sheet1!A10");
    }

    [Fact]
    public void XlsxExportImport_HyperlinkDoesNotOverwriteCellValue()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell
        {
            Value = 42.0,
            Hyperlink = new SpreadsheetHyperlink
            {
                Kind = SpreadsheetHyperlinkKind.Web,
                Target = "https://example.com"
            }
        };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        var cell = imported.Sheets[0].Cells["A1"];
        cell.Value.Should().Be(42.0);
        cell.Hyperlink.Should().NotBeNull();
    }

    [Fact]
    public void XlsxExportImport_NamedRangeUsedInFormula_SurvivesRoundTrip()
    {
        var workbook = new SpreadsheetWorkbook();
        var sheet = workbook.ActiveSheet!;
        sheet.Cells["A1"] = new SpreadsheetCell { Value = 3 };
        sheet.Cells["A2"] = new SpreadsheetCell { Value = 4 };
        workbook.NamedRanges.Add(new SpreadsheetNamedRange
        {
            Name = "Data",
            RefersTo = "=Sheet1!$A$1:$A$2",
            Scope = NamedRangeScope.Workbook
        });
        sheet.Cells["B1"] = new SpreadsheetCell { Formula = "=SUM(Data)" };

        var bytes = XlsxExporter.Export(workbook);
        var imported = XlsxImporter.Import(bytes);

        imported.NamedRanges.Should().ContainSingle();
        imported.Sheets[0].Cells["B1"].Formula.Should().Be("=SUM(Data)");
    }
}
