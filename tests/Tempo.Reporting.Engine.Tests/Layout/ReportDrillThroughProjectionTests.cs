using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Processing;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class ReportDrillThroughProjectionTests
{
    [Fact]
    public void GenerateInteractive_TableCellDrillThrough_ProjectsRegionAtCellRectWithRowContext()
    {
        var drillThrough = new ReportDrillThroughAction
        {
            TargetReportPath = "Finance/Customer Detail",
            ParameterMappings =
            [
                new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Field, "Region"),
            ],
        };
        var dataSet = new ProcessedDataSet(
            "Items",
            [
                new ReportDataColumn("Name", DataFieldType.String),
                new ReportDataColumn("Region", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
            ],
            [
                Row(("Name", "Ada"), ("Region", "EU"), ("Total", 10m)),
                Row(("Name", "Grace"), ("Region", "US"), ("Total", 20m)),
            ]);
        var table = new ReportTableElement
        {
            Id = "items",
            DataSetName = "Items",
            X = 0,
            Y = 0,
            Width = 200,
            Height = 120,
            Columns = [new ReportTableColumn("Name", 120), new ReportTableColumn("Region", 80)],
            Detail = new ReportTableRow
            {
                Height = 20,
                Cells =
                [
                    new ReportTableCell { Expression = "=Fields.Name", DrillThrough = drillThrough, TextStyle = FixedStyle() },
                    new ReportTableCell { Expression = "=Fields.Region", TextStyle = FixedStyle() },
                ],
            },
        };
        var instance = TableInstance(table, dataSet, new ReportPageSize(320, 260));

        var result = ReportSnapshotGenerator.GenerateInteractive(instance, new FixedTextMeasurer());

        result.DrillThroughRegions.Should().HaveCount(2);
        var first = result.DrillThroughRegions[0];
        first.Action.Should().BeSameAs(drillThrough);
        first.PageNumber.Should().Be(1);
        first.Width.Should().BeGreaterThan(0);
        first.Height.Should().BeGreaterThan(0);
        // Anchored to the first (Name) cell rectangle within the page body.
        first.X.Should().BeGreaterThanOrEqualTo(10);
        first.Y.Should().BeGreaterThanOrEqualTo(10);
        // Bound row field values are carried so a Field-source mapping resolves against the real row.
        first.Context["Name"].Should().Be("Ada");
        first.Context["Region"].Should().Be("EU");
        result.DrillThroughRegions[1].Context["Region"].Should().Be("US");
    }

    [Fact]
    public void GenerateInteractive_ChartSeriesDrillThrough_ProjectsRegionPerCategoryWithCategoryContext()
    {
        var drillThrough = new ReportDrillThroughAction
        {
            TargetReportPath = "Finance/Status Detail",
            ParameterMappings =
            [
                new ReportDrillThroughParameterMapping("Status", ReportDrillThroughSourceKind.Field, "Status"),
            ],
        };
        var dataSet = new ProcessedDataSet(
            "Sales",
            [
                new ReportDataColumn("Status", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
            ],
            [
                Row(("Status", "Open"), ("Total", 30m)),
                Row(("Status", "Closed"), ("Total", 10m)),
            ]);
        var chart = new ReportChartElement
        {
            Id = "by-status",
            ChartType = ReportChartType.Column,
            DataSetName = "Sales",
            X = 0,
            Y = 0,
            Width = 260,
            Height = 160,
            Series =
            [
                new ReportChartSeries
                {
                    Name = "Revenue",
                    CategoryExpression = "=Fields.Status",
                    ValueExpression = "=Fields.Total",
                    DrillThrough = drillThrough,
                },
            ],
        };
        var instance = ChartInstance(chart, dataSet, new ReportPageSize(320, 260));

        var result = ReportSnapshotGenerator.GenerateInteractive(instance, new FixedTextMeasurer());

        result.DrillThroughRegions.Should().HaveCount(2);
        result.DrillThroughRegions.Should().OnlyContain(region => region.Action == drillThrough && region.Width > 0);
        result.DrillThroughRegions.Select(region => region.Context["Status"]).Should().BeEquivalentTo(["Open", "Closed"]);
    }

    [Fact]
    public void GenerateInteractive_WithoutDrillThrough_ProjectsNoRegions()
    {
        var dataSet = new ProcessedDataSet(
            "Items",
            [new ReportDataColumn("Name", DataFieldType.String)],
            [Row(("Name", "Ada"))]);
        var table = new ReportTableElement
        {
            Id = "items",
            DataSetName = "Items",
            X = 0,
            Y = 0,
            Width = 200,
            Height = 60,
            Columns = [new ReportTableColumn("Name", 200)],
            Detail = new ReportTableRow { Height = 20, Cells = [new ReportTableCell { Expression = "=Fields.Name", TextStyle = FixedStyle() }] },
        };
        var instance = TableInstance(table, dataSet, new ReportPageSize(300, 200));

        var result = ReportSnapshotGenerator.GenerateInteractive(instance, new FixedTextMeasurer());

        result.DrillThroughRegions.Should().BeEmpty();
    }

    private static ReportInstance TableInstance(ReportTableElement table, ProcessedDataSet dataSet, ReportPageSize pageSize)
    {
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup { PageSize = pageSize, Margins = new ReportThickness(10) },
        };
        var band = new ReportBand { Kind = ReportBandKind.Detail, Height = table.Height, Elements = [table] };
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal) { [dataSet.Name] = dataSet });
        return new ReportInstance(
            definition,
            [new ReportBandInstance(ReportBandKind.Detail, null, null, [new ReportElementInstance(table, null, null)], sourceBand: band)],
            context.DataSets,
            context);
    }

    private static ReportInstance ChartInstance(ReportChartElement chart, ProcessedDataSet dataSet, ReportPageSize pageSize)
    {
        var definition = new ReportDefinition
        {
            PageSetup = new ReportPageSetup { PageSize = pageSize, Margins = new ReportThickness(10) },
        };
        var band = new ReportBand { Kind = ReportBandKind.Detail, Height = chart.Height, Elements = [chart] };
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal) { [dataSet.Name] = dataSet });
        return new ReportInstance(
            definition,
            [new ReportBandInstance(ReportBandKind.Detail, null, null, [new ReportElementInstance(chart, null, null)], sourceBand: band)],
            context.DataSets,
            context);
    }

    private static ProcessedDataRow Row(params (string Name, object? Value)[] values)
        => new(values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    private static ReportTextStyle FixedStyle() => new() { FontFamily = "Fixed", FontSize = 10 };
}
