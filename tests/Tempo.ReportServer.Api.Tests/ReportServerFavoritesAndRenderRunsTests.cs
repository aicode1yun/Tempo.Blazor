using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tempo.ReportServer.Api.Security;
using Tempo.ReportServer.Api.Storage;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Integration specification for the Fáze 12 per-user favorites and ad-hoc render-run history endpoints.
/// Favorites are server-side and scoped to the authenticated principal; render runs are persisted for
/// both success and failure. Header-based principals exercise the resolver directly (the ASP.NET
/// authentication gate is orthogonal and covered elsewhere).
/// </summary>
public sealed class ReportServerFavoritesAndRenderRunsTests
{
    private const string TenantId = "tenant-a";

    [Fact]
    public async Task AddFavorite_PersistsAndIsReturnedForSamePrincipal_ButNotForAnotherUser()
    {
        await using var host = await FavoritesTestApp.CreateAsync();
        var report = await host.SeedReportAsync();

        using var user1 = host.CreateUserClient("user-1", "Viewer");
        var add = await user1.PostAsJsonAsync("/api/favorites", new AddReportFavoriteRequestDto
        {
            TenantId = TenantId,
            ReportId = report.ReportId,
        });
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        // Persisted in the store.
        host.CountFavorites(TenantId, "user-1", report.ReportId).Should().Be(1);

        var user1List = await user1.GetFromJsonAsync<List<ReportFavoriteDto>>(
            $"/api/favorites?tenantId={TenantId}");
        user1List.Should().ContainSingle(f => f.ReportId == report.ReportId && f.ReportName == report.Name);

        using var user2 = host.CreateUserClient("user-2", "Viewer");
        var user2List = await user2.GetFromJsonAsync<List<ReportFavoriteDto>>(
            $"/api/favorites?tenantId={TenantId}");
        user2List.Should().BeEmpty();
    }

    [Fact]
    public async Task AddFavorite_IsIdempotent()
    {
        await using var host = await FavoritesTestApp.CreateAsync();
        var report = await host.SeedReportAsync();

        using var user1 = host.CreateUserClient("user-1", "Viewer");
        (await user1.PostAsJsonAsync("/api/favorites", Add(report.ReportId))).EnsureSuccessStatusCode();
        (await user1.PostAsJsonAsync("/api/favorites", Add(report.ReportId))).EnsureSuccessStatusCode();

        host.CountFavorites(TenantId, "user-1", report.ReportId).Should().Be(1);
    }

