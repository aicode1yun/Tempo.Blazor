using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Engine.Expressions;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Tests.Processing;

public sealed class GroupingAndAggregateTests
{
    [Fact]
    public async Task Group_GroupsRowsByMultipleLevelsAndSortsGroupsByAggregate()
    {
        var dataSet = await SalesAsync(
            ("North", "Hardware", "A", 20m),
            ("North", "Software", "B", 80m),
            ("South", "Hardware", "C", 40m),
            ("South", "Software", "D", 30m));
        var context = Context(dataSet);

        var groups = ReportGroupingEngine.Group(
            dataSet,
            [
                new ReportGroupLevel(
                    "Region",
                    "=Fields.Region",
                    [new ReportSortDefinition("=Sum(Fields.Total)", ReportSortDirection.Descending)]),
                new ReportGroupLevel("Category", "=Fields.Category"),
            ],
            context);

        groups.Select(group => group.Key).Should().Equal("North", "South");
        groups[0].Rows.Should().HaveCount(2);
        groups[0].Children.Select(group => group.Key).Should().Equal("Hardware", "Software");
        groups[0].Children[1].Rows.Single()["Customer"].Should().Be("B");
    }

    [Fact]
    public async Task Evaluate_CalculatesSupportedAggregatesAndDefersPageScopedAggregates()
    {
        var dataSet = await SalesAsync(
            ("North", "Hardware", "A", 20m),
            ("North", "Hardware", "B", 20m),
            ("North", "Software", "C", 50m));
        var context = Context(dataSet);

        ReportAggregateEngine.Evaluate("=Sum(Fields.Total)", dataSet.Rows, context).AsNumber().Should().Be(90m);
        ReportAggregateEngine.Evaluate("=Count(Fields.Customer)", dataSet.Rows, context).AsNumber().Should().Be(3m);
        ReportAggregateEngine.Evaluate("=CountDistinct(Fields.Total)", dataSet.Rows, context).AsNumber().Should().Be(2m);
        ReportAggregateEngine.Evaluate("=Min(Fields.Total)", dataSet.Rows, context).AsNumber().Should().Be(20m);
        ReportAggregateEngine.Evaluate("=Max(Fields.Total)", dataSet.Rows, context).AsNumber().Should().Be(50m);
        ReportAggregateEngine.Evaluate("=Avg(Fields.Total)", dataSet.Rows, context).AsNumber().Should().Be(30m);
        ReportAggregateEngine.Evaluate("=First(Fields.Customer)", dataSet.Rows, context).AsString().Should().Be("A");
        ReportAggregateEngine.Evaluate("=Last(Fields.Customer)", dataSet.Rows, context).AsString().Should().Be("C");
        ReportAggregateEngine.Evaluate("=Sum(Fields.Total, \"report\")", dataSet.Rows.Take(2).ToArray(), context, dataSet.Rows)
            .AsNumber().Should().Be(90m);

        var pageAggregate = ReportAggregateEngine.Evaluate("=Sum(Fields.Total, \"page\")", dataSet.Rows, context);

        pageAggregate.Kind.Should().Be(ExpressionValueKind.Deferred);
        pageAggregate.DeferredKind.Should().Be(ExpressionDeferredKind.PageAggregate);
    }

    [Fact]
    public async Task EvaluateRunningTotal_ReturnsCumulativeAggregatePerRow()
    {
        var dataSet = await SalesAsync(
            ("North", "Hardware", "A", 20m),
            ("North", "Hardware", "B", 30m),
            ("North", "Software", "C", 50m));

        var totals = ReportAggregateEngine.EvaluateRunningTotal(
            "=Sum(Fields.Total)",
            dataSet.Rows,
            Context(dataSet));

        totals.Select(total => total.AsNumber()).Should().Equal(20m, 50m, 100m);
    }

    private static ReportProcessingContext Context(ProcessedDataSet dataSet)
        => new(
            new ReportExecutionContext("tenant", "user", "en-US"),
            new Dictionary<string, ReportParameterValue>(),
            new Dictionary<string, ProcessedDataSet> { [dataSet.Name] = dataSet });

    private static Task<ProcessedDataSet> SalesAsync(params (string Region, string Category, string Customer, decimal Total)[] rows)
        => ReportDataSetRuntime.LoadAsync(
            "Sales",
            new ReportDataSetResult(
                [
                    new ReportDataColumn("Region", DataFieldType.String),
                    new ReportDataColumn("Category", DataFieldType.String),
                    new ReportDataColumn("Customer", DataFieldType.String),
                    new ReportDataColumn("Total", DataFieldType.Number),
                ],
                Rows(rows)),
            CancellationToken.None);

    private static async IAsyncEnumerable<ReportDataRow> Rows(IEnumerable<(string Region, string Category, string Customer, decimal Total)> rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return new ReportDataRow(new Dictionary<string, object?>
            {
                ["Region"] = row.Region,
                ["Category"] = row.Category,
                ["Customer"] = row.Customer,
                ["Total"] = row.Total,
            });
        }
    }
}
