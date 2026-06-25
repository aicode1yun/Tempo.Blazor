using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Layout;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;

namespace Tempo.Reporting.Engine.Tests.Layout;

public sealed class TableLayoutTests
{
    [Fact]
    public void Layout_ResolvesFixedAndProportionalColumnsAndCollapsedBorders()
    {
        var table = new ReportTableElement
        {
            Width = 300,
            Columns =
            [
                new ReportTableColumn("Name", 120),
                new ReportTableColumn("Share", 1) { WidthMode = ReportTableColumnWidthMode.Proportional },
                new ReportTableColumn("Total", 2) { WidthMode = ReportTableColumnWidthMode.Proportional },
            ],
            Header = Row("Name", "Share", "Total"),
            Detail = Row("=Fields.Name", "=Fields.Share", "=Fields.Total"),
        };

        var layout = Layout(table, Rows(("A", 1m, 10m)));
        var commands = ReportTableLayouter.ToSnapshotCommands(layout.Pages.Single(), 0, 0, "table", new FixedTextMeasurer()).ToArray();

        layout.Columns.Select(column => column.Width).Should().Equal(120, 60, 120);
        layout.Pages.Single().Rows.Should().HaveCount(2);
        commands.Count(command => command.Type == ReportSnapshotCommandType.Line && command.Id.Contains("-grid-", StringComparison.Ordinal))
            .Should().BeGreaterThan(4);
        commands.Should().NotContain(command => command.Id.Contains("-cell-border-", StringComparison.Ordinal));
    }

    [Fact]
    public void Layout_CreatesDetailRowsFromDatasetAndGrowsRowHeightForCanGrowCells()
    {
        var table = new ReportTableElement
        {
            Width = 110,
            Columns = [new ReportTableColumn("Description", 110)],
            Detail = new ReportTableRow
            {
                Height = 14,
                Cells =
                [
                    new ReportTableCell
                    {
                        Expression = "=Fields.Name",
                        CanGrow = true,
                        TextStyle = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 },
                    },
                ],
            },
        };

        var layout = Layout(table, Rows(("Very long detail value that wraps", 1m, 10m)));

