using System.Net;
using System.Text;
using Tempo.ReportServer.Api.DataSources;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.ReportServer.Api.Tests.DataSources;

public sealed class RestJsonReportDataProviderTests
{
    [Fact]
    public async Task GetDataAsync_ExpandsUrlTemplateWithEncodingSelectsJsonPathArrayAndAddsConfiguredAuthHeader()
    {
        var handler = new CapturingHttpHandler(
            """{"items":[{"id":1,"name":"Ada"},{"id":2,"name":"Bob"}]}""");
        var registry = new InMemoryNamedDataSourceRegistry();
        registry.Register(NamedReportDataSource.RestJson(
            tenantId: "tenant-a",
            name: "orders-rest",
            baseUri: new Uri("https://api.example.test/"),
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "secret-a" }));
        var provider = new RestJsonReportDataProvider(registry, new HttpClient(handler));

        var result = await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery
            {
                SourceName = "orders-rest",
                Text = "orders?region={Region}",
                Selector = "$.items",
                MaxRows = 1,
            },
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU & CZ"),
            },
            new ReportExecutionContext("tenant-a", "user-1", "en-US"));
        var rows = await ReadRowsAsync(result.Rows);

        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.example.test/orders?region=EU%20%26%20CZ");
        handler.LastRequest.Headers.GetValues("X-Api-Key").Should().Equal("secret-a");
        rows.Should().ContainSingle();
        rows[0].Values["name"].Should().Be("Ada");
        result.Schema.Select(c => c.Name).Should().Equal("id", "name");
    }

    [Fact]
    public async Task GetDataAsync_SelectsJsonPointerArray()
    {
        var handler = new CapturingHttpHandler("""{"data":{"rows":[{"id":7}]}}""");
        var registry = new InMemoryNamedDataSourceRegistry();
        registry.Register(NamedReportDataSource.RestJson("tenant-a", "orders-rest", new Uri("https://api.example.test/")));
        var provider = new RestJsonReportDataProvider(registry, new HttpClient(handler));

        var result = await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery { SourceName = "orders-rest", Text = "orders", Selector = "/data/rows" },
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal),
            new ReportExecutionContext("tenant-a", "user-1", "en-US"));
        var rows = await ReadRowsAsync(result.Rows);

        rows.Should().ContainSingle();
        rows[0].Values["id"].Should().Be(7L);
    }

    [Fact]
    public async Task GetDataAsync_RejectsUnknownTemplateParameterInsteadOfReadingSecrets()
    {
        var registry = new InMemoryNamedDataSourceRegistry();
        registry.Register(NamedReportDataSource.RestJson(
            "tenant-a",
            "orders-rest",
            new Uri("https://api.example.test/"),
            new Dictionary<string, string> { ["Authorization"] = "Bearer secret" }));
        var provider = new RestJsonReportDataProvider(registry, new HttpClient(new CapturingHttpHandler("{}")));

        var act = async () => await provider.GetDataAsync(
            "Orders",
            new ReportDataQuery { SourceName = "orders-rest", Text = "orders?token={Authorization}" },
            new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal),
            new ReportExecutionContext("tenant-a", "user-1", "en-US"));

        await act.Should().ThrowAsync<ReportDataProviderException>()
            .Where(ex => ex.Code == "RestJson.UnknownTemplateParameter");
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

    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        private readonly string _json;

        public CapturingHttpHandler(string json)
        {
            _json = json;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
