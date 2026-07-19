using Tempo.Blazor.Reporting.Models;
using Tempo.Blazor.Reporting.Services;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Serialization;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using DefinitionFieldType = Tempo.Reporting.Abstractions.Definitions.ReportDataFieldType;

namespace Tempo.ReportServer.Web.Services;

/// <summary>Builds in-memory report sources for the report server demo.</summary>
public sealed class DemoReportSourceFactory
{
    // The rich, hand-authored demo definitions this factory can supply sample DATA for. Any other id
    // is a real (created/uploaded) report whose definition must come from the catalog, never from here.
    private static readonly HashSet<string> KnownDemoReportIds = new(StringComparer.Ordinal)
    {
        "sales-register",
        "sales-dashboard",
        "invoice-aging",
        "margin-watch",
        "fulfillment-sla",
    };

    /// <summary>Creates the sales register report source.</summary>
    public IReportSource CreateSalesRegister()
        => CreateReportSource("sales-register");

    /// <summary>Whether this factory has a hand-authored demo definition (with sample data) for the id.</summary>
    public bool IsKnownDemoReport(string reportId)
        => KnownDemoReportIds.Contains(reportId);

    /// <summary>Creates a report source for a catalog report id.</summary>
    public IReportSource CreateReportSource(string reportId)
        => new EmbeddedReportSource(CreateReportDefinition(reportId), new SalesRegisterProvider());

    /// <summary>
    /// Builds an in-process preview source for an ARBITRARY report definition (a real created/uploaded
    /// report). The self-contained portal has no live data source for such a report, so the preview shows
    /// the report's real layout (title, bands, tables) with EMPTY data — never another report's content.
    /// </summary>
    public IReportSource CreateReportSourceFromDefinition(ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new EmbeddedReportSource(definition, new EmptyDefinitionProvider(definition));
    }

    /// <summary>
    /// Parses a canonical report-definition JSON into a <see cref="ReportDefinition"/> using the SAME
    /// canonical serializer the server writes/reads it with (so custom converters like ReportPageSize.unit
    /// round-trip), or returns <see langword="null"/> when the payload is missing/blank/unparseable or
    /// carries no meaningful content (e.g. the <c>"{}"</c> placeholder), so callers can fall back to a demo
    /// definition or a graceful state.
    /// </summary>
    public static ReportDefinition? TryParseDefinition(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return null;
        }