    [Fact]
    public async Task DeleteFavorite_RemovesIt_Returns204_Then404()
    {
        await using var host = await FavoritesTestApp.CreateAsync();
        var report = await host.SeedReportAsync();

        using var user1 = host.CreateUserClient("user-1", "Viewer");
        (await user1.PostAsJsonAsync("/api/favorites", Add(report.ReportId))).EnsureSuccessStatusCode();

        var delete = await user1.DeleteAsync($"/api/favorites/{report.ReportId}?tenantId={TenantId}");
        var deleteAgain = await user1.DeleteAsync($"/api/favorites/{report.ReportId}?tenantId={TenantId}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        deleteAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.CountFavorites(TenantId, "user-1", report.ReportId).Should().Be(0);
    }

    [Fact]
    public async Task GetFavorites_Unauthenticated_Returns401()
    {
        await using var host = await FavoritesTestApp.CreateAsync();

        using var anonymous = host.CreateAnonymousClient();
        var response = await anonymous.GetAsync($"/api/favorites?tenantId={TenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFavorites_CrossTenant_Returns403()
    {
        await using var host = await FavoritesTestApp.CreateAsync();

        using var user1 = host.CreateUserClient("user-1", "Viewer");
        var response = await user1.GetAsync("/api/favorites?tenantId=other-tenant");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddFavorite_EmptyReportId_Returns400()
    {
        await using var host = await FavoritesTestApp.CreateAsync();

        using var user1 = host.CreateUserClient("user-1", "Viewer");
        var response = await user1.PostAsJsonAsync("/api/favorites", new AddReportFavoriteRequestDto
        {
            TenantId = TenantId,
            ReportId = string.Empty,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Render_ThenGetRenderRuns_ShowsSucceededRunForCaller()
    {
        await using var host = await FavoritesTestApp.CreateAsync();
        var report = await host.SeedReportAsync();

        using var viewer = host.CreateUserClient("viewer-1", "Viewer");
        var render = await viewer.PostAsJsonAsync("/api/render", RenderRequest(report.ReportId));
        render.StatusCode.Should().Be(HttpStatusCode.OK);

        var runs = await viewer.GetFromJsonAsync<List<RenderRunDto>>(
            $"/api/render/runs?tenantId={TenantId}&reportId={report.ReportId}");
        runs.Should().ContainSingle(run =>
            run.ReportId == report.ReportId &&
            run.ActorId == "viewer-1" &&
            run.Outcome == "Succeeded");

        // The run history is per-user: another user does not see it.
        using var other = host.CreateUserClient("viewer-2", "Viewer");
        var otherRuns = await other.GetFromJsonAsync<List<RenderRunDto>>(
            $"/api/render/runs?tenantId={TenantId}");
        otherRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Render_QuotaFailure_IsAlsoRecordedAsRenderRun()
    {
        // MaxSynchronousPages = -1 forces every render over the page quota (PageQuotaExceeded / 413).
        await using var host = await FavoritesTestApp.CreateAsync(
            quota: new ReportServerQuotaOptions { MaxSynchronousPages = -1 });
        var report = await host.SeedReportAsync();

        using var viewer = host.CreateUserClient("viewer-1", "Viewer");
        var render = await viewer.PostAsJsonAsync("/api/render", RenderRequest(report.ReportId));
        render.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        var runs = await viewer.GetFromJsonAsync<List<RenderRunDto>>(
            $"/api/render/runs?tenantId={TenantId}");
        runs.Should().ContainSingle(run =>
            run.ReportId == report.ReportId && run.Outcome == "PageQuotaExceeded");
    }

    private static AddReportFavoriteRequestDto Add(string reportId)
        => new() { TenantId = TenantId, ReportId = reportId };

    private static RenderReportRequestDto RenderRequest(string reportId)
        => new()
        {
            TenantId = TenantId,
            ReportId = reportId,
            Format = ReportRenderFormat.Snapshot,
            CultureName = "en-US",
        };

    private static string FixtureDefinitionJson()
        => ReportDefinitionJsonSerializer.Serialize(new ReportDefinition
        {
            Id = "sales-register",
            Name = "Sales Register",
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

    private sealed class FavoritesTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly SqliteConnection _connection;

        private FavoritesTestApp(WebApplication app, SqliteConnection connection)
        {
            _app = app;
            _connection = connection;
        }

        public static async Task<FavoritesTestApp> CreateAsync(ReportServerQuotaOptions? quota = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connection));
            if (quota is not null)
            {
                // ReportServerQuotaOptions is an init-only record, so override the resolved IOptions
                // rather than mutating it in a configure callback.
                builder.Services.AddSingleton<IOptions<ReportServerQuotaOptions>>(Options.Create(quota));
            }

            var app = builder.Build();
            app.UseTempoReportServerTenantContext();
            app.MapTempoReportServerApi();

            await app.Services.EnsureTempoReportServerDatabaseAsync().ConfigureAwait(false);
            await app.StartAsync().ConfigureAwait(false);
            return new FavoritesTestApp(app, connection);
        }

        public HttpClient CreateUserClient(string userId, string roles)
        {
            var client = _app.GetTestClient();
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.TenantId, TenantId);
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.UserId, userId);
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.Roles, roles);
            return client;
        }

        public HttpClient CreateAnonymousClient() => _app.GetTestClient();

        public int CountFavorites(string tenantId, string userId, string reportId)
        {
            using var scope = _app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ReportServerDbContext>();
            return dbContext.Favorites.Count(f =>
                f.TenantId == tenantId && f.UserId == userId && f.ReportId == reportId);
        }

        public async Task<ReportDetailDto> SeedReportAsync()
        {
            using var author = CreateUserClient("seed-author", "Author");
            var folderResponse = await author.PostAsJsonAsync("/api/folders", new CreateReportFolderRequestDto
            {
                TenantId = TenantId,
                Name = "Finance",
            });
            folderResponse.EnsureSuccessStatusCode();
            var folder = (await folderResponse.Content.ReadFromJsonAsync<ReportFolderDto>())!;

            var reportResponse = await author.PostAsJsonAsync("/api/reports", new CreateReportRequestDto
            {
                TenantId = TenantId,
                FolderId = folder.FolderId,
                Name = "Sales Register",
                DefinitionJson = FixtureDefinitionJson(),
            });
            reportResponse.EnsureSuccessStatusCode();
            return (await reportResponse.Content.ReadFromJsonAsync<ReportDetailDto>())!;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
