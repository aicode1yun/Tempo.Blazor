using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Phase 0 "red" smoke specification for the future Tempo Report Server API host.
/// The host MUST expose anonymous (no authentication) liveness endpoints:
/// <c>GET /health</c> and <c>GET /version</c>, each returning HTTP 200.
/// These endpoints are intentionally NOT wired yet (they arrive in Phase 1),
/// so this test is expected to FAIL (404) until then.
/// </summary>
public sealed class HealthAndVersionEndpointsTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/version")]
    public async Task Host_RespondsWithOk_Anonymously(string path)
    {
        await using var host = await ReportServerHost.CreateAsync();

        // No authentication headers are attached: the endpoint must be anonymous.
        var response = await host.Client.GetAsync(path);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the report server host must expose an anonymous '{0}' endpoint",
            path);
    }

    private sealed class ReportServerHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly SqliteConnection _connection;

        private ReportServerHost(WebApplication app, SqliteConnection connection)
        {
            _app = app;
            _connection = connection;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<ReportServerHost> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connection));

            var app = builder.Build();
            app.UseTempoReportServerTenantContext();
            app.MapTempoReportServerApi();
            await app.Services.EnsureTempoReportServerDatabaseAsync().ConfigureAwait(false);
            await app.StartAsync().ConfigureAwait(false);

            return new ReportServerHost(app, connection);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
