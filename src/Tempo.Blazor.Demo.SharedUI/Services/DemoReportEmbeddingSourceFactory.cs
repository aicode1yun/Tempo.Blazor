using System.Globalization;
using System.Net.Http.Headers;
using Tempo.Blazor.Reporting.Models;
using Tempo.Blazor.Reporting.Services;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using DefinitionFieldType = Tempo.Reporting.Abstractions.Definitions.ReportDataFieldType;

namespace Tempo.Blazor.Demo.SharedUI.Services;

/// <summary>Creates report sources for the embedded reporting demo page.</summary>
public sealed class DemoReportEmbeddingSourceFactory
{
    /// <summary>Report id used by the local embedded demo.</summary>
    public const string EmbeddedReportId = "embedded-sales-workspace";

    /// <summary>Report id exposed by the report server demo API.</summary>
    public const string RemoteReportId = "sales-dashboard";

    /// <summary>Deterministic demo API key accepted by the report server sample.</summary>
    public const string DemoApiKey = "tmr_demo_embed_key";

    /// <summary>Header used by report server embedding API keys.</summary>
    public const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>Creates a report source that runs fully inside the host application.</summary>
    public IReportSource CreateEmbeddedSource()
        => new EmbeddedReportSource(CreateEmbeddedDefinition(), new DemoSalesDataProvider());

    /// <summary>Creates a report source backed by a remote report server API.</summary>
    public IReportSource CreateRemoteSource(
        HttpClient httpClient,
        string apiKey,
        string reportId = RemoteReportId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        httpClient.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(ApiKeyHeaderName, apiKey.Trim());
        }

        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return new RemoteReportSource(httpClient, reportId);
    }

    /// <summary>Creates initial parameter values for the embedded report.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue> CreateDefaultParameters()
        => new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
        {
            ["Region"] = ReportParameterValue.Scalar("EU"),
            ["MinimumTotal"] = ReportParameterValue.Scalar(1_000m),
            ["IncludeClosed"] = ReportParameterValue.Scalar(true),
        };

    private static ReportDefinition CreateEmbeddedDefinition()
        => new()
        {
            Id = EmbeddedReportId,
            Name = "Foreign App Revenue Pack",
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(760, 560),
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
                        Text("title", "Foreign App Revenue Pack", 0, 0, 360, 24, 18, bold: true),
                        Text("tenant", "Tenant: northwind | User: embedded-user", 430, 5, 280, 16, 10, ReportHorizontalAlignment.Right),
                        new ReportLineElement
                        {
                            Id = "rule",
                            X = 0,
                            Y = 34,
                            Width = 712,
                            Height = 0,
                            Stroke = new ReportBorderLine("#94a3b8", 1),
                        },
                    ],
                },
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 184,
                    KeepTogether = true,
                    Elements =
                    [
                        Chart(
                            "embedded-status-column",
                            ReportChartType.Column,
                            "Revenue by status",
                            "Status",
                            "Revenue",
                            0,
                            0,
                            330,
                            160,
                            "=Fields.Status",
                            "=Fields.Total",
                            "#2563eb"),
                        Chart(
                            "embedded-customer-line",
                            ReportChartType.Line,
                            "Customer trend",
                            "Customer",
                            "Revenue",
                            360,
                            0,
                            352,
                            160,
                            "=Fields.Customer",
                            "=Fields.Total",
                            "#14b8a6"),
                    ],
                },
                Detail = new ReportBand { Kind = ReportBandKind.Detail, Height = 0 },
                ReportFooter = new ReportBand
                {
                    Kind = ReportBandKind.ReportFooter,
                    Height = 280,
                    Elements =
                    [
                        SalesTable(),
                    ],
                },
                PageFooter = new ReportBand
                {
                    Kind = ReportBandKind.PageFooter,
                    Height = 24,
                    Elements =
                    [
                        Text("page", "Page PageNumber / TotalPages", 560, 4, 150, 14, 10, ReportHorizontalAlignment.Right),
                    ],
                },
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
                DefaultExpression = "=1000",
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
                Source = new ReportDataSourceReference { Name = "Demo commerce data" },
                Query = "select Customer, Region, Total, Status from DemoSales",
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
                Source = new ReportDataSourceReference { Name = "Demo commerce data" },
                Query = "select Code, Name from DemoRegions",
                Fields =
                [
                    new ReportDataSetField("Code", DefinitionFieldType.String),
                    new ReportDataSetField("Name", DefinitionFieldType.String),
                ],
            },
        ];

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

    private static ReportTableElement SalesTable()
        => new()
        {
            Id = "embedded-sales-table",
            X = 0,
            Y = 0,
            Width = 690,
            Height = 260,
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
                    new ReportTableCell { Expression = "=Fields.Customer" },
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

    private static ReportTextStyle TextStyle(double fontSize, bool bold = false)
        => new()
        {
            FontFamily = "Inter",
            FontSize = fontSize,
            Bold = bold,
            Color = "#111827",
        };

    private sealed class DemoSalesDataProvider : IReportDataProvider
    {
        public async Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
        {
            await Task.Yield();
            context.CancellationToken.ThrowIfCancellationRequested();
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
                ? Convert.ToString(regionValue.ScalarValue, CultureInfo.InvariantCulture)
                : "EU";
            var minimumTotal = parameters.TryGetValue("MinimumTotal", out var minimumValue)
                ? Convert.ToDecimal(minimumValue.ScalarValue ?? 0, CultureInfo.InvariantCulture)
                : 0m;
            var includeClosed = !parameters.TryGetValue("IncludeClosed", out var includeClosedValue) ||
                Convert.ToBoolean(includeClosedValue.ScalarValue ?? true, CultureInfo.InvariantCulture);
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
            for (var index = 1; index <= 16; index++)
            {
                yield return ($"Europe Channel {index:00}", "EU", 880 + index * 57, index % 4 == 0 ? "Closed" : "Open");
            }

            for (var index = 1; index <= 12; index++)
            {
                yield return ($"US Channel {index:00}", "US", 780 + index * 62, index % 3 == 0 ? "Closed" : "Open");
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