        try
        {
            var definition = ReportDefinitionJsonSerializer.Deserialize(definitionJson);
            var hasContent = !string.IsNullOrWhiteSpace(definition.Name)
                || definition.DataSets.Count > 0
                || definition.Bands.PageHeader is not null
                || definition.Bands.ReportHeader is not null
                || definition.Bands.Detail is not null
                || definition.Bands.ReportFooter is not null
                || definition.Bands.PageFooter is not null;
            return hasContent ? definition : null;
        }
        catch (ReportDefinitionJsonException)
        {
            return null;
        }
    }

    /// <summary>Creates an editable report definition for a catalog report id.</summary>
    public ReportDefinition CreateReportDefinition(string reportId)
    {
        if (string.Equals(reportId, "sales-dashboard", StringComparison.Ordinal))
        {
            return CreateSalesDashboardDefinition();
        }

        var title = reportId switch
        {
            "invoice-aging" => "Invoice Aging",
            "margin-watch" => "Margin Watch",
            "fulfillment-sla" => "Fulfillment SLA",
            _ => "Sales Register",
        };

        return CreateDefinition(reportId, title);
    }

    private static ReportDefinition CreateDefinition(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(560, 360),
                Margins = new ReportThickness(24),
            },
            Parameters = CreateParameters(),
            DataSets = CreateDataSets(),
            Bands = CreateBands(name),
        };

    private static ReportDefinition CreateSalesDashboardDefinition()
        => new()
        {
            Id = "sales-dashboard",
            Name = "Dashboard prodejů",
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(760, 620),
                Margins = new ReportThickness(24),
            },
            Parameters = CreateParameters(),
            DataSets = CreateDataSets(),
            Bands = new ReportBandCollection
            {
                PageHeader = new ReportBand
                {
                    Kind = ReportBandKind.PageHeader,
                    Height = 44,
                    Elements =
                    [
                        Text("dashboard-title", "Dashboard prodejů", 0, 0, 340, 24, 18, bold: true),
                        Text("dashboard-subtitle", "Region EU | součet 22 527 | 18 objednávek", 420, 5, 280, 16, 10, ReportHorizontalAlignment.Right),
                        new ReportLineElement
                        {
                            Id = "dashboard-rule",
                            X = 0,
                            Y = 34,
                            Width = 712,
                            Height = 0,
                            Stroke = new ReportBorderLine("#cbd5e1", 1),
                        },
                    ],
                },
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 192,
                    KeepTogether = true,
                    Elements =
                    [
                        Chart(
                            "status-column",
                            ReportChartType.Column,
                            "Revenue by status",
                            "Status",
                            "Revenue",
                            0,
                            0,
                            218,
                            170,
                            "=Fields.Status",
                            "=Fields.Total",
                            "#2563eb"),
                        Chart(
                            "customer-line",
                            ReportChartType.Line,
                            "Orders by customer",
                            "Customer",
                            "Revenue",
                            240,
                            0,
                            244,
                            170,
                            "=Fields.Customer",
                            "=Fields.Total",
                            "#14b8a6"),
                        Chart(
                            "status-donut",
                            ReportChartType.Donut,
                            "Status mix",
                            "Status",
                            "Revenue",
                            506,
                            0,
                            206,
                            170,
                            "=Fields.Status",
                            "=Fields.Total",
                            "#f59e0b"),
                    ],
                },
                Detail = new ReportBand { Kind = ReportBandKind.Detail, Height = 0 },
                ReportFooter = new ReportBand
                {
                    Kind = ReportBandKind.ReportFooter,
                    Height = 344,
                    Elements =
                    [
                        SalesDashboardTable(),
                    ],
                },
                PageFooter = CreatePageFooterBand(),
            },
        };

    private static List<ReportParameterDefinition> CreateParameters()
        =>
        [
            new ReportParameterDefinition
            {
                Name = "Region",
                Label = "Region",
                DataType = ReportParameterType.List,
                DefaultExpression = "=\"EU\"",
                AvailableValues = ReportParameterAvailableValues.FromDataSet("Regions", "Code", "Name"),
            },
            new ReportParameterDefinition
            {
                Name = "MinimumTotal",
                Label = "Minimum total",
                DataType = ReportParameterType.Number,
                DefaultExpression = "=0",
                Required = false,
            },
            new ReportParameterDefinition
            {
                Name = "IncludeClosed",
                Label = "Include closed orders",
                DataType = ReportParameterType.Boolean,
                DefaultExpression = "=true",
                Required = false,
            },
        ];

    private static List<ReportDataSetDefinition> CreateDataSets()
        =>
        [
            new ReportDataSetDefinition
            {
                Name = "Sales",
                Source = new ReportDataSourceReference { Name = "ERP SQL" },
                Query = "select Customer, Region, Total, Status from Sales",
                Fields =
                [
                    new ReportDataSetField("Customer", DefinitionFieldType.String),
                    new ReportDataSetField("Region", DefinitionFieldType.String),
                    new ReportDataSetField("Total", DefinitionFieldType.Number),
                    new ReportDataSetField("Status", DefinitionFieldType.String),
                ],
            },
            new ReportDataSetDefinition
            {
                Name = "Regions",
                Source = new ReportDataSourceReference { Name = "ERP SQL" },
                Query = "select Code, Name from Regions",
                Fields =
                [
                    new ReportDataSetField("Code", DefinitionFieldType.String),
                    new ReportDataSetField("Name", DefinitionFieldType.String),
                ],
            },
        ];

    private static ReportBandCollection CreateBands(string title)
        => new()
        {
            PageHeader = CreatePageHeaderBand(title),
            Detail = CreateDetailBand(),
            PageFooter = CreatePageFooterBand(),
        };

    private static ReportBand CreatePageHeaderBand(string title)
        => new()
        {
            Kind = ReportBandKind.PageHeader,
            Height = 36,
            Elements =
            [
                Text("title", title, 0, 0, 220, 22, 16, bold: true),
                new ReportLineElement
                {
                    Id = "rule",
                    X = 0,
                    Y = 30,
                    Width = 512,
                    Height = 0,
                    Stroke = new ReportBorderLine("#94a3b8", 1),
                },
            ],
        };

    private static ReportBand CreateDetailBand()
        => new()
        {
            Kind = ReportBandKind.Detail,
            Height = 30,
            Elements =
            [
                FieldText("customer", "=Fields.Customer", 0, 0, 170, 18) with
                {
                    // Definition-driven drill-through: clicking a customer opens the sales register scoped to
                    // that row's region. The engine projects this into an anchored region with the row context.
                    DrillThrough = new ReportDrillThroughAction
                    {
                        TargetReportPath = "Finance/Sales Register",
                        ParameterMappings =
                        [
                            new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Field, "Region"),
                        ],
                    },
                },
                FieldText("region", "=Fields.Region", 180, 0, 70, 18),
                FieldText("total", "=Fields.Total", 270, 0, 100, 18, ReportHorizontalAlignment.Right),
                FieldText("status", "=Fields.Status", 400, 0, 90, 18),
            ],
        };

    private static ReportBand CreatePageFooterBand()
        => new()
        {
            Kind = ReportBandKind.PageFooter,
            Height = 24,
            Elements =
            [
                Text("page", "Page PageNumber / TotalPages", 360, 4, 150, 14, 10, ReportHorizontalAlignment.Right),
            ],
        };

    private static ReportChartElement Chart(
        string id,
        ReportChartType type,
        string title,
        string categoryAxisTitle,
        string valueAxisTitle,
        double x,
        double y,
        double width,
        double height,
        string categoryExpression,
        string valueExpression,
        string color)
        => new()
        {
            Id = id,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ChartType = type,
            DataSetName = "Sales",
            Title = title,
            CategoryAxisTitle = categoryAxisTitle,
            ValueAxisTitle = valueAxisTitle,
            ShowLegend = true,
            ShowValueAxis = type is not (ReportChartType.Pie or ReportChartType.Donut),
            ColorPalette = ["#2563eb", "#14b8a6", "#f59e0b", "#ef4444"],
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Actual",
                    CategoryExpression = categoryExpression,
                    ValueExpression = valueExpression,
                    Color = color,
                },
            ],
        };

    private static ReportTableElement SalesDashboardTable()
        => new()
        {
            Id = "sales-dashboard-table",
            X = 0,
            Y = 0,
            Width = 690,
            Height = 320,
            DataSetName = "Sales",
            RepeatHeaderOnNewPage = true,
            ZebraStripeColor = "#f8fafc",
            Columns =
            [
                new ReportTableColumn("Customer", 270),
                new ReportTableColumn("Region", 90),
                new ReportTableColumn("Total", 140),
                new ReportTableColumn("Status", 120),
            ],
            Header = new ReportTableRow
            {
                Height = 20,
                BackgroundColor = "#e2e8f0",
                Cells =
                [
                    new ReportTableCell { Text = "Customer", TextStyle = TextStyle(10, bold: true) },
                    new ReportTableCell { Text = "Region", TextStyle = TextStyle(10, bold: true) },
                    new ReportTableCell { Text = "Total", TextStyle = TextStyle(10, bold: true), HorizontalAlignment = ReportHorizontalAlignment.Right },
                    new ReportTableCell { Text = "Status", TextStyle = TextStyle(10, bold: true) },
                ],
            },
            Detail = new ReportTableRow
            {
                Height = 18,
                Cells =
                [
                    new ReportTableCell
                    {
                        Expression = "=Fields.Customer",
                        // Demonstrates the drill-through DTO on a table cell: clicking a customer opens the
                        // sales register scoped to that row's region.
                        DrillThrough = new ReportDrillThroughAction
                        {
                            TargetReportPath = "Finance/Sales Register",
                            ParameterMappings =
                            [
                                new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Field, "Region"),
                            ],
                        },
                    },
                    new ReportTableCell { Expression = "=Fields.Region" },
                    new ReportTableCell { Expression = "=Fields.Total", NumberFormat = "#,##0.00", HorizontalAlignment = ReportHorizontalAlignment.Right },
                    new ReportTableCell { Expression = "=Fields.Status" },
                ],
            },
        };

    private static ReportTextBoxElement Text(
        string id,
        string text,
        double x,
        double y,
        double width,
        double height,
        double fontSize,
        ReportHorizontalAlignment alignment = ReportHorizontalAlignment.Left,
        bool bold = false)
        => new()
        {
            Id = id,
            Text = text,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            HorizontalAlignment = alignment,
            TextStyle = TextStyle(fontSize, bold),
        };

    private static ReportTextBoxElement FieldText(
        string id,
        string expression,
        double x,
        double y,
        double width,
        double height,
        ReportHorizontalAlignment alignment = ReportHorizontalAlignment.Left)
        => new()
        {
            Id = id,
            Expression = expression,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            HorizontalAlignment = alignment,
            TextStyle = TextStyle(11),
        };

    private static ReportTextStyle TextStyle(double fontSize, bool bold = false)
        => new()
        {
            FontFamily = "Inter",
            FontSize = fontSize,
            Bold = bold,
            Color = "#111827",
        };

    // Supplies the correct SCHEMA (columns from the definition's declared dataset fields) but ZERO rows,
    // so an arbitrary real report renders its own layout with empty data — no cross-report content.
    private sealed class EmptyDefinitionProvider : IReportDataProvider
    {
        private readonly ReportDefinition _definition;

        public EmptyDefinitionProvider(ReportDefinition definition)
            => _definition = definition;

        public Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
        {
            var dataSet = _definition.DataSets.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, dataSetName, StringComparison.Ordinal));
            var schema = (dataSet?.Fields ?? [])
                .Select(field => new ReportDataColumn(field.Name, MapFieldType(field.DataType)))
                .ToArray();
            return Task.FromResult(new ReportDataSetResult(schema, EmptyRows()));
        }

        private static DataFieldType MapFieldType(DefinitionFieldType type)
            => type switch
            {
                DefinitionFieldType.Number => DataFieldType.Number,
                DefinitionFieldType.Date => DataFieldType.Date,
                DefinitionFieldType.Boolean => DataFieldType.Boolean,
                DefinitionFieldType.Object => DataFieldType.Object,
                _ => DataFieldType.String,
            };

