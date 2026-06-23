using Tempo.Blazor.Reporting.Models;
using Tempo.Blazor.Reporting.Services;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Snapshot;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Blazor.Reporting.Tests.Services;

public sealed class EmbeddedReportSourceTests
{
    [Fact]
    public async Task RenderAsync_UsesParametersToDriveReportContent()
    {
        var source = new EmbeddedReportSource(Definition(), new RegionFilteringProvider());

        var result = await source.RenderAsync(new ReportViewerRenderRequest
        {
            Parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
            },
        });

        var text = SnapshotText(result.Snapshot);
        text.Should().Contain("Ada");
        text.Should().NotContain("Bea");
    }

    [Fact]
    public async Task GetMetadataAsync_ResolvesListOptions()
    {
        var source = new EmbeddedReportSource(Definition(), new RegionFilteringProvider());

        var metadata = await source.GetMetadataAsync(new ReportViewerMetadataRequest());

        metadata.Parameters.Single().Options
            .Select(option => option.Value)
            .Should().BeEquivalentTo(["EU", "US"]);
    }

    [Fact]
    public async Task RenderAsync_ReturnsResolvedDefaultParameters()
    {
        var source = new EmbeddedReportSource(Definition(), new RegionFilteringProvider());

        var result = await source.RenderAsync(new ReportViewerRenderRequest());

        result.Parameters.Should().NotBeNull();
        result.Parameters!["Region"].ScalarValue.Should().Be("EU");
    }

    [Fact]
    public async Task ExportPdfAsync_RendersPdfBytes()
    {
        var source = new EmbeddedReportSource(Definition(), new RegionFilteringProvider());

        var export = await source.ExportPdfAsync(new ReportViewerRenderRequest
        {
            Parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("US"),
            },
        });

        export.ContentType.Should().Be("application/pdf");
        export.Bytes.Take(4).Should().Equal([0x25, 0x50, 0x44, 0x46]);
    }

    private static string SnapshotText(ReportSnapshot snapshot)
        => string.Join(
            " ",
            snapshot.Pages
                .SelectMany(page => page.Commands)
                .Where(command => command.Type == ReportSnapshotCommandType.TextRun)
                .Select(command => command.Text));

    private static ReportDefinition Definition()
        => new()
        {
            Id = "sales-by-region",
            Name = "Sales by region",
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(320, 220),
                Margins = new ReportThickness(16),
            },
            Parameters =
            [
                new ReportParameterDefinition
                {
                    Name = "Region",
                    Label = "Region",
                    DataType = ReportParameterType.List,
                    DefaultExpression = "=\"EU\"",
                    AvailableValues = ReportParameterAvailableValues.FromDataSet("Regions", "Code", "Name"),
                },
            ],
            DataSets =
            [
                new ReportDataSetDefinition { Name = "Sales" },
                new ReportDataSetDefinition { Name = "Regions" },
            ],
            Bands = new ReportBandCollection
            {
                Detail = new ReportBand
                {
                    Kind = ReportBandKind.Detail,
                    Height = 24,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "customer",
                            X = 0,
                            Y = 0,
                            Width = 180,
                            Height = 18,
                            Expression = "=Fields.Customer",
                            TextStyle = new ReportTextStyle { FontFamily = "Inter", FontSize = 12 },
                        },
                    ],
                },
            },
        };

    private sealed class RegionFilteringProvider : IReportDataProvider
    {
        public Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
        {
            if (string.Equals(dataSetName, "Regions", StringComparison.Ordinal))
            {
                return Task.FromResult(new ReportDataSetResult(
                    [new ReportDataColumn("Code", DataFieldType.String), new ReportDataColumn("Name", DataFieldType.String)],
                    Rows(
                    [
                        Row(("Code", "EU"), ("Name", "Europe")),
                        Row(("Code", "US"), ("Name", "United States")),
                    ])));
            }

            var region = parameters.TryGetValue("Region", out var value)
                ? Convert.ToString(value.ScalarValue, System.Globalization.CultureInfo.InvariantCulture)
                : null;
            var rows = new[]
            {
                Row(("Region", "EU"), ("Customer", "Ada")),
                Row(("Region", "US"), ("Customer", "Bea")),
            }.Where(row => string.Equals(Convert.ToString(row["Region"], System.Globalization.CultureInfo.InvariantCulture), region, StringComparison.Ordinal));

            return Task.FromResult(new ReportDataSetResult(
                [new ReportDataColumn("Region", DataFieldType.String), new ReportDataColumn("Customer", DataFieldType.String)],
                Rows(rows)));
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
