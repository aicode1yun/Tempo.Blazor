using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class ChartLayoutTests
{
    [Fact]
    public void Generate_ColumnChartCreatesDeterministicBarsAxesGridAndLegend()
    {
        var chart = Chart(ReportChartType.Column, width: 240, height: 160);
        var snapshot = Generate(chart);

        var commands = snapshot.Pages.Single().Commands;
        commands.Should().Contain(command => command.Id.EndsWith("chart-plot", StringComparison.Ordinal) && command.Type == ReportSnapshotCommandType.Rectangle);
        commands.Should().Contain(command => command.Id.EndsWith("chart-grid-2", StringComparison.Ordinal) && command.Type == ReportSnapshotCommandType.Line);
        commands.Should().Contain(command => command.Id.EndsWith("chart-axis-y", StringComparison.Ordinal) && command.Type == ReportSnapshotCommandType.Line);
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "Sales dashboard");
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "North");
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "120");
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "Actual");

        var bars = commands
            .Where(command => command.Id.Contains("chart-actual-bar-", StringComparison.Ordinal))
            .ToArray();
        bars.Should().HaveCount(3);
        bars[0].X.Should().BeApproximately(62.17, 0.02);
        bars[0].Y.Should().BeApproximately(56.67, 0.02);
        bars[0].Height.Should().BeApproximately(53.33, 0.02);
        bars[0].Fill.Should().Be("#2563eb");
        bars[2].Y.Should().BeApproximately(30, 0.02);
        bars[2].Height.Should().BeApproximately(80, 0.02);
    }

    [Fact]
    public void Generate_LineChartCreatesPolylinePathAndPointMarkers()
    {
        var snapshot = Generate(Chart(ReportChartType.Line, width: 240, height: 160));
        var commands = snapshot.Pages.Single().Commands;

        commands.Should().Contain(command =>
            command.Id.EndsWith("chart-actual-line", StringComparison.Ordinal) &&
            command.Type == ReportSnapshotCommandType.Path &&
            command.PathData == "M 70 56.67 L 134 83.33 L 198 30");
        commands.Where(command => command.Id.Contains("chart-actual-point-", StringComparison.Ordinal))
            .Should().HaveCount(3)
            .And.AllSatisfy(command => command.Type.Should().Be(ReportSnapshotCommandType.Path));
    }

    [Fact]
    public void Generate_DonutChartCreatesArcSegmentsAndCenterHole()
    {
        var snapshot = Generate(Chart(ReportChartType.Donut, width: 220, height: 160));
        var commands = snapshot.Pages.Single().Commands;

        commands.Where(command => command.Id.Contains("chart-actual-slice-", StringComparison.Ordinal))
            .Should().HaveCount(3)
            .And.AllSatisfy(command => command.Type.Should().Be(ReportSnapshotCommandType.Path));
        commands.Should().Contain(command =>
            command.Id.EndsWith("chart-donut-hole", StringComparison.Ordinal) &&
            command.Type == ReportSnapshotCommandType.Path &&
            command.Fill == "#ffffff");
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "North");
        commands.Should().Contain(command => command.Type == ReportSnapshotCommandType.TextRun && command.Text == "Actual");
    }

    private static ReportChartElement Chart(ReportChartType type, double width, double height)
        => new()
        {
            Id = "chart",
            X = 0,
            Y = 0,
            Width = width,
            Height = height,
            ChartType = type,
            DataSetName = "Sales",
            Title = "Sales dashboard",
            CategoryAxisTitle = "Region",
            ValueAxisTitle = "Revenue",
            ColorPalette = ["#2563eb", "#14b8a6", "#f59e0b"],
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Actual",
                    CategoryExpression = "=Fields.Region",
                    ValueExpression = "=Fields.Total",
                    Color = "#2563eb",
                },
            ],
        };

    private static ReportSnapshot Generate(ReportChartElement chart)
    {
        var definition = new ReportDefinition
        {
            Id = "dashboard",
            Name = "Dashboard",
            PageSetup = new ReportPageSetup
            {
                PageSize = new ReportPageSize(320, 220),
                Margins = new ReportThickness(20),
            },
        };
        var band = new ReportBand
        {
            Kind = ReportBandKind.Detail,
            Height = chart.Height,
            Elements = [chart],
        };
        var dataSet = new ProcessedDataSet(
            "Sales",
            [
                new ReportDataColumn("Region", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
            ],
            [
                Row("North", 80m),
                Row("South", 40m),
                Row("West", 120m),
            ]);
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal) { ["Sales"] = dataSet });
        var instance = new ReportInstance(
            definition,
            [new ReportBandInstance(ReportBandKind.Detail, null, null, [new ReportElementInstance(chart, null, null)], sourceBand: band)],
            context.DataSets,
            context);

        return ReportSnapshotGenerator.Generate(instance, new FixedTextMeasurer(), new ReportSnapshotGeneratorOptions { SnapshotId = "chart-test" });
    }

    private static ProcessedDataRow Row(string region, decimal total)
        => new(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Region"] = region,
            ["Total"] = total,
        });
}
