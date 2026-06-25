using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Tests.Validation;
using Tempo.Reporting.Abstractions.Validation;

namespace Tempo.Reporting.Abstractions.Tests.Definitions;

public sealed class ReportDefinitionModelTests
{
    [Fact]
    public void ReportDefinition_CanDescribeCoreBandsElementsParametersDatasetsAndStyles()
    {
        var definition = CreateValidDefinition();

        definition.SchemaVersion.Should().Be(ReportDefinition.CurrentSchemaVersion);
        definition.PageSetup.PageSize.Should().Be(ReportPageSize.A4);
        definition.PageSetup.Margins.Left.Should().Be(36);
        definition.Parameters.Should().ContainSingle(p => p.Name == "Region")
            .Which.AvailableValues!.StaticValues.Should().ContainSingle(v => v.Label == "Europe");
        definition.DataSets.Should().ContainSingle(d => d.Name == "Orders");
        definition.Styles.Should().ContainSingle(s => s.Id == "Heading")
            .Which.Text.Bold.Should().BeTrue();
        definition.Bands.ReportHeader!.Elements.Should().Contain(e => e is ReportTextBoxElement);
        definition.Bands.ReportHeader.Elements.Should().Contain(e => e is ReportImageElement);
        definition.Bands.ReportHeader.Elements.Should().Contain(e => e is ReportShapeElement);
        definition.Bands.ReportHeader.Elements.Should().Contain(e => e is ReportLineElement);
        definition.Bands.ReportHeader.Elements.Should().Contain(e => e is ReportTableElement);
        definition.Bands.ReportHeader.Elements.OfType<ReportTableElement>().Single()
            .Detail.Cells[1].NumberFormat.Should().Be("#,##0.00");
        definition.Bands.ReportHeader.Elements.Should().Contain(e => e is ReportChartElement);
        var chart = definition.Bands.ReportHeader.Elements.OfType<ReportChartElement>().Single();
        chart.ChartType.Should().Be(ReportChartType.Donut);
        chart.ColorPalette.Should().Contain(["#2563eb", "#14b8a6"]);
        chart.Series.Should().ContainSingle().Which.Color.Should().Be("#2563eb");
        definition.Bands.ReportHeader.Elements.Should().Contain(e => e is ReportSubReportElement);
        definition.Bands.Groups.Should().ContainSingle(g => g.Name == "ByRegion");

        var result = new ReportDefinitionValidator(ReportingValidationTestLocalizer.Create()).Validate(definition);

        result.IsValid.Should().BeTrue();
    }

