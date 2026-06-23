using System.Text;
using Tempo.Blazor.Demo.SharedUI.Services;
using Tempo.Blazor.Reporting.Models;

namespace Tempo.Blazor.Tests.Reporting;

public sealed class ReportEmbeddingDemoTests
{
    [Fact]
    public async Task EmbeddedSource_RendersDemoDataAndExportsCsv()
    {
        var factory = new DemoReportEmbeddingSourceFactory();
        var source = factory.CreateEmbeddedSource();
        var parameters = factory.CreateDefaultParameters();
        var request = new ReportViewerRenderRequest
        {
            TenantId = "northwind",
            UserId = "embedded-user",
            CultureName = "en-US",
            Parameters = parameters,
        };

        var metadata = await source.GetMetadataAsync(new ReportViewerMetadataRequest
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            CultureName = request.CultureName,
            Parameters = parameters,
        });
        var render = await source.RenderAsync(request);
        var csv = await source.ExportCsvAsync(request);

        metadata.ReportId.Should().Be(DemoReportEmbeddingSourceFactory.EmbeddedReportId);
        metadata.Parameters.Should().Contain(parameter => parameter.Definition.Name == "Region" && parameter.Options.Count == 2);
        render.Snapshot.Pages.Should().NotBeEmpty();
        csv.FileName.Should().Be($"{DemoReportEmbeddingSourceFactory.EmbeddedReportId}.csv");
        Encoding.UTF8.GetString(csv.Bytes).Should().Contain("Europe Channel");
    }

    [Fact]
    public void RemoteSource_AddsDemoApiKeyHeader()
    {
        var factory = new DemoReportEmbeddingSourceFactory();
        using var client = new HttpClient { BaseAddress = new Uri("https://reports.example.test/") };

        var source = factory.CreateRemoteSource(client, DemoReportEmbeddingSourceFactory.DemoApiKey);

        source.Should().NotBeNull();
        client.DefaultRequestHeaders.GetValues(DemoReportEmbeddingSourceFactory.ApiKeyHeaderName)
            .Should()
            .ContainSingle(DemoReportEmbeddingSourceFactory.DemoApiKey);
    }
}
