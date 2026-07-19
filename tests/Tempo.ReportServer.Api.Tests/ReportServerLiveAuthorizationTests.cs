using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Integration specification for the in-handler ACL enforcement and audit trail on the live
/// <c>/api</c> catalog, render and data-source endpoints (Fáze 11). The endpoints resolve the report
/// security principal, enforce the required folder permission, and audit the outcome (Allowed after a
/// successful operation, Denied on a rejected one). Header-based principals exercise the resolver
/// directly (the ASP.NET authentication gate is orthogonal and covered elsewhere).
/// </summary>
public sealed class ReportServerLiveAuthorizationTests
{
    private const string TenantId = "tenant-a";

    [Fact]
    public async Task Render_AsViewerWithRenderPermission_Returns200_AndAuditsAllowed()
    {
        await using var host = await LiveAuthTestApp.CreateAsync();
        var report = await host.SeedReportAsync();

        using var viewer = host.CreateUserClient("viewer-1", "Viewer");
        var response = await viewer.PostAsJsonAsync("/api/render", RenderRequest(report.ReportId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await host.ListAuditAsync();
        events.Should().Contain(e =>
            e.Action == ReportAuditAction.RenderReport &&
            e.ActorId == "viewer-1" &&
            e.ResourceId == report.ReportId &&
            e.Outcome == ReportAuditOutcome.Allowed);
    }

    [Fact]
    public async Task Render_ForReportInDeniedFolder_Returns403_AndAuditsDenied()
    {
        await using var host = await LiveAuthTestApp.CreateAsync();
        var report = await host.SeedReportAsync();
        await host.DenyRenderAsync(report.FolderId, "viewer-1");

        using var viewer = host.CreateUserClient("viewer-1", "Viewer");
        var response = await viewer.PostAsJsonAsync("/api/render", RenderRequest(report.ReportId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var events = await host.ListAuditAsync();
        events.Should().Contain(e =>
            e.Action == ReportAuditAction.RenderReport &&
            e.ActorId == "viewer-1" &&
            e.ResourceId == report.ReportId &&
            e.Outcome == ReportAuditOutcome.Denied);
    }

    [Fact]
    public async Task CreateReport_AsViewer_Returns403_ButAsAuthor_Returns200()
    {
        await using var host = await LiveAuthTestApp.CreateAsync();
        var folder = await host.SeedFolderAsync();

        using var viewer = host.CreateUserClient("viewer-1", "Viewer");
        var viewerResponse = await viewer.PostAsJsonAsync("/api/reports", new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Viewer Attempt",
            DefinitionJson = FixtureDefinitionJson(),
        });

        using var author = host.CreateUserClient("author-9", "Author");
        var authorResponse = await author.PostAsJsonAsync("/api/reports", new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Author Report",
            DefinitionJson = FixtureDefinitionJson(),
        });

        viewerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        authorResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateFolder_AsViewer_Returns403_ButAsAuthor_Returns201()
    {
        await using var host = await LiveAuthTestApp.CreateAsync();

        using var viewer = host.CreateUserClient("viewer-1", "Viewer");
        var viewerResponse = await viewer.PostAsJsonAsync("/api/folders", new CreateReportFolderRequestDto
        {
            TenantId = TenantId,
            Name = "Viewer Folder",
        });

        using var author = host.CreateUserClient("author-9", "Author");
        var authorResponse = await author.PostAsJsonAsync("/api/folders", new CreateReportFolderRequestDto
        {
            TenantId = TenantId,
            Name = "Author Folder",
        });

        viewerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        authorResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

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

    private sealed class LiveAuthTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly SqliteConnection _connection;

        private LiveAuthTestApp(WebApplication app, SqliteConnection connection)
        {
            _app = app;
            _connection = connection;
        }

        public static async Task<LiveAuthTestApp> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connection));

            var app = builder.Build();
            app.UseTempoReportServerTenantContext();
            // Map without RequireAuthorization: the in-handler ACL enforcement resolves header-based
            // principals through the ReportHttpSecurityContextFactory (the ASP.NET auth gate is
            // orthogonal and only accepts API keys / bearer tokens).
            app.MapTempoReportServerApi();

            await app.Services.EnsureTempoReportServerDatabaseAsync().ConfigureAwait(false);
            await app.StartAsync().ConfigureAwait(false);
            return new LiveAuthTestApp(app, connection);
        }

        public HttpClient CreateUserClient(string userId, string roles)
        {
            var client = _app.GetTestClient();
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.TenantId, TenantId);
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.UserId, userId);
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.Roles, roles);
            return client;
        }

        public async Task<ReportFolderDto> SeedFolderAsync()
        {
            using var author = CreateUserClient("seed-author", "Author");
            var response = await author.PostAsJsonAsync("/api/folders", new CreateReportFolderRequestDto
            {
                TenantId = TenantId,
                Name = "Finance",
            });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ReportFolderDto>())!;
        }

        public async Task<ReportDetailDto> SeedReportAsync()
        {
            var folder = await SeedFolderAsync();
            using var author = CreateUserClient("seed-author", "Author");
            var response = await author.PostAsJsonAsync("/api/reports", new CreateReportRequestDto
            {
                TenantId = TenantId,
                FolderId = folder.FolderId,
                Name = "Sales Register",
                DefinitionJson = FixtureDefinitionJson(),
            });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ReportDetailDto>())!;
        }

        public async Task DenyRenderAsync(string folderId, string userId)
        {
            using var scope = _app.Services.CreateScope();
            var requestContext = scope.ServiceProvider.GetRequiredService<ReportServerRequestContext>();
            requestContext.Set(new ReportExecutionContext(TenantId, "admin", "en-US"));
            var store = scope.ServiceProvider.GetRequiredService<IReportPermissionStore>();
            await store.GrantAclEntryAsync(
                folderId,
                ReportFolderAclEntry.DenyUser(folderId, userId, ReportPermission.Render),
                new ReportExecutionContext(TenantId, "admin", "en-US"));
        }

        public async Task<IReadOnlyList<ReportAuditEvent>> ListAuditAsync()
        {
            using var scope = _app.Services.CreateScope();
            var auditLog = scope.ServiceProvider.GetRequiredService<IReportAuditLog>();
            return await auditLog.ListAsync(TenantId);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
