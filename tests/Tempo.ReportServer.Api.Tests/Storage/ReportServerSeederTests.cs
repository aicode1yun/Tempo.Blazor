using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Persistence;

/// <summary>Idempotency specification for <see cref="ReportServerSeeder"/>.</summary>
public sealed class ReportServerSeederTests
{
    [Fact]
    public async Task Seed_OnEmptyCatalog_CreatesRootFolderAndOwnerGrant()
    {
        var options = new ReportServerSeedOptions
        {
            Enabled = true,
            TenantId = "acme",
            OwnerSubject = "user-owner",
            OwnerRole = "Admin",
        };
        using var harness = await SeedHarness.CreateAsync("acme");

        var first = await ReportServerSeeder.SeedAsync(harness.Context, options);

        first.Should().BeTrue();
        var roots = await harness.Context.Folders.IgnoreQueryFilters()
            .Where(folder => folder.TenantId == "acme" && folder.Path == "/").ToListAsync();
        roots.Should().ContainSingle();
        var grants = await harness.Context.FolderPermissions
            .Where(grant => grant.TenantId == "acme" && grant.SubjectId == "user-owner").ToListAsync();
        grants.Should().ContainSingle().Which.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Seed_RunTwice_IsIdempotent()
    {
        var options = new ReportServerSeedOptions
        {
            Enabled = true,
            TenantId = "acme",
            OwnerSubject = "user-owner",
        };
        using var harness = await SeedHarness.CreateAsync("acme");

        var first = await ReportServerSeeder.SeedAsync(harness.Context, options);
        var second = await ReportServerSeeder.SeedAsync(harness.Context, options);

        first.Should().BeTrue();
        second.Should().BeFalse("a second seed pass must not insert duplicate data");
        var roots = await harness.Context.Folders.IgnoreQueryFilters()
            .Where(folder => folder.TenantId == "acme" && folder.Path == "/").CountAsync();
        roots.Should().Be(1);
        var grants = await harness.Context.FolderPermissions
            .Where(grant => grant.TenantId == "acme" && grant.SubjectId == "user-owner").CountAsync();
        grants.Should().Be(1);
    }

    private sealed class SeedHarness : IDisposable
    {
        private readonly SqliteConnection _connection;

        private SeedHarness(SqliteConnection connection, ReportServerDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public ReportServerDbContext Context { get; }

        public static async Task<SeedHarness> CreateAsync(string tenantId)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var requestContext = new ReportServerRequestContext();
            requestContext.Set(new ReportExecutionContext(tenantId, "seed", "en-US"));
            var options = new DbContextOptionsBuilder<ReportServerDbContext>().UseSqlite(connection).Options;
            var context = new ReportServerDbContext(options, requestContext);
            await context.Database.EnsureCreatedAsync();
            return new SeedHarness(connection, context);
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
