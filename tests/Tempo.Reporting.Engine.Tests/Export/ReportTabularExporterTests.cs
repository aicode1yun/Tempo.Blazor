#pragma warning disable MA0051

using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Export;
using Tempo.Reporting.Engine.Processing;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using DefinitionFieldType = Tempo.Reporting.Abstractions.Definitions.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Tests.Export;

public sealed class ReportTabularExporterTests
{
    [Fact]
    public void ExportCsv_UsesCultureDelimiterQuotingAndOptionalBom()
    {
        var (definition, context) = CreateReport();
        var document = ReportTabularExportBuilder.Build(definition, context);

        var bytes = ReportCsvExporter.Export(
            document,
            new ReportCsvExportOptions
            {
                Culture = CultureInfo.GetCultureInfo("cs-CZ"),
                Delimiter = ';',
                IncludeBom = true,
            });

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var csv = Encoding.UTF8.GetString(bytes[3..]);
        csv.Should().Be(string.Join(
            "\r\n",
            "Customer;Total;Issued",
            "\"ACME; Praha\";1234,5;31.12.2025",
            "\"Quote \"\"Ltd\"\"\";89,25;01.01.2026",
            string.Empty));
    }

    [Fact]
    public void ExportXlsx_WritesTableDataHeaderStyleAndNumberFormats()
    {
        var (definition, context) = CreateReport();
        var document = ReportTabularExportBuilder.Build(definition, context);

        var bytes = ReportXlsxExporter.Export(
            document,
            new ReportXlsxExportOptions { Culture = CultureInfo.GetCultureInfo("en-US") });

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet("orders-table");
        worksheet.Cell(1, 1).GetString().Should().Be("Customer");
        worksheet.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor.Color.ToArgb().Should().Be(unchecked((int)0xFFDDEBFF));
        worksheet.Cell(2, 1).GetString().Should().Be("ACME; Praha");
        worksheet.Cell(2, 2).GetValue<decimal>().Should().Be(1234.5m);
        worksheet.Cell(2, 2).Style.NumberFormat.Format.Should().Be("#,##0.00");
        worksheet.Cell(2, 3).GetDateTime().Should().Be(new DateTime(2025, 12, 31));
        worksheet.Cell(2, 3).Style.DateFormat.Format.Should().Be("yyyy-mm-dd");
    }

    private static (ReportDefinition Definition, ReportProcessingContext Context) CreateReport()
    {
        var definition = new ReportDefinition
        {
            Id = "orders-export",
            Name = "Orders Export",
            DataSets =
            [
                new ReportDataSetDefinition
                {
                    Name = "Orders",
                    Fields =
                    [
                        new ReportDataSetField("Customer", DefinitionFieldType.String),
                        new ReportDataSetField("Total", DefinitionFieldType.Number),
                        new ReportDataSetField("Issued", DefinitionFieldType.Date),
                    ],
                },
            ],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 80,
                    Elements =
                    [
                        new ReportTableElement
                        {
                            Id = "orders-table",
                            DataSetName = "Orders",
                            Columns =
                            [
                                new ReportTableColumn("Customer", 160),
                                new ReportTableColumn("Total", 80),
                                new ReportTableColumn("Issued", 90),
                            ],
                            Header = new ReportTableRow
                            {
                                BackgroundColor = "#ddebff",
                                Cells =
                                [
                                    new ReportTableCell { Text = "Customer", TextStyle = new ReportTextStyle { Bold = true } },
                                    new ReportTableCell { Text = "Total", TextStyle = new ReportTextStyle { Bold = true } },
                                    new ReportTableCell { Text = "Issued", TextStyle = new ReportTextStyle { Bold = true } },
                                ],
                            },
                            Detail = new ReportTableRow
                            {
                                Cells =
                                [
                                    new ReportTableCell { Expression = "=Fields.Customer" },
                                    new ReportTableCell { Expression = "=Fields.Total", NumberFormat = "#,##0.00" },
                                    new ReportTableCell { Expression = "=Fields.Issued", NumberFormat = "yyyy-mm-dd" },
                                ],
                            },
                        },
                    ],
                },
            },
        };
        var dataSet = new ProcessedDataSet(
            "Orders",
            [
                new ReportDataColumn("Customer", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
                new ReportDataColumn("Issued", DataFieldType.Date),
            ],
            [
                new ProcessedDataRow(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Customer"] = "ACME; Praha",
                    ["Total"] = 1234.5m,
                    ["Issued"] = new DateTime(2025, 12, 31),
                }),
                new ProcessedDataRow(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Customer"] = "Quote \"Ltd\"",
                    ["Total"] = 89.25m,
                    ["Issued"] = new DateTime(2026, 1, 1),
                }),
            ]);
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "cs-CZ"),
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal),
            new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal)
            {
                ["Orders"] = dataSet,
            },
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        return (definition, context);
    }
}

#pragma warning restore MA0051
