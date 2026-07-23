using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Export;
using Tempo.Blazor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>Typed XLSX serialization supplied by the optional DataTable XLSX package.</summary>
public sealed class DataTableXlsxExporterTests
{
    [Fact]
    public void AddTempoBlazorDataTableXlsx_RegistersCanonicalExporter()
    {
        var services = new ServiceCollection();

        services.AddTempoBlazorDataTableXlsx();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDataTableXlsxExporter>()
            .Should().BeSameAs(provider.GetRequiredService<DataTableXlsxExporter>());
    }

    [Fact]
    public void Export_WritesHeadersRowsAndNativeCellTypes()
    {
        var exportedAt = new DateTime(2026, 7, 23, 14, 30, 0, DateTimeKind.Unspecified);
        var data = new DataTableExportData
        {
            Name = "Report/2026",
            Headers = ["Name", "Count", "Amount", "Exported"],
            Rows = [["Alice", "12", "19.5", "2026-07-23 14:30:00"]],
            Values = [["Alice", 12, 19.5m, exportedAt]]
        };

        var bytes = new DataTableXlsxExporter().Export(data);

        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        new OpenXmlValidator().Validate(document).Should().BeEmpty();
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().Single();
        sheet.Name!.Value.Should().Be("Report_2026");
        var rows = workbookPart.WorksheetParts.Single().Worksheet
            .GetFirstChild<SheetData>()!.Elements<Row>().ToList();
        rows.Should().HaveCount(2);
        rows[0].Elements<Cell>().Select(CellText).Should().Equal("Name", "Count", "Amount", "Exported");

        var cells = rows[1].Elements<Cell>().ToList();
        cells[0].DataType!.Value.Should().Be(CellValues.InlineString);
        CellText(cells[0]).Should().Be("Alice");
        cells[1].DataType!.Value.Should().Be(CellValues.Number);
        cells[1].CellValue!.Text.Should().Be("12");
        cells[2].DataType!.Value.Should().Be(CellValues.Number);
        cells[2].CellValue!.Text.Should().Be("19.5");
        cells[3].DataType!.Value.Should().Be(CellValues.Number);
        cells[3].StyleIndex!.Value.Should().BeGreaterThan(0U);
        DateTime.FromOADate(double.Parse(cells[3].CellValue!.Text,
            System.Globalization.CultureInfo.InvariantCulture)).Should().Be(exportedAt);

        var style = workbookPart.WorkbookStylesPart!.Stylesheet.CellFormats!
            .Elements<CellFormat>().ElementAt((int)cells[3].StyleIndex.Value);
        style.NumberFormatId!.Value.Should().BeGreaterThanOrEqualTo(164U);
    }

    [Fact]
    public void Export_EmptyData_WritesOnlyHeaderRow()
    {
        var bytes = new DataTableXlsxExporter().Export(new DataTableExportData
        {
            Headers = ["Name", "Count"],
            Rows = [],
            Values = []
        });

        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        var rows = document.WorkbookPart!.WorksheetParts.Single().Worksheet
            .GetFirstChild<SheetData>()!.Elements<Row>().ToList();
        rows.Should().ContainSingle();
        rows[0].Elements<Cell>().Select(CellText).Should().Equal("Name", "Count");
    }

    private static string CellText(Cell cell) =>
        cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
}
