using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Integration specification for the deployable Tempo Report Server API host: the catalog/render
/// surface requires an authenticated principal (JWT bearer or API key), while <c>/health</c> and
/// <c>/version</c> stay anonymous. Verified through an in-process host mirroring the production
/// authentication and authorization wiring.
/// </summary>
public sealed class ReportServerHostAuthTests
{
    [Theory]
    [InlineData(ReportRenderFormat.Pdf, "application/pdf")]
    [InlineData(ReportRenderFormat.Xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData(ReportRenderFormat.Csv, "text/csv")]
    public async Task RenderEndpoint_WithApiKey_ReturnsNonEmptyBytesForFixtureReport(
        ReportRenderFormat format,
        string expectedContentType)
    {
        await using var host = await ReportServerAuthTestApp.CreateAsync();
        var authorizedClient = host.CreateApiKeyClient();
        var api = new TempoReportServerClient(authorizedClient);
        var reportId = await CreateFixtureReportAsync(api);

        var result = await api.RenderAsync(new RenderReportRequestDto
        {
            TenantId = TenantId,
            ReportId = reportId,
            Format = format,
            CultureName = "en-US",
        });

        result.Format.Should().Be(format);
        result.ContentType.Should().Be(expectedContentType);
        result.Bytes.Should().NotBeEmpty("the render endpoint must return {0} payload bytes", format);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutTokenOrApiKey_Returns401()
    {
        await using var host = await ReportServerAuthTestApp.CreateAsync();
        using var anonymousClient = host.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync($"/api/folders?tenantId={TenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenderEndpoint_WithApiKey_ForUnknownReport_Returns404()
    {
        await using var host = await ReportServerAuthTestApp.CreateAsync();
        var authorizedClient = host.CreateApiKeyClient();

        var response = await authorizedClient.PostAsJsonAsync("/api/render", new RenderReportRequestDto
        {
            TenantId = TenantId,
            ReportId = "does-not-exist",
            Format = ReportRenderFormat.Pdf,
            CultureName = "en-US",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/version")]
    public async Task HostDiagnostics_StayAnonymous_OnAuthenticatedHost(string path)
    {
        await using var host = await ReportServerAuthTestApp.CreateAsync();
        using var anonymousClient = host.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private const string TenantId = "tenant-a";

    private static async Task<string> CreateFixtureReportAsync(TempoReportServerClient api)
    {
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto
        {
            TenantId = TenantId,
            Name = "Finance",
        });
        var report = await api.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Sales Register",
            DefinitionJson = FixtureDefinitionJson(),
        });
        return report.ReportId;
    }

    private static string FixtureDefinitionJson()
        => ReportDefinitionJsonSerializer.Serialize(new ReportDefinition
        {
            Id = "sales-register",
            Name = "Sales Register",
            DataSets =
            [
                new ReportDataSetDefinition { Name = "main" },
            ],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 60,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "title",
                            X = 24,
                            Y = 24,
                            Width = 320,
                            Height = 24,
                            Text = "Sales Register",
                        },
                    ],
                },
            },
        });

    private sealed class ReportServerAuthTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly SqliteConnection _connection;
        private readonly string _apiKey;

        private ReportServerAuthTestApp(WebApplication app, SqliteConnection connection, string apiKey)
        {
            _app = app;
            _connection = connection;
            _apiKey = apiKey;
        }

        public static async Task<ReportServerAuthTestApp> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connection));
            // Last registration wins: supply deterministic tabular rows so CSV/XLSX exports are non-empty.
            builder.Services.AddScoped<IReportDataProvider, FixtureReportDataProvider>();
            builder.Services.AddReportServerAuthentication(builder.Configuration);

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseTempoReportServerTenantContext();
            app.MapTempoReportServerApi()
                .RequireAuthorization(ReportServerAuthenticationDefaults.ApiPolicy);

            await app.Services.EnsureTempoReportServerDatabaseAsync().ConfigureAwait(false);
            await app.StartAsync().ConfigureAwait(false);

            var keyStore = app.Services.GetRequiredService<IReportApiKeyStore>();
            var created = await keyStore.CreateAsync(TenantId, "integration-tests", ReportPermission.All).ConfigureAwait(false);

            return new ReportServerAuthTestApp(app, connection, created.PlainTextKey);
        }

        public HttpClient CreateApiKeyClient()
        {
            var client = _app.GetTestClient();
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.ApiKey, _apiKey);
            return client;
        }

        public HttpClient CreateAnonymousClient() => _app.GetTestClient();

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class FixtureReportDataProvider : IReportDataProvider
    {
        public Task<ReportDataSetResult> GetDataAsync(
            string dataSetName,
            ReportDataQuery query,
            IReadOnlyDictionary<string, ReportParameterValue> parameters,
            ReportExecutionContext context)
            => Task.FromResult(new ReportDataSetResult([], Rows(context.CancellationToken)));

        private static async IAsyncEnumerable<ReportDataRow> Rows(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new ReportDataRow(new Dictionary<string, object?> { ["Name"] = "Alpha", ["Value"] = 1 });
            yield return new ReportDataRow(new Dictionary<string, object?> { ["Name"] = "Beta", ["Value"] = 2 });
        }
    }
}
