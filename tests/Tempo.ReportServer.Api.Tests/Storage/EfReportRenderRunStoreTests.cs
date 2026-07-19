using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Persistence;

/// <summary>
/// Guards the render-run history persistence contract of <see cref="EfReportRenderRunStore"/>: runs are
/// recorded, listing is newest-first and scoped to a single actor within a tenant, and the optional
/// report filter constrains the result set.
/// </summary>
public sealed class EfReportRenderRunStoreTests
{
    private const string TenantId = "acme";

    [Fact]
    public async Task RecordAndList_ReturnsNewestFirst()
    {
        using var harness = await RenderRunStoreHarness.CreateAsync();
        var baseTime = DateTimeOffset.UtcNow;

        await harness.Store.RecordAsync(Run("actor-1", "report-a", "Succeeded", baseTime));
        await harness.Store.RecordAsync(Run("actor-1", "report-a", "TimedOut", baseTime.AddSeconds(1)));

        var runs = await harness.Store.ListAsync(TenantId, "actor-1", reportId: null, max: 50);

        runs.Should().HaveCount(2);
        runs[0].Outcome.Should().Be("TimedOut");
        runs[1].Outcome.Should().Be("Succeeded");
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyThatActorsRuns()
    {
        using var harness = await RenderRunStoreHarness.CreateAsync();
        var now = DateTimeOffset.UtcNow;

        await harness.Store.RecordAsync(Run("actor-1", "report-a", "Succeeded", now));
        await harness.Store.RecordAsync(Run("actor-2", "report-a", "Succeeded", now));

        var runs = await harness.Store.ListAsync(TenantId, "actor-1", reportId: null, max: 50);

        runs.Should().ContainSingle(run => run.ActorId == "actor-1");
    }

    [Fact]
    public async Task ListAsync_FiltersByReportId()
    {
        using var harness = await RenderRunStoreHarness.CreateAsync();
        var now = DateTimeOffset.UtcNow;

        await harness.Store.RecordAsync(Run("actor-1", "report-a", "Succeeded", now));
        await harness.Store.RecordAsync(Run("actor-1", "report-b", "Succeeded", now));

        var runs = await harness.Store.ListAsync(TenantId, "actor-1", reportId: "report-b", max: 50);

        runs.Should().ContainSingle(run => run.ReportId == "report-b");
    }

    private static RenderRunEntity Run(string actorId, string reportId, string outcome, DateTimeOffset createdAt)
        => new()
        {
            TenantId = TenantId,
            ActorId = actorId,
            ReportId = reportId,
            Format = "Snapshot",
            Outcome = outcome,
            ParametersJson = "{}",
            CreatedAt = createdAt,
        };

    private sealed class RenderRunStoreHarness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ReportServerDbContext _dbContext;

        private RenderRunStoreHarness(SqliteConnection connection, ReportServerDbContext dbContext)
        {
            _connection = connection;
            _dbContext = dbContext;
            Store = new EfReportRenderRunStore(dbContext);
        }

        public EfReportRenderRunStore Store { get; }

        public static async Task<RenderRunStoreHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var requestContext = new ReportServerRequestContext();
            requestContext.Set(new ReportExecutionContext(TenantId, "actor", "en-US"));
            var options = new DbContextOptionsBuilder<ReportServerDbContext>().UseSqlite(connection).Options;
            var dbContext = new ReportServerDbContext(options, requestContext);
            await dbContext.Database.EnsureCreatedAsync();
            return new RenderRunStoreHarness(connection, dbContext);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
