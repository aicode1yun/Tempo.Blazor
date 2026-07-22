using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Persistence;

/// <summary>
/// Guards the per-user favorite persistence contract of <see cref="EfReportFavoriteStore"/>: adds are
/// idempotent (the unique (tenant, user, report) key prevents duplicates), listing is scoped to a single
/// user within a tenant, and removal reports whether a row was deleted.
/// </summary>
public sealed class EfReportFavoriteStoreTests
{
    private const string TenantId = "acme";

    [Fact]
    public async Task AddAsync_IsIdempotent_NoDuplicateRow()
    {
        using var harness = await FavoriteStoreHarness.CreateAsync();

        var first = await harness.Store.AddAsync(TenantId, "user-1", "report-a");
        var second = await harness.Store.AddAsync(TenantId, "user-1", "report-a");

        first.Id.Should().Be(second.Id);
        var list = await harness.Store.ListAsync(TenantId, "user-1");
        list.Should().ContainSingle(favorite => favorite.ReportId == "report-a");
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyThatUsersFavorites()
    {
        using var harness = await FavoriteStoreHarness.CreateAsync();

        await harness.Store.AddAsync(TenantId, "user-1", "report-a");
        await harness.Store.AddAsync(TenantId, "user-2", "report-b");

        var forUser1 = await harness.Store.ListAsync(TenantId, "user-1");

        forUser1.Should().ContainSingle(favorite => favorite.ReportId == "report-a");
        forUser1.Should().NotContain(favorite => favorite.ReportId == "report-b");
    }

    [Fact]
    public async Task RemoveAsync_RemovesFavorite_AndReportsWhetherFound()
    {
        using var harness = await FavoriteStoreHarness.CreateAsync();
        await harness.Store.AddAsync(TenantId, "user-1", "report-a");

        var removed = await harness.Store.RemoveAsync(TenantId, "user-1", "report-a");
        var removedAgain = await harness.Store.RemoveAsync(TenantId, "user-1", "report-a");

        removed.Should().BeTrue();
        removedAgain.Should().BeFalse();
        (await harness.Store.ListAsync(TenantId, "user-1")).Should().BeEmpty();
    }

    private sealed class FavoriteStoreHarness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ReportServerDbContext _dbContext;

        private FavoriteStoreHarness(SqliteConnection connection, ReportServerDbContext dbContext)
        {
            _connection = connection;
            _dbContext = dbContext;
            Store = new EfReportFavoriteStore(dbContext, TimeProvider.System);
        }

        public EfReportFavoriteStore Store { get; }

        public static async Task<FavoriteStoreHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var requestContext = new ReportServerRequestContext();
            requestContext.Set(new ReportExecutionContext(TenantId, "actor", "en-US"));
            var options = new DbContextOptionsBuilder<ReportServerDbContext>().UseSqlite(connection).Options;
            var dbContext = new ReportServerDbContext(options, requestContext);
            await dbContext.Database.EnsureCreatedAsync();
            return new FavoriteStoreHarness(connection, dbContext);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