    public static ReportDefinition CreateValidDefinition()
    {
        return new ReportDefinition
        {
            Id = "monthly-orders",
            Name = "Monthly orders",
            Description = "Operational monthly order report.",
            PageSetup = new ReportPageSetup
            {
                PageSize = ReportPageSize.A4,
                Orientation = ReportPageOrientation.Portrait,
                Margins = new ReportThickness(36, 48, 36, 48),
            },
            Parameters =
            [
                new ReportParameterDefinition
                {
                    Name = "Region",
                    Label = "Region",
                    DataType = ReportParameterType.List,
                    DefaultExpression = "=\"EU\"",
                    AllowMultipleValues = true,
                    AvailableValues = ReportParameterAvailableValues.Static(
                    [
                        new ReportParameterAvailableValue("EU", "Europe"),
                        new ReportParameterAvailableValue("NA", "North America"),
                    ]),
                },
            ],
            DataSets =
            [
                new ReportDataSetDefinition
                {
                    Name = "Orders",
                    Source = new ReportDataSourceReference { Name = "orders-db" },
                    Query = "select id, region, total from orders where region in (@Region)",
                    Fields =
                    [
                        new ReportDataSetField("Region", ReportDataFieldType.String),
                        new ReportDataSetField("Total", ReportDataFieldType.Number),
                    ],
                    Parameters =
                    [
                        new ReportDataSetParameterBinding("Region", "=Parameters.Region"),
                    ],
                },
            ],
            Styles =
            [
                new ReportStyleDefinition
                {
                    Id = "Heading",
                    Text = new ReportTextStyle
                    {
                        FontFamily = "Inter",
                        FontSize = 18,
                        Bold = true,
                        Color = "#111827",
                    },
                    Padding = new ReportThickness(4),
                },
            ],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 160,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "title",
                            X = 36,
                            Y = 24,
                            Width = 420,
                            Height = 32,
                            Text = "Monthly orders",
                            CanGrow = true,
                            HorizontalAlignment = ReportHorizontalAlignment.Left,
                            VerticalAlignment = ReportVerticalAlignment.Middle,
                            Padding = new ReportThickness(4, 2, 4, 2),
                            Border = new ReportBorder { Bottom = new ReportBorderLine("#d1d5db", 1) },
                            TextStyle = new ReportTextStyle { FontFamily = "Inter", FontSize = 18, Bold = true },
                        },
                        new ReportImageElement
                        {
                            Id = "logo",
                            X = 500,
                            Y = 20,
                            Width = 48,
                            Height = 48,
                            SourceKind = ReportImageSourceKind.Url,
                            Source = "https://example.invalid/logo.png",
                            Sizing = ReportImageSizingMode.Contain,
                        },
                        new ReportShapeElement
                        {
                            Id = "summary-box",
                            X = 36,
                            Y = 76,
                            Width = 512,
                            Height = 44,
                            Shape = ReportShapeKind.RoundedRectangle,
                            FillColor = "#f9fafb",
                            Border = ReportBorder.All("#d1d5db", 1),
                        },
                        new ReportLineElement
                        {
                            Id = "header-line",
                            X = 36,
                            Y = 132,
                            Width = 512,
                            Height = 0,
                            Stroke = new ReportBorderLine("#9ca3af", 1),
                        },
                        new ReportTableElement
                        {
                            Id = "orders-table",
                            X = 36,
                            Y = 136,
                            Width = 512,
                            Height = 20,
                            DataSetName = "Orders",
                            Columns =
                            [
                                new ReportTableColumn("Region", 180),
                                new ReportTableColumn("Total", 120),
                            ],
                            Detail = new ReportTableRow
                            {
                                Cells =
                                [
                                new ReportTableCell { Text = "=Fields.Region" },
                                    new ReportTableCell { Text = "=Fields.Total", NumberFormat = "#,##0.00" },
                                ],
                            },
                        },
                        new ReportChartElement
                        {
                            Id = "orders-chart",
                            X = 36,
                            Y = 160,
                            Width = 240,
                            Height = 120,
                            ChartType = ReportChartType.Donut,
                            DataSetName = "Orders",
                            Title = "Sales by region",
                            ShowLegend = true,
                            ShowValueAxis = false,
                            ColorPalette = ["#2563eb", "#14b8a6"],
                            Series =
                            [
                                new ReportChartSeries
                                {
                                    Name = "Total",
                                    CategoryExpression = "=Fields.Region",
                                    ValueExpression = "=Fields.Total",
                                    Color = "#2563eb",
                                },
                            ],
                        },
                        new ReportSubReportElement
                        {
                            Id = "tax-subreport",
                            X = 300,
                            Y = 160,
                            Width = 248,
                            Height = 120,
                            ReportId = "tax-summary",
                            ParameterMappings =
                            [
                                new ReportSubReportParameterMapping("Region", "=Parameters.Region"),
                            ],
                        },
                    ],
                },
                PageHeader = new ReportBand { Kind = ReportBandKind.PageHeader, Height = 32 },
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 24,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "detail-total",
                            X = 36,
                            Y = 0,
                            Width = 120,
                            Height = 18,
                            Expression = "=Fields.Total",
                        },
                    ],
                },
                PageFooter = new ReportBand { Kind = ReportBandKind.PageFooter, Height = 32 },
                Groups =
                [
                    new ReportGroupDefinition
                    {
                        Name = "ByRegion",
                        Expression = "=Fields.Region",
                        GroupHeader = new ReportBand { Kind = ReportBandKind.GroupHeader, Height = 24 },
                        GroupFooter = new ReportBand { Kind = ReportBandKind.GroupFooter, Height = 24 },
                    },
                ],
            },
        };
    }
}
