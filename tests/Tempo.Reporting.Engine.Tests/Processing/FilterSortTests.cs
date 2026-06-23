using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Tests.Processing;

public sealed class FilterSortTests
{
    [Fact]
    public async Task FilterAndSort_EvaluatesExpressionsWithParametersAndNullOrdering()
    {
        var dataSet = await OrdersAsync(
            ("chata", 10m, "B"),
            ("hrnek", 50m, "A"),
            (null, 20m, "A"),
            ("auto", 70m, "B"));
        var context = Context(new Dictionary<string, ReportParameterValue>
        {
            ["MinTotal"] = ReportParameterValue.Scalar(20m),
        });

        var processed = ReportDataSetProcessor.FilterAndSort(
            dataSet,
            "=Fields.Total >= Parameters.MinTotal",
            [new ReportSortDefinition("=Fields.Name", ReportSortDirection.Ascending, ReportNullSortOrder.Last)],
            context);

        processed.Rows.Select(row => row["Name"]).Should().Equal("auto", "hrnek", null);
    }

    [Fact]
    public async Task FilterAndSort_KeepsExplicitNullOrderingIndependentOfDescendingDirection()
    {
        var dataSet = await OrdersAsync(
            ("alpha", 10m, "A"),
            (null, 20m, "A"),
            ("zulu", 30m, "A"));

        var processed = ReportDataSetProcessor.FilterAndSort(
            dataSet,
            filterExpression: null,
            [new ReportSortDefinition("=Fields.Name", ReportSortDirection.Descending, ReportNullSortOrder.Last)],
            Context());

        processed.Rows.Select(row => row["Name"]).Should().Equal("zulu", "alpha", null);
    }

    [Fact]
    public async Task FilterAndSort_UsesExecutionCultureForStringCollation()
    {
        var dataSet = await OrdersAsync(
            ("chata", 1m, "A"),
            ("hrnek", 1m, "A"),
            ("idea", 1m, "A"));

        var processed = ReportDataSetProcessor.FilterAndSort(
            dataSet,
            filterExpression: null,
            [new ReportSortDefinition("=Fields.Name")],
            Context(cultureName: "cs-CZ"));

        processed.Rows.Select(row => row["Name"]).Should().Equal("hrnek", "chata", "idea");
    }

    private static ReportProcessingContext Context(
        IReadOnlyDictionary<string, ReportParameterValue>? parameters = null,
        string cultureName = "en-US")
        => new(
            new ReportExecutionContext("tenant", "user", cultureName),
            parameters ?? new Dictionary<string, ReportParameterValue>());

    private static Task<ProcessedDataSet> OrdersAsync(params (string? Name, decimal Total, string Category)[] rows)
        => ReportDataSetRuntime.LoadAsync(
            "Orders",
            new ReportDataSetResult(
                [
                    new ReportDataColumn("Name", DataFieldType.String),
                    new ReportDataColumn("Total", DataFieldType.Number),
                    new ReportDataColumn("Category", DataFieldType.String),
                ],
                Rows(rows)),
            CancellationToken.None);

    private static async IAsyncEnumerable<ReportDataRow> Rows(IEnumerable<(string? Name, decimal Total, string Category)> rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return new ReportDataRow(new Dictionary<string, object?>
            {
                ["Name"] = row.Name,
                ["Total"] = row.Total,
                ["Category"] = row.Category,
            });
        }
    }
}
