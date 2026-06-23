using Microsoft.Data.Sqlite;
using Tempo.ReportServer.Api.DataSources;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.ReportServer.Api.Tests.DataSources;

public sealed class SqlReportDataProviderTests
{
    [Fact]
    public async Task GetDataAsync_UsesDbParametersAndHonorsRowLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await SeedAsync(connection);
        var registry = new InMemoryNamedDataSourceRegistry();
        registry.Register(NamedReportDataSource.Sql(
            tenantId: "tenant-a",
            name: "orders-sql",
            connectionFactory: () => connection));
        var provider = new SqlReportDataProvider(registry);

        var result = await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery
            {
                SourceName = "orders-sql",
                Text = "select id, region, total from orders where region = @Region order by id",
                MaxRows = 1,
            },
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
            },
            new ReportExecutionContext("tenant-a", "user-1", "en-US"));
        var rows = await ReadRowsAsync(result.Rows);

        rows.Should().ContainSingle();
        rows[0].Values["id"].Should().Be(1L);
        rows[0].Values["total"].Should().Be(12.5);
        result.Schema.Select(c => c.Name).Should().Equal("id", "region", "total");
    }

    [Fact]
    public async Task GetDataAsync_RejectsBraceInterpolationInSql()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var registry = new InMemoryNamedDataSourceRegistry();
        registry.Register(NamedReportDataSource.Sql("tenant-a", "orders-sql", () => connection));
        var provider = new SqlReportDataProvider(registry);

        var act = async () => await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery
            {
                SourceName = "orders-sql",
                Text = "select * from orders where region = '{Region}'",
            },
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
            },
            new ReportExecutionContext("tenant-a", "user-1", "en-US"));

        await act.Should().ThrowAsync<ReportDataProviderException>()
            .Where(ex => ex.Code == "Sql.QueryInterpolationRejected");
    }

    private static async Task SeedAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table orders (id integer primary key, region text not null, total real not null);
            insert into orders (region, total) values ('EU', 12.5);
            insert into orders (region, total) values ('EU', 30.0);
            insert into orders (region, total) values ('NA', 18.0);
            """;
        await command.ExecuteNonQueryAsync();
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
