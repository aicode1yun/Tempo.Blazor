using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.ReportServer.Api.Security;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Persistence;

/// <summary>
/// Guards the allow-only, user-subject-only contract of <see cref="EfReportFolderPermissionStore"/>.
/// The EF folder-ACL model cannot represent Deny effects or Role/Application subjects, so those
/// combinations must fail loudly (<see cref="ReportPermissionUnsupportedException"/>) instead of being
/// silently dropped, which would produce a false security assurance and a lying audit trail. The
/// in-memory store keeps supporting the full model unchanged.
/// </summary>
public sealed class EfReportFolderPermissionStoreTests
{
    private const string TenantId = "acme";
    private const string FolderId = "folder-1";

    [Fact]
    public async Task GrantAclEntry_UserAllow_Persists()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowUser(FolderId, "user-1", ReportPermission.View | ReportPermission.Render),
            context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Should().ContainSingle(entry => entry.SubjectId == "user-1" && entry.Effect == ReportAclEffect.Allow);
    }

    [Fact]
    public async Task GrantAclEntry_UserDeny_Throws_AndPersistsNothing()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        var act = async () => await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.DenyUser(FolderId, "user-1", ReportPermission.View),
            context);

        await act.Should().ThrowAsync<ReportPermissionUnsupportedException>();
        (await harness.Store.ListFolderAclEntriesAsync(FolderId, context)).Should().BeEmpty();
    }

    [Fact]
    public async Task GrantAclEntry_RoleAllow_Throws_AndPersistsNothing()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        var act = async () => await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowRole(FolderId, ReportServerRole.Author, ReportPermission.View),
            context);

        await act.Should().ThrowAsync<ReportPermissionUnsupportedException>();
        (await harness.Store.ListFolderAclEntriesAsync(FolderId, context)).Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAclEntry_NonUserSubject_Throws()
    {
        using var harness = await StoreHarness.CreateAsync();

        var act = async () => await harness.Store.RevokeAclEntryAsync(
            FolderId,
            ReportAclSubjectKind.Role,
            ReportServerRole.Author.ToString(),
            harness.ExecutionContext);

        await act.Should().ThrowAsync<ReportPermissionUnsupportedException>();
    }

    [Fact]
    public async Task InMemoryStore_SupportsDenyAndRole_Unchanged()
    {
        var store = new InMemoryReportPermissionStore();
        var context = new ReportExecutionContext(TenantId, "actor", "en-US");

        await store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.DenyUser(FolderId, "user-1", ReportPermission.View),
            context);
        await store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowRole(FolderId, ReportServerRole.Author, ReportPermission.View),
            context);

        var entries = await store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Should().Contain(entry => entry.SubjectKind == ReportAclSubjectKind.User && entry.Effect == ReportAclEffect.Deny);
        entries.Should().Contain(entry => entry.SubjectKind == ReportAclSubjectKind.Role && entry.Effect == ReportAclEffect.Allow);
    }

    private sealed class StoreHarness : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ReportServerDbContext _dbContext;

        private StoreHarness(SqliteConnection connection, ReportServerDbContext dbContext)
        {
            _connection = connection;
            _dbContext = dbContext;
            Store = new EfReportFolderPermissionStore(dbContext);
        }

        public EfReportFolderPermissionStore Store { get; }

        public ReportExecutionContext ExecutionContext { get; } = new(TenantId, "actor", "en-US");

        public static async Task<StoreHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var requestContext = new ReportServerRequestContext();
            requestContext.Set(new ReportExecutionContext(TenantId, "actor", "en-US"));
            var options = new DbContextOptionsBuilder<ReportServerDbContext>().UseSqlite(connection).Options;
            var dbContext = new ReportServerDbContext(options, requestContext);
            await dbContext.Database.EnsureCreatedAsync();
            return new StoreHarness(connection, dbContext);
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
