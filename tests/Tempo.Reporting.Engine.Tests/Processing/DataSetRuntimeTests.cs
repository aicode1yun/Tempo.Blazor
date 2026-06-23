using DataFieldType = Tempo.Reporting.Abstractions.Data.ReportDataFieldType;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Engine.Processing;

namespace Tempo.Reporting.Engine.Tests.Processing;

public sealed class DataSetRuntimeTests
{
    [Fact]
    public async Task LoadAsync_PreservesSchemaAndMaterializesTypedRows()
    {
        var result = new ReportDataSetResult(
            [
                new ReportDataColumn("Name", DataFieldType.String),
                new ReportDataColumn("Total", DataFieldType.Number),
                new ReportDataColumn("BookedAt", DataFieldType.Date),
                new ReportDataColumn("Paid", DataFieldType.Boolean),
            ],
            Rows(
                new Dictionary<string, object?>
                {
                    ["Name"] = "Ada",
                    ["Total"] = 125.50m,
                    ["BookedAt"] = new DateTimeOffset(2026, 6, 22, 9, 0, 0, TimeSpan.Zero),
                    ["Paid"] = true,
                },
                new Dictionary<string, object?>
                {
                    ["Name"] = "Borek",
                    ["Total"] = 42m,
                    ["BookedAt"] = new DateTimeOffset(2026, 6, 23, 9, 0, 0, TimeSpan.Zero),
                    ["Paid"] = false,
                }));

        var dataSet = await ReportDataSetRuntime.LoadAsync("Orders", result, CancellationToken.None);

        dataSet.Name.Should().Be("Orders");
        dataSet.Schema.Should().ContainSingle(column => column.Name == "Total" && column.DataType == DataFieldType.Number);
        dataSet.Rows.Should().HaveCount(2);
        dataSet.Rows[0]["Name"].Should().Be("Ada");
        dataSet.Rows[0]["Total"].Should().Be(125.50m);
    }

    [Fact]
    public async Task Cursor_IteratesRowsAndCanResetWithoutRequeryingProvider()
    {
        var dataSet = await ReportDataSetRuntime.LoadAsync(
            "Orders",
            new ReportDataSetResult(
                [new ReportDataColumn("Id", DataFieldType.Number)],
                Rows(
                    new Dictionary<string, object?> { ["Id"] = 1 },
                    new Dictionary<string, object?> { ["Id"] = 2 })),
            CancellationToken.None);

        var cursor = dataSet.CreateCursor();

        cursor.MoveNext().Should().BeTrue();
        cursor.Current["Id"].Should().Be(1);
        cursor.MoveNext().Should().BeTrue();
        cursor.Current["Id"].Should().Be(2);
        cursor.MoveNext().Should().BeFalse();

        cursor.Reset();

        cursor.MoveNext().Should().BeTrue();
        cursor.Current["Id"].Should().Be(1);
    }

    private static async IAsyncEnumerable<ReportDataRow> Rows(params IReadOnlyDictionary<string, object?>[] rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return new ReportDataRow(row);
        }
    }
}