#pragma warning disable CS1998 // async enumerator with no awaits is the streaming contract shape
        private static async IAsyncEnumerable<ReportDataRow> EmptyRows()
        {
            yield break;
        }
#pragma warning restore CS1998
    }

    private sealed class SalesRegisterProvider : IReportDataProvider
    {
        public async Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
        {
            await Task.Delay(250, context.CancellationToken).ConfigureAwait(false);
            if (string.Equals(dataSetName, "Regions", StringComparison.Ordinal))
            {
                return new ReportDataSetResult(
                    [new ReportDataColumn("Code", DataFieldType.String), new ReportDataColumn("Name", DataFieldType.String)],
                    Rows(
                    [
                        Row(("Code", "EU"), ("Name", "Europe")),
                        Row(("Code", "US"), ("Name", "United States")),
                    ]));
            }

            var region = parameters.TryGetValue("Region", out var regionValue)
                ? Convert.ToString(regionValue.ScalarValue, System.Globalization.CultureInfo.InvariantCulture)
                : "EU";
            var minimumTotal = parameters.TryGetValue("MinimumTotal", out var minimumValue)
                ? Convert.ToDecimal(minimumValue.ScalarValue ?? 0, System.Globalization.CultureInfo.InvariantCulture)
                : 0m;
            var includeClosed = !parameters.TryGetValue("IncludeClosed", out var includeClosedValue) ||
                Convert.ToBoolean(includeClosedValue.ScalarValue ?? true, System.Globalization.CultureInfo.InvariantCulture);
            var rows = SalesRows()
                .Where(row => string.Equals(row.Region, region, StringComparison.Ordinal))
                .Where(row => row.Total >= minimumTotal)
                .Where(row => includeClosed || !string.Equals(row.Status, "Closed", StringComparison.Ordinal))
                .Select(row => Row(
                    ("Customer", row.Customer),
                    ("Region", row.Region),
                    ("Total", row.Total),
                    ("Status", row.Status)));

            return new ReportDataSetResult(
                [
                    new ReportDataColumn("Customer", DataFieldType.String),
                    new ReportDataColumn("Region", DataFieldType.String),
                    new ReportDataColumn("Total", DataFieldType.Number),
                    new ReportDataColumn("Status", DataFieldType.String),
                ],
                Rows(rows));
        }

        private static IEnumerable<(string Customer, string Region, decimal Total, string Status)> SalesRows()
        {
            for (var index = 1; index <= 18; index++)
            {
                yield return ($"Europe Customer {index:00}", "EU", 900 + index * 37, index % 4 == 0 ? "Closed" : "Open");
            }

            for (var index = 1; index <= 14; index++)
            {
                yield return ($"US Customer {index:00}", "US", 760 + index * 42, index % 3 == 0 ? "Closed" : "Open");
            }
        }

        private static IReadOnlyDictionary<string, object?> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);

        private static async IAsyncEnumerable<ReportDataRow> Rows(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
        {
            foreach (var row in rows)
            {
                await Task.Yield();
                yield return new ReportDataRow(row);
            }
        }
    }
}
