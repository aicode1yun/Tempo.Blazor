using Microsoft.Data.Sqlite;
using Tempo.ReportServer.Api.DataSources;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Tests.DataSources;

public sealed class NamedDataSourceRegistryTests
{
    [Fact]
    public void Resolve_IsTenantScopedAndDoesNotLeakSecretsAcrossTenants()
    {
        var registry = new InMemoryNamedDataSourceRegistry();
        registry.Register(NamedReportDataSource.RestJson(
            "tenant-a",
            "orders",
            new Uri("https://tenant-a.example.test/"),
            new Dictionary<string, string> { ["Authorization"] = "Bearer tenant-a-secret" }));
        registry.Register(NamedReportDataSource.RestJson(
            "tenant-b",
            "orders",
            new Uri("https://tenant-b.example.test/"),
            new Dictionary<string, string> { ["Authorization"] = "Bearer tenant-b-secret" }));

        var sourceA = registry.Resolve(
            "orders",
            new ReportExecutionContext("tenant-a", "user-1", "en-US"));

        sourceA!.Rest!.BaseUri.Should().Be(new Uri("https://tenant-a.example.test/"));
        sourceA.Rest.Headers["Authorization"].Should().Be("Bearer tenant-a-secret");
        registry.Resolve("missing", new ReportExecutionContext("tenant-a", "user-1", "en-US"))
            .Should().BeNull();
    }

    [Fact]
    public void SqlDataSource_StoresConnectionFactoryOutsideReportDefinitions()
    {
        var source = NamedReportDataSource.Sql(
            "tenant-a",
            "orders-sql",
            () => new SqliteConnection("Data Source=:memory:"));

        source.SqlOptions!.ConnectionFactory().Should().BeOfType<SqliteConnection>();
        source.Rest.Should().BeNull();
    }
}
