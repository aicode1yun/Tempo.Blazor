using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Reporting.Interop;
using Tempo.Blazor.Reporting.Models;
using Tempo.ReportServer.Api.Security;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportServerApiEndpointTests
{
    [Fact]
    public async Task RemoteEndpoints_RequireCredentialsAndAcceptDemoApiKey()
    {
        await using var app = await CreateAppAsync();
        var anonymousClient = app.GetTestClient();
        var authorizedClient = app.GetTestClient();
        authorizedClient.DefaultRequestHeaders.Add(ReportSecurityHeaders.ApiKey, ReportServerEmbeddingDemo.ApiKey);
        var request = new ReportViewerRenderRequest
        {
            CultureName = "en-US",
        };

        var anonymous = await anonymousClient.PostAsJsonAsync("/api/reports/sales-dashboard/render", request);
        var authorized = await authorizedClient.PostAsJsonAsync("/api/reports/sales-dashboard/render", request);

        anonymous.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        authorized.EnsureSuccessStatusCode();
        var result = await authorized.Content.ReadFromJsonAsync<ReportViewerRenderResult>(ReportViewerJson.Options);
        result!.Snapshot.Pages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportEndpoints_ReturnCsvAndXlsxFiles()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        var request = new ReportViewerRenderRequest
        {
            CultureName = "cs-CZ",
        };

        client.DefaultRequestHeaders.Add(ReportSecurityHeaders.ApiKey, ReportServerEmbeddingDemo.ApiKey);

        var csvResponse = await client.PostAsJsonAsync("/api/reports/sales-dashboard/export/csv", request);
        var xlsxResponse = await client.PostAsJsonAsync("/api/reports/sales-dashboard/export/xlsx", request);

        csvResponse.EnsureSuccessStatusCode();
        xlsxResponse.EnsureSuccessStatusCode();
        csvResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        xlsxResponse.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var csvBytes = await csvResponse.Content.ReadAsByteArrayAsync();
        csvBytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var csv = Encoding.UTF8.GetString(csvBytes[3..]);
        csv.Should().Contain("Customer;Region;Total;Status");
        csv.Should().Contain("Europe Customer 01;EU;937;Open");
        var xlsxBytes = await xlsxResponse.Content.ReadAsByteArrayAsync();
        xlsxBytes.Take(4).Should().Equal(0x50, 0x4B, 0x03, 0x04);
        xlsxBytes.Length.Should().BeGreaterThan(1_000);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IReportApiKeyStore, DemoReportApiKeyStore>();
        builder.Services.AddReportServerSecurity();
        builder.Services.AddSingleton<DemoReportSourceFactory>();
        var app = builder.Build();
        app.MapReportServerDemoApi();
        await app.StartAsync().ConfigureAwait(false);
        return app;
    }
}
