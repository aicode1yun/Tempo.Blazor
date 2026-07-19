using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions;
using Tempo.ReportServer.Api.Security;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Tests.Persistence;

/// <summary>
/// Guards the full-fidelity ACL persistence contract of <see cref="EfReportFolderPermissionStore"/>.
/// The EF folder-ACL model persists the subject kind (User/Role/Application), the effect (Allow/Deny)
/// and the explicit permission flags of each entry, so Deny effects and Role/Application subjects round
/// trip faithfully instead of being rejected. Legacy rows (no explicit permission bits) still project
/// their granted role into permissions for backward compatibility. The in-memory store keeps supporting
/// the full model unchanged.
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
        entries.Should().ContainSingle(entry =>
            entry.SubjectKind == ReportAclSubjectKind.User &&
            entry.SubjectId == "user-1" &&
            entry.Effect == ReportAclEffect.Allow);
    }

    [Fact]
    public async Task GrantAclEntry_RoleAllow_RoundTripsAsRoleEntry()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowRole(FolderId, ReportServerRole.Author, ReportPermission.View),
            context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Should().ContainSingle(entry =>
            entry.SubjectKind == ReportAclSubjectKind.Role &&
            entry.SubjectId == ReportServerRole.Author.ToString() &&
            entry.Effect == ReportAclEffect.Allow);
    }

    [Fact]
    public async Task GrantAclEntry_ApplicationAllow_RoundTripsAsApplicationEntry()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowApplication(FolderId, "embed-app", ReportPermission.Render),
            context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Should().ContainSingle(entry =>
            entry.SubjectKind == ReportAclSubjectKind.Application &&
            entry.SubjectId == "embed-app" &&
            entry.Effect == ReportAclEffect.Allow);
    }

    [Fact]
    public async Task GrantAclEntry_UserDeny_RoundTripsAsDeny_AndIsInherited()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.DenyUser(FolderId, "user-1", ReportPermission.Render),
            context);

        var direct = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        direct.Should().ContainSingle(entry =>
            entry.SubjectId == "user-1" && entry.Effect == ReportAclEffect.Deny);

        var inherited = await harness.Store.ListInheritedAclEntriesAsync(FolderId, context);
        inherited.Should().ContainSingle(entry =>
            entry.SubjectId == "user-1" && entry.Effect == ReportAclEffect.Deny);
    }

    [Fact]
    public async Task GrantAclEntry_ExplicitPermissionBits_RoundTripExactly_NotRoleProjected()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        // Render only: a role projection would widen this to View|Render|Export, so an exact round trip
        // proves the store persists the raw permission bits rather than re-deriving them from the role.
        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowUser(FolderId, "user-1", ReportPermission.Render),
            context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Single(entry => entry.SubjectId == "user-1").Permissions.Should().Be(ReportPermission.Render);
    }

    [Fact]
    public async Task GrantAclEntry_AllowAndDenyForSameSubject_CoexistOnFolder()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowUser(FolderId, "user-1", ReportPermission.View),
            context);
        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.DenyUser(FolderId, "user-1", ReportPermission.Render),
            context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Should().Contain(entry => entry.SubjectId == "user-1" && entry.Effect == ReportAclEffect.Allow);
        entries.Should().Contain(entry => entry.SubjectId == "user-1" && entry.Effect == ReportAclEffect.Deny);
    }

    [Fact]
    public async Task RevokeAclEntry_RoleSubject_RemovesTheGrant()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;
        await harness.Store.GrantAclEntryAsync(
            FolderId,
            ReportFolderAclEntry.AllowRole(FolderId, ReportServerRole.Author, ReportPermission.View),
            context);

        await harness.Store.RevokeAclEntryAsync(
            FolderId,
            ReportAclSubjectKind.Role,
            ReportServerRole.Author.ToString(),
            context);

        (await harness.Store.ListFolderAclEntriesAsync(FolderId, context)).Should().BeEmpty();
    }

    [Fact]
    public async Task SetAclEntries_PersistsMixedSubjectsAndEffects()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        await harness.Store.SetAclEntriesAsync(
            FolderId,
            [
                ReportFolderAclEntry.AllowRole(FolderId, ReportServerRole.Viewer, ReportPermission.Render),
                ReportFolderAclEntry.AllowApplication(FolderId, "embed-app", ReportPermission.Render),
                ReportFolderAclEntry.DenyUser(FolderId, "user-1", ReportPermission.Render),
            ],
            context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Should().Contain(entry => entry.SubjectKind == ReportAclSubjectKind.Role && entry.Effect == ReportAclEffect.Allow);
        entries.Should().Contain(entry => entry.SubjectKind == ReportAclSubjectKind.Application);
        entries.Should().Contain(entry => entry.SubjectKind == ReportAclSubjectKind.User && entry.Effect == ReportAclEffect.Deny);
    }

    [Fact]
    public async Task LegacyRoleGrant_WithoutExplicitPermissions_ProjectsRoleIntoPermissions()
    {
        using var harness = await StoreHarness.CreateAsync();
        var context = harness.ExecutionContext;

        // GrantAsync is the legacy role-based grant path: it stores a role name with no explicit
        // permission bits, so the store must project the role into permissions when reading back.
        await harness.Store.GrantAsync(FolderId, "sub-viewer", ReportServerRole.Viewer, context);

        var entries = await harness.Store.ListFolderAclEntriesAsync(FolderId, context);
        entries.Single(entry => entry.SubjectId == "sub-viewer").Permissions
            .Should().Be(ReportPermission.View | ReportPermission.Render | ReportPermission.Export);
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
