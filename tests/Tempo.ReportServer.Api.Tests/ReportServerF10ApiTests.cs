using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Api.Rendering;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.ReportServer.Api;

namespace Tempo.ReportServer.Api.Tests;

public sealed class ReportServerF10ApiTests
{
    [Fact]
    public async Task CatalogEndpoints_KeepTenantsIsolatedAndExposeRevisionsParametersRenderAndDataSources()
    {
        await using var app = await ReportServerTestApp.CreateAsync();
        var client = new TempoReportServerClient(app.Client);

        var folderA = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = "tenant-a", Name = "Finance" });
        var folderB = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = "tenant-b", Name = "Finance" });
        var reportA = await client.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "tenant-a",
            FolderId = folderA.FolderId,
            Name = "Sales Register",
            DefinitionJson = DefinitionJson("sales-register", "Sales Register"),
        });
        var reportB = await client.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "tenant-b",
            FolderId = folderB.FolderId,
            Name = "Private Register",
            DefinitionJson = DefinitionJson("private-register", "Private Register"),
        });

        var tenantAReports = await client.SearchReportsAsync(new ReportSearchRequestDto { TenantId = "tenant-a", Query = "Register" });
        tenantAReports.Should().ContainSingle(report => report.ReportId == reportA.ReportId);
        tenantAReports.Should().NotContain(report => report.ReportId == reportB.ReportId);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReportAsync(reportB.ReportId, "tenant-a"));

        var parameters = await client.GetParametersAsync(reportA.ReportId, "tenant-a");
        parameters.Should().ContainSingle(parameter => parameter.Name == "Region")
            .Which.Kind.Should().Be(ReportParameterMetadataKind.Select);

        var render = await client.RenderAsync(new RenderReportRequestDto
        {
            TenantId = "tenant-a",
            ReportId = reportA.ReportId,
            Format = ReportRenderFormat.Snapshot,
            CultureName = "en-US",
        });
        render.PageCount.Should().BeGreaterThan(0);
        render.SnapshotJson.Should().Contain("Sales");

        var secondRevision = await client.UpdateReportDefinitionAsync(new UpdateReportDefinitionRequestDto
        {
            TenantId = "tenant-a",
            ReportId = reportA.ReportId,
            ExpectedRevisionId = reportA.LatestRevisionId,
            DefinitionJson = DefinitionJson("sales-register", "Sales Register v2"),
            Comment = "draft update",
        });
        secondRevision.RevisionNumber.Should().Be(2);

        var published = await client.PublishRevisionAsync(reportA.ReportId, "tenant-a", new PublishReportRevisionRequestDto
        {
            RevisionId = secondRevision.RevisionId,
        });
        published.IsPublished.Should().BeTrue();

        var rolledBack = await client.RollbackRevisionAsync(reportA.ReportId, "tenant-a", new RollbackReportRevisionRequestDto
        {
            RevisionId = reportA.LatestRevisionId!,
            Comment = "rollback smoke",
        });
        rolledBack.RevisionNumber.Should().Be(3);
        var revisions = await client.GetRevisionsAsync(reportA.ReportId, "tenant-a");
        revisions.Should().HaveCount(3);

        var archive = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = "tenant-a", Name = "Archive" });
        var moved = await client.MoveReportAsync(reportA.ReportId, "tenant-a", new MoveReportRequestDto { FolderId = archive.FolderId });
        moved.FolderId.Should().Be(archive.FolderId);

        var source = await client.UpsertDataSourceAsync(new UpsertReportDataSourceRequestDto
        {
            TenantId = "tenant-a",
            Name = "orders-db",
            Kind = "sql",
            Connection = "Data Source=:memory:",
        });
        var sources = await client.GetDataSourcesAsync("tenant-a");
        sources.Should().ContainSingle(item => item.DataSourceId == source.DataSourceId);
        var connection = await client.TestDataSourceConnectionAsync(source.DataSourceId, "tenant-a");
        connection.Success.Should().BeTrue();
        var schema = await client.GetDataSourceSchemaAsync(source.DataSourceId, "tenant-a");
        schema.Columns.Should().NotBeEmpty();
        var preview = await client.PreviewDataSourceAsync(source.DataSourceId, "tenant-a", top: 2);
        preview.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task RenderJobs_ProcessTenantsWithRoundRobinFairness()
    {
        await using var app = await ReportServerTestApp.CreateAsync();
        var client = new TempoReportServerClient(app.Client);
        var tenantAFolder = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = "tenant-a", Name = "Finance" });
        var tenantBFolder = await client.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = "tenant-b", Name = "Finance" });
        var reportA = await client.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "tenant-a",
            FolderId = tenantAFolder.FolderId,
            Name = "Tenant A",
            DefinitionJson = DefinitionJson("tenant-a", "Tenant A"),
        });
        var reportB = await client.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = "tenant-b",
            FolderId = tenantBFolder.FolderId,
            Name = "Tenant B",
            DefinitionJson = DefinitionJson("tenant-b", "Tenant B"),
        });

        await client.QueueRenderAsync(RenderRequest("tenant-a", reportA.ReportId));
        await client.QueueRenderAsync(RenderRequest("tenant-a", reportA.ReportId));
        await client.QueueRenderAsync(RenderRequest("tenant-b", reportB.ReportId));

        var queue = app.Services.GetRequiredService<IReportRenderJobQueue>();
        var processed = new[]
        {
            await queue.ProcessNextAsync(),
            await queue.ProcessNextAsync(),
            await queue.ProcessNextAsync(),
        };

        processed.Select(job => job!.TenantId).Should().Equal("tenant-a", "tenant-b", "tenant-a");
        processed.Should().OnlyContain(job => job!.Status == RenderJobStatus.Completed);
    }

    private static RenderReportRequestDto RenderRequest(string tenantId, string reportId)
        => new()
        {
            TenantId = tenantId,
            ReportId = reportId,
            Format = ReportRenderFormat.Snapshot,
            CultureName = "en-US",
        };

    private static string DefinitionJson(string id, string name)
        => ReportDefinitionJsonSerializer.Serialize(new ReportDefinition
        {
            Id = id,
            Name = name,
            Parameters =
            [
                new ReportParameterDefinition
                {
                    Name = "Region",
                    Label = "Region",
                    DataType = ReportParameterType.List,
                    AvailableValues = ReportParameterAvailableValues.Static(
                    [
                        new ReportParameterAvailableValue("EU", "Europe"),
                        new ReportParameterAvailableValue("US", "United States"),
                    ]),
                },
            ],
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 80,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "title",
                            X = 24,
                            Y = 24,
                            Width = 320,
                            Height = 24,
                            Text = name,
                        },
                    ],
                },
            },
        });

    private sealed class ReportServerTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly SqliteConnection _connection;

        private ReportServerTestApp(WebApplication app, SqliteConnection connection)
        {
            _app = app;
            _connection = connection;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _app.Services;

        public static async Task<ReportServerTestApp> CreateAsync()
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
            return new ReportServerTestApp(app, connection);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
