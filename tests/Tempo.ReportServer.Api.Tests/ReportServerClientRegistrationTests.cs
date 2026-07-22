using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Verifies that <c>AddTempoReportServerClient</c> registers a config-driven typed
/// <see cref="ITempoReportServerClient"/> that talks to a real report server API host. This is the
/// client half of the Web dogfooding replacement (typed client, base URL from configuration).
/// </summary>
public sealed class ReportServerClientRegistrationTests
{
    [Fact]
    public async Task RegisteredTypedClient_RoundTripsAgainstApiHost()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connection));
        // Open dev/test host without an authentication gate: allow anonymous operations so the
        // in-handler ACL enforcement does not fail closed (401) for principal-less requests.
        builder.Services.Configure<Tempo.ReportServer.Api.ReportServerApiOptions>(o => o.AllowAnonymousOperations = true);
        var app = builder.Build();
        app.UseTempoReportServerTenantContext();
        app.MapTempoReportServerApi();
        await app.Services.EnsureTempoReportServerDatabaseAsync();
        await app.StartAsync();

        try
        {
            var handler = app.GetTestServer().CreateHandler();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [ReportServerClientExtensions.BaseUrlConfigurationKey] = "http://localhost",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTempoReportServerClient(configuration);
            // Route the typed client through the in-memory test server transport.
            services.AddHttpClient<ITempoReportServerClient, TempoReportServerClient>()
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            await using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<ITempoReportServerClient>();

            var folder = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = "tenant-a", Name = "Finance" });
            var report = await client.CreateReportAsync(new CreateReportRequestDto
            {
                TenantId = "tenant-a",
                FolderId = folder.FolderId,
                Name = "Sales Register",
                DefinitionJson = "{\"id\":\"sales\"}",
            });

            var results = await client.SearchReportsAsync(new ReportSearchRequestDto { TenantId = "tenant-a", Query = "Sales" });
            results.Should().ContainSingle(r => r.ReportId == report.ReportId);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public void AddTempoReportServerClient_MissingBaseUrl_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var act = () => services.AddTempoReportServerClient(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{ReportServerClientExtensions.BaseUrlConfigurationKey}*");
    }
}
