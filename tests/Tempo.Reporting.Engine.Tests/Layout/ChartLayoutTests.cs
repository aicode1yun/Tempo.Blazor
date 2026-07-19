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

    [Fact]
    public void Generate_StackedColumnChartStacksSegmentsCumulativelyWithExactAreaRatios()
    {
        // North: Actual=40, Target=20 (stack total 60 -> nice axis maps to 60, filling the plot).
        // South: Actual=10, Target=20 (stack total 30).
        var snapshot = GenerateStacked(StackedChart(ReportChartType.StackedColumn, width: 240, height: 160));
        var commands = snapshot.Pages.Single().Commands;

        var actualBars = commands
            .Where(command => command.Id.Contains("chart-actual-bar-", StringComparison.Ordinal))
            .OrderBy(command => command.Id, StringComparer.Ordinal)
            .ToArray();
        var targetBars = commands
            .Where(command => command.Id.Contains("chart-target-bar-", StringComparison.Ordinal))
            .OrderBy(command => command.Id, StringComparer.Ordinal)
            .ToArray();
        actualBars.Should().HaveCount(2);
        targetBars.Should().HaveCount(2);

        // North category (index 000): the value axis maps a total of 60 to the full 80pt plot.
        var actualNorth = actualBars[0];
        var targetNorth = targetBars[0];
        actualNorth.Fill.Should().Be("#2563eb");
        targetNorth.Fill.Should().Be("#14b8a6");

        // Exact area ratio: Actual(40) segment is exactly twice the Target(20) segment.
        actualNorth.Height.Should().BeApproximately(53.33, 0.02);
        targetNorth.Height.Should().BeApproximately(26.67, 0.02);
        (actualNorth.Height / targetNorth.Height).Should().BeApproximately(2.0, 0.001);

        // Cumulative stacking: the first (Actual) segment sits on the baseline, the second
        // (Target) sits directly on top with NO gap — segments are contiguous strokes.
        // (Plot spans y=30..110 inside the 20pt page margins; the 60-total stack fills 80pt.)
        actualNorth.Y.Should().BeApproximately(56.67, 0.02);
        (actualNorth.Y + actualNorth.Height).Should().BeApproximately(110, 0.02); // plot bottom
        targetNorth.Y.Should().BeApproximately(30, 0.02); // plot top: full 80pt stack
        (targetNorth.Y + targetNorth.Height).Should().BeApproximately(actualNorth.Y, 0.02);

        // Both segments of a category share the same X band (stacked, not grouped).
        targetNorth.X.Should().BeApproximately(actualNorth.X, 0.02);
        targetNorth.Width.Should().BeApproximately(actualNorth.Width, 0.02);
    }

    [Fact]
    public void Generate_StackedBarChartStacksSegmentsHorizontallyWithExactAreaRatios()
    {
        var snapshot = GenerateStacked(StackedChart(ReportChartType.StackedBar, width: 240, height: 160));
        var commands = snapshot.Pages.Single().Commands;

        var actualBars = commands
            .Where(command => command.Id.Contains("chart-actual-bar-", StringComparison.Ordinal))
            .OrderBy(command => command.Id, StringComparer.Ordinal)
            .ToArray();
        var targetBars = commands
            .Where(command => command.Id.Contains("chart-target-bar-", StringComparison.Ordinal))
            .OrderBy(command => command.Id, StringComparer.Ordinal)
            .ToArray();
        actualBars.Should().HaveCount(2);
        targetBars.Should().HaveCount(2);

        var actualNorth = actualBars[0];
        var targetNorth = targetBars[0];
        // Exact area ratio along the horizontal axis: Actual(40) is twice Target(20).
        (actualNorth.Width / targetNorth.Width).Should().BeApproximately(2.0, 0.001);
        // Target segment starts exactly where the Actual segment ends (contiguous, no gap).
        targetNorth.X.Should().BeApproximately(actualNorth.X + actualNorth.Width, 0.02);
        // Same row band and height for both segments of the category.
        targetNorth.Y.Should().BeApproximately(actualNorth.Y, 0.02);
        targetNorth.Height.Should().BeApproximately(actualNorth.Height, 0.02);
    }

    [Fact]
    public void Generate_StackedAreaChartCreatesOnePolygonPerSeriesWithCumulativeBaseline()
    {
        var snapshot = GenerateStacked(StackedChart(ReportChartType.StackedArea, width: 240, height: 160));
        var commands = snapshot.Pages.Single().Commands;

        commands.Where(command => command.Id.EndsWith("chart-actual-area", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Type.Should().Be(ReportSnapshotCommandType.Path);
        commands.Where(command => command.Id.EndsWith("chart-target-area", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Type.Should().Be(ReportSnapshotCommandType.Path);
    }

    [Fact]
    public void Generate_NonStackedColumnWithTwoSeriesRemainsGroupedSideBySide()
    {
        // Guard: an existing multi-series Column stays GROUPED (distinct X per series), unchanged.
        var snapshot = GenerateStacked(StackedChart(ReportChartType.Column, width: 240, height: 160));
        var commands = snapshot.Pages.Single().Commands;

        var actualNorth = commands.Single(command => command.Id.EndsWith("chart-actual-bar-000", StringComparison.Ordinal));
        var targetNorth = commands.Single(command => command.Id.EndsWith("chart-target-bar-000", StringComparison.Ordinal));

        // Grouped bars sit side by side (different X) and both grow from the baseline (same bottom).
        actualNorth.X.Should().NotBe(targetNorth.X);
        (actualNorth.Y + actualNorth.Height).Should().BeApproximately(targetNorth.Y + targetNorth.Height, 0.02);
    }

    private static ReportChartElement StackedChart(ReportChartType type, double width, double height)
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
            ColorPalette = ["#2563eb", "#14b8a6", "#f59e0b"],
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Actual",
                    CategoryExpression = "=Fields.Region",
                    ValueExpression = "=Fields.Actual",
                    Color = "#2563eb",
                },
                new ReportChartSeries
                {
                    Name = "Target",
                    CategoryExpression = "=Fields.Region",
                    ValueExpression = "=Fields.Target",
                    Color = "#14b8a6",
                },
            ],
        };

    private static ReportSnapshot GenerateStacked(ReportChartElement chart)
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
                new ReportDataColumn("Actual", DataFieldType.Number),
                new ReportDataColumn("Target", DataFieldType.Number),
            ],
            [
                StackedRow("North", 40m, 20m),
                StackedRow("South", 10m, 20m),
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

    private static ProcessedDataRow StackedRow(string region, decimal actual, decimal target)
        => new(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Region"] = region,
            ["Actual"] = actual,
            ["Target"] = target,
        });

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