        layout.Pages.Single().Rows.Should().ContainSingle();
        layout.Pages.Single().Rows[0].Height.Should().BeGreaterThan(14);
        layout.Pages.Single().Rows[0].Cells[0].Text.Should().Be("Very long detail value that wraps");
    }

    [Fact]
    public void Layout_AddsGroupHeadersAndFootersWithAggregateValues()
    {
        var table = new ReportTableElement
        {
            Width = 180,
            Columns = [new ReportTableColumn("Name", 90), new ReportTableColumn("Total", 90)],
            Groups =
            [
                new ReportTableGroupDefinition
                {
                    Name = "Region",
                    Expression = "=Fields.Region",
                    Header = Row("Region", "=Fields.Region"),
                    Footer = Row("Region total", "=Sum(Fields.Total)"),
                },
            ],
            Detail = Row("=Fields.Name", "=Fields.Total"),
        };

        var layout = Layout(table, Rows(
            ("Ada", 1m, 10m, "West"),
            ("Borek", 1m, 15m, "West"),
            ("Cyril", 1m, 7m, "East")));

        var rows = layout.Pages.SelectMany(page => page.Rows).ToArray();

        rows.Count(row => row.Kind == ReportTableLayoutRowKind.GroupHeader).Should().Be(2);
        rows.Count(row => row.Kind == ReportTableLayoutRowKind.GroupFooter).Should().Be(2);
        rows.Should().Contain(row => row.Kind == ReportTableLayoutRowKind.GroupFooter && row.Cells[1].Text == "25");
        rows.Should().Contain(row => row.Kind == ReportTableLayoutRowKind.GroupFooter && row.Cells[1].Text == "7");
    }

    [Fact]
    public void Layout_PaginatesBetweenRowsRepeatsHeaderAndKeepsGroupHeaderWithFirstDetail()
    {
        var table = new ReportTableElement
        {
            Width = 160,
            RepeatHeaderOnNewPage = true,
            Columns = [new ReportTableColumn("Name", 100), new ReportTableColumn("Total", 60)],
            Header = Row("Name", "Total"),
            Groups =
            [
                new ReportTableGroupDefinition
                {
                    Name = "Region",
                    Expression = "=Fields.Region",
                    Header = Row("Region", "=Fields.Region"),
                },
            ],
            Detail = Row("=Fields.Name", "=Fields.Total"),
        };

        var layout = Layout(
            table,
            Rows(
                ("Ada", 1m, 10m, "West"),
                ("Borek", 1m, 15m, "West"),
                ("Cyril", 1m, 7m, "East"),
                ("Dana", 1m, 9m, "East")),
            firstPageHeight: 42,
            pageHeight: 62);

        layout.Pages.Should().HaveCountGreaterThan(1);
        layout.Pages.Skip(1).Should().OnlyContain(page => page.Rows.First().Kind == ReportTableLayoutRowKind.Header && page.Rows.First().Repeated);
        layout.Pages.Should().NotContain(page => page.Rows.Last().Kind == ReportTableLayoutRowKind.GroupHeader);
    }

    [Fact]
    public void Layout_AppliesRowVisibilityAndExpressionDrivenBackgrounds()
    {
        var table = new ReportTableElement
        {
            Width = 160,
            ZebraStripeColor = "#f8fafc",
            Columns = [new ReportTableColumn("Name", 100), new ReportTableColumn("Total", 60)],
            Detail = new ReportTableRow
            {
                Height = 20,
                VisibleExpression = "=Fields.Total >= 10",
                BackgroundExpression = "=Fields.Region = \"West\"",
                Cells =
                [
                    new ReportTableCell { Expression = "=Fields.Name" },
                    new ReportTableCell { Expression = "=Fields.Total", HorizontalAlignment = ReportHorizontalAlignment.Right },
                ],
            },
        };

        var layout = Layout(table, Rows(
            ("Ada", 1m, 10m, "West"),
            ("Borek", 1m, 8m, "East"),
            ("Cyril", 1m, 12m, "East")));

        var rows = layout.Pages.Single().Rows;

        rows.Should().HaveCount(2);
        rows[0].Cells[0].Text.Should().Be("Ada");
        rows[0].BackgroundColor.Should().Be("#f8fafc");
        rows[1].Cells[0].Text.Should().Be("Cyril");
        rows[1].BackgroundColor.Should().Be("#f8fafc");
    }

    private static ReportTableLayout Layout(
        ReportTableElement table,
        ProcessedDataSet dataSet,
        double firstPageHeight = 1_000,
        double pageHeight = 1_000)
    {
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            dataSets: new Dictionary<string, ProcessedDataSet>(StringComparer.Ordinal) { [dataSet.Name] = dataSet });
        return ReportTableLayouter.Layout(
            new ReportTableLayoutRequest
            {
                Table = table,
                DataSet = dataSet,
                Context = context,
                FirstPageHeight = firstPageHeight,
                PageHeight = pageHeight,
                Width = table.Width,
            },
            new FixedTextMeasurer());
    }

    private static ReportTableRow Row(params string[] values)
        => new()
        {
            Height = 20,
            Cells = values.Select(value => new ReportTableCell
            {
                Text = value.StartsWith('=') ? null : value,
                Expression = value.StartsWith('=') ? value : null,
                TextStyle = new ReportTextStyle { FontFamily = "Fixed", FontSize = 10 },
                Padding = new ReportThickness(2),
            }).ToList(),
        };

    private static ProcessedDataSet Rows(params (string Name, decimal Share, decimal Total, string Region)[] rows)
        => new(
            "Rows",
            [
                new ReportDataColumn("Name", DataFieldType.String),
                new ReportDataColumn("Share", DataFieldType.Number),
                new ReportDataColumn("Total", DataFieldType.Number),
                new ReportDataColumn("Region", DataFieldType.String),
            ],
            rows.Select(row => new ProcessedDataRow(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Name"] = row.Name,
                ["Share"] = row.Share,
                ["Total"] = row.Total,
                ["Region"] = row.Region,
            })).ToArray());

    private static ProcessedDataSet Rows(params (string Name, decimal Share, decimal Total)[] rows)
        => Rows(rows.Select(row => (row.Name, row.Share, row.Total, "West")).ToArray());
}
