using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.ReportServer.Api.Rendering;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Rendering;

/// <summary>
/// Non-race specification for the SQL-Server-backed distributed render job queue
/// (<see cref="EfReportRenderJobQueue"/>) over a SQLite database: the enqueue → process → Completed
/// round-trip, tenant-scoped lookup, and the failure path. The atomic multi-node claim semantics are
/// exercised separately against a real SQL Server (see <c>EfReportRenderJobQueueMsSqlTests</c>), because
/// the exactly-one-winner guarantee needs a real row-locking database.
/// </summary>
public sealed class EfReportRenderJobQueueTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await using var context = NewContext("tenant-a");
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Enqueue_Process_CompletesTheJob_AndExposesTheDownloadUrl()
    {
        var reportId = await SeedReportAsync("tenant-a");
        var queue = CreateQueue(out var context);
        await using var _ = context;

        var queued = await queue.EnqueueAsync(RenderRequest("tenant-a", reportId));
        queued.Status.Should().Be(RenderJobStatus.Queued);
        queued.JobId.Should().StartWith("job_");

        var processed = await queue.ProcessNextAsync();

        processed.Should().NotBeNull();
        processed!.Status.Should().Be(RenderJobStatus.Completed);
        processed.JobId.Should().Be(queued.JobId);
        processed.DownloadUrl.Should().Be($"api/render/jobs/{queued.JobId}/result");
        processed.SnapshotUrl.Should().Be(processed.DownloadUrl);
        processed.StartedAt.Should().NotBeNull();
        processed.CompletedAt.Should().NotBeNull();

        // The persisted row reflects the terminal state and the lease is released.
        var reloaded = await queue.GetAsync("tenant-a", queued.JobId);
        reloaded!.Status.Should().Be(RenderJobStatus.Completed);
        var row = await context.RenderJobs.AsNoTracking().SingleAsync(j => j.JobId == queued.JobId);
        row.LeaseOwner.Should().BeNull();
        row.LeasedUntilTicks.Should().BeNull();
    }

    [Fact]
    public async Task ProcessNext_WhenQueueEmpty_ReturnsNull()
    {
        var queue = CreateQueue(out var context);
        await using var _ = context;

        (await queue.ProcessNextAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Get_IsTenantScoped()
    {
        var reportId = await SeedReportAsync("tenant-a");
        var queue = CreateQueue(out var context);
        await using var _ = context;
        var queued = await queue.EnqueueAsync(RenderRequest("tenant-a", reportId));

        (await queue.GetAsync("tenant-a", queued.JobId)).Should().NotBeNull();
        (await queue.GetAsync("tenant-b", queued.JobId)).Should().BeNull("a job belongs only to its own tenant");
        (await queue.GetAsync("tenant-a", "job_missing")).Should().BeNull();
    }

    [Fact]
    public async Task Process_WhenReportMissing_MarksTheJobFailed()
    {
        var queue = CreateQueue(out var context);
        await using var _ = context;
        var queued = await queue.EnqueueAsync(RenderRequest("tenant-a", "does-not-exist"));

        var processed = await queue.ProcessNextAsync();

        processed!.Status.Should().Be(RenderJobStatus.Failed);
        processed.ErrorMessage.Should().Contain("does-not-exist");
        var row = await context.RenderJobs.AsNoTracking().SingleAsync(j => j.JobId == queued.JobId);
        row.LeaseOwner.Should().BeNull("a failed job still releases its lease");
        row.LeasedUntilTicks.Should().BeNull();
    }

    private EfReportRenderJobQueue CreateQueue(out ReportServerDbContext context)
    {
        context = NewContext("tenant-a");
        var store = new EfReportServerStore(context);
        var renderer = new ReportServerRenderer(new EmptyReportDataProvider());
        return new EfReportRenderJobQueue(
            context,
            ContextRequestContext(context),
            store,
            renderer,
            new ReportRenderJobQueueOptions(),
            TimeProvider.System,
            new ReportRenderNodeIdentity { NodeId = "sqlite-node" });
    }

    private async Task<string> SeedReportAsync(string tenantId)
    {
        await using var context = NewContext(tenantId);
        var store = new EfReportServerStore(context);
        var folder = await store.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = tenantId, Name = "Finance" });
        var report = await store.CreateReportAsync(
            new CreateReportRequestDto
            {
                TenantId = tenantId,
                FolderId = folder.FolderId,
                Name = "Sales Register",
                DefinitionJson = DefinitionJson(),
            },
            "seed-user");
        return report.ReportId;
    }

    private ReportServerDbContext NewContext(string tenantId)
    {
        var requestContext = new ReportServerRequestContext();
        requestContext.Set(new ReportExecutionContext(tenantId, "test-user", "en-US"));
        var options = new DbContextOptionsBuilder<ReportServerDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new ReportServerDbContext(options, requestContext);
        _contexts[context] = requestContext;
        return context;
    }

    private readonly Dictionary<ReportServerDbContext, ReportServerRequestContext> _contexts = new();

    private ReportServerRequestContext ContextRequestContext(ReportServerDbContext context) => _contexts[context];

    private static RenderReportRequestDto RenderRequest(string tenantId, string reportId)
        => new()
        {
            TenantId = tenantId,
            ReportId = reportId,
            Format = ReportRenderFormat.Snapshot,
            CultureName = "en-US",
        };

    private static string DefinitionJson()
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
                            Width = 240,
                            Height = 24,
                            Text = "Sales",
                        },
                    ],
                },
            },
        });
}
