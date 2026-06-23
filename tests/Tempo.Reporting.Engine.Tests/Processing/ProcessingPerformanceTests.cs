using System.Diagnostics;
using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Engine.Processing;
using Xunit.Abstractions;

namespace Tempo.Reporting.Engine.Tests.Processing;

public sealed class ProcessingPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ProcessingPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Group_OneHundredThousandRowsThreeLevels_CompletesUnderUpperBound()
    {
        const int rowCount = 100_000;
        var dataSet = await ReportDataSetRuntime.LoadAsync(
            "Perf",
            new ReportDataSetResult(
                [
                    new ReportDataColumn("Region", DataFieldType.String),
                    new ReportDataColumn("Category", DataFieldType.String),
                    new ReportDataColumn("Bucket", DataFieldType.String),
                    new ReportDataColumn("Total", DataFieldType.Number),
                ],
                Rows(rowCount)),
            CancellationToken.None);
        var context = new ReportProcessingContext(
            new ReportExecutionContext("tenant", "user", "en-US"),
            new Dictionary<string, ReportParameterValue>(),
            new Dictionary<string, ProcessedDataSet> { [dataSet.Name] = dataSet });
        var stopwatch = Stopwatch.StartNew();

        var groups = ReportGroupingEngine.Group(
            dataSet,
            [
                new ReportGroupLevel("Region", "=Fields.Region"),
                new ReportGroupLevel("Category", "=Fields.Category"),
                new ReportGroupLevel("Bucket", "=Fields.Bucket"),
            ],
            context);

        stopwatch.Stop();
        _output.WriteLine($"F4 processing perf smoke: {rowCount:n0} rows / 3 levels in {stopwatch.Elapsed.TotalMilliseconds:n0} ms");
        groups.Should().HaveCount(10);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    private static async IAsyncEnumerable<ReportDataRow> Rows(int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (i % 1_000 == 0)
            {
                await Task.Yield();
            }

            yield return new ReportDataRow(new Dictionary<string, object?>
            {
                ["Region"] = $"R{i % 10}",
                ["Category"] = $"C{i % 25}",
                ["Bucket"] = $"B{i % 100}",
                ["Total"] = i % 17,
            });
        }
    }
}
