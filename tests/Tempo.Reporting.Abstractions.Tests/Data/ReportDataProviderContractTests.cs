using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Reporting.Abstractions.Tests.Data;

public sealed class ReportDataProviderContractTests
{
    [Fact]
    public async Task StaticDataProvider_StreamsSchemaRowsAndHonorsMaxRows()
    {
        var provider = StaticDataProvider.FromRows(
            "Orders",
            [
                new Dictionary<string, object?> { ["Id"] = 1, ["Region"] = "EU", ["Total"] = 12.5m },
                new Dictionary<string, object?> { ["Id"] = 2, ["Region"] = "NA", ["Total"] = 18m },
            ]);

        var result = await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery { MaxRows = 1 },
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal),
            new ReportExecutionContext("tenant-a", "user-1", "cs-CZ"));
        var rows = await ReadRowsAsync(result.Rows);

        result.Schema.Select(c => c.Name).Should().Equal("Id", "Region", "Total");
        result.Schema.Single(c => c.Name == "Total").DataType.Should().Be(ReportDataFieldType.Number);
        rows.Should().ContainSingle();
        rows[0].Values["Region"].Should().Be("EU");
    }

    [Fact]
    public async Task StaticDataProvider_UsesExecutionContextCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = StaticDataProvider.FromRows(
            "Orders",
            [new Dictionary<string, object?> { ["Id"] = 1 }]);

        var act = async () => await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery(),
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal),
            new ReportExecutionContext("tenant-a", "user-1", "en-US", CancellationToken: cts.Token));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ReportParameterValue_PreservesScalarAndMultiValueInputs()
    {
        ReportParameterValue.Scalar("EU").Values.Should().Equal("EU");
        ReportParameterValue.Multiple(["EU", "NA"]).Values.Should().Equal("EU", "NA");
    }

    private static async Task<List<ReportDataRow>> ReadRowsAsync(IAsyncEnumerable<ReportDataRow> rows)
    {
        var result = new List<ReportDataRow>();
        await foreach (var row in rows)
        {
            result.Add(row);
        }

        return result;
    }
}
