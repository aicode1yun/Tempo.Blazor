using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Tests.Security;

public sealed class ReportPermissionResolverTests
{
    [Theory]
    [InlineData(ReportServerRole.TenantAdmin, ReportPermission.ManagePermissions, true)]
    [InlineData(ReportServerRole.Author, ReportPermission.EditDefinition, true)]
    [InlineData(ReportServerRole.Author, ReportPermission.ManageDataSources, false)]
    [InlineData(ReportServerRole.Viewer, ReportPermission.Render, true)]
    [InlineData(ReportServerRole.Viewer, ReportPermission.EditDefinition, false)]
    public async Task AuthorizeAsync_AppliesBuiltInRolePermissions(
        ReportServerRole role,
        ReportPermission permission,
        bool expected)
    {
        var resolver = new ReportPermissionResolver(new InMemoryReportPermissionStore());
        var principal = ReportSecurityContext.ForUser("tenant-a", "user-1", [role]);

        var result = await resolver.AuthorizeAsync(
            principal,
            new ReportPermissionRequirement(permission, ReportResourceKind.ReportDefinition));

        result.Allowed.Should().Be(expected);
    }

    [Fact]
    public async Task AuthorizeAsync_InheritsFolderAclsAndDenyOverridesAllow()
    {
        var store = new InMemoryReportPermissionStore();
        var tenant = new ReportExecutionContext("tenant-a", "admin", "en-US");
        await store.SaveFolderAsync(new ReportFolderPermissionNode("root"), tenant);
        await store.SaveFolderAsync(new ReportFolderPermissionNode("finance", ParentFolderId: "root"), tenant);
        await store.SetAclEntriesAsync(
            "root",
            [
                ReportFolderAclEntry.AllowRole("root", ReportServerRole.Viewer, ReportPermission.Render),
                ReportFolderAclEntry.AllowUser("root", "author-1", ReportPermission.ManageDataSources),
            ],
            tenant);
        await store.SetAclEntriesAsync(
            "finance",
            [
                ReportFolderAclEntry.DenyUser("finance", "viewer-1", ReportPermission.Render),
                ReportFolderAclEntry.AllowUser("finance", "viewer-1", ReportPermission.EditDefinition),
            ],
            tenant);
        var resolver = new ReportPermissionResolver(store);

        var viewer = ReportSecurityContext.ForUser("tenant-a", "viewer-1", [ReportServerRole.Viewer]);
        var author = ReportSecurityContext.ForUser("tenant-a", "author-1", [ReportServerRole.Author]);

        var denied = await resolver.AuthorizeAsync(
            viewer,
            new ReportPermissionRequirement(ReportPermission.Render, ReportResourceKind.Render, FolderRouteKey: "finance"),
            "finance");
        var inheritedGrant = await resolver.AuthorizeAsync(
            author,
            new ReportPermissionRequirement(ReportPermission.ManageDataSources, ReportResourceKind.DataSource, RequiresAuthorRole: true),
            "finance");
        var roleGate = await resolver.AuthorizeAsync(
            viewer,
            new ReportPermissionRequirement(ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, RequiresAuthorRole: true),
            "finance");

        denied.Allowed.Should().BeFalse("explicit user deny on finance must beat viewer role render allow inherited from root");
        inheritedGrant.Allowed.Should().BeTrue("author should inherit a root data-source grant into the finance folder");
        roleGate.Allowed.Should().BeFalse("folder grants must not turn a viewer into an author for CRUD endpoints");
    }

    [Fact]
    public async Task AuthorizeAsync_FolderDenyForUser_BeatsRoleAllow()
    {
        var store = new InMemoryReportPermissionStore();
        var tenant = new ReportExecutionContext("tenant-a", "admin", "en-US");
        await store.SaveFolderAsync(new ReportFolderPermissionNode("finance"), tenant);
        await store.SetAclEntriesAsync(
            "finance",
            [ReportFolderAclEntry.DenyUser("finance", "viewer-1", ReportPermission.Render)],
            tenant);
        var resolver = new ReportPermissionResolver(store);

        // The viewer role would normally grant Render; an explicit user Deny on the folder must win.
        var viewer = ReportSecurityContext.ForUser("tenant-a", "viewer-1", [ReportServerRole.Viewer]);
        var result = await resolver.AuthorizeAsync(
            viewer,
            new ReportPermissionRequirement(ReportPermission.Render, ReportResourceKind.Render),
            "finance");

        result.Allowed.Should().BeFalse("an explicit folder Deny beats the role-granted allow");
    }

    [Fact]
    public async Task AuthorizeAsync_RoleSubjectAllow_GrantsUserHoldingThatRole()
    {
        var store = new InMemoryReportPermissionStore();
        var tenant = new ReportExecutionContext("tenant-a", "admin", "en-US");
        await store.SaveFolderAsync(new ReportFolderPermissionNode("finance"), tenant);
        await store.SetAclEntriesAsync(
            "finance",
            [ReportFolderAclEntry.AllowRole("finance", ReportServerRole.Author, ReportPermission.ManageDataSources)],
            tenant);
        var resolver = new ReportPermissionResolver(store);

        // ManageDataSources is not part of the Author base permissions, so it can only come from the
        // Role-subject folder grant applying to a user that holds the Author role.
        var author = ReportSecurityContext.ForUser("tenant-a", "author-1", [ReportServerRole.Author]);
        var result = await resolver.AuthorizeAsync(
            author,
            new ReportPermissionRequirement(ReportPermission.ManageDataSources, ReportResourceKind.DataSource, RequiresAuthorRole: true),
            "finance");

        result.Allowed.Should().BeTrue("a Role-subject allow applies to every user holding that role");
    }

    [Fact]
    public async Task AuthorizeAsync_ApplicationSubjectAllow_GrantsApiKeyWithThatApplicationId()
    {
        var store = new InMemoryReportPermissionStore();
        var tenant = new ReportExecutionContext("tenant-a", "admin", "en-US");
        await store.SaveFolderAsync(new ReportFolderPermissionNode("finance"), tenant);
        await store.SetAclEntriesAsync(
            "finance",
            [ReportFolderAclEntry.AllowApplication("finance", "embed-app", ReportPermission.Render)],
            tenant);
        var resolver = new ReportPermissionResolver(store);

        // The API key carries no base Render scope; the Application-subject folder grant supplies it.
        var apiKey = ReportSecurityContext.ForApiKey(new ReportApiKeyDescriptor
        {
            TenantId = "tenant-a",
            ApplicationId = "embed-app",
            KeyId = "rk_1",
            Permissions = ReportPermission.None,
        });
        var result = await resolver.AuthorizeAsync(
            apiKey,
            new ReportPermissionRequirement(ReportPermission.Render, ReportResourceKind.Render),
            "finance");

        result.Allowed.Should().BeTrue("an Application-subject allow applies to an API key with that ApplicationId");
    }

    [Fact]
    public async Task AuthorizeAsync_ApiKeyWithEditDefinitionScope_SatisfiesAuthorOnlyRequirement()
    {
        var resolver = new ReportPermissionResolver(new InMemoryReportPermissionStore());
        var apiKey = ReportSecurityContext.ForApiKey(new ReportApiKeyDescriptor
        {
            TenantId = "tenant-a",
            ApplicationId = "embed-app",
            KeyId = "rk_1",
            Permissions = ReportPermission.View | ReportPermission.EditDefinition,
        });

        // A machine key has no realm role, so its EditDefinition scope alone must satisfy an author-only
        // write requirement (its permission scope is its authority).
        var result = await resolver.AuthorizeAsync(
            apiKey,
            new ReportPermissionRequirement(ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, RequiresAuthorRole: true));

        result.Allowed.Should().BeTrue("an EditDefinition-scoped API key is author-equivalent for writes");
    }

    [Fact]
    public async Task AuthorizeAsync_RenderOnlyApiKey_CannotSatisfyAuthorOnlyRequirement()
    {
        var resolver = new ReportPermissionResolver(new InMemoryReportPermissionStore());
        var apiKey = ReportSecurityContext.ForApiKey(new ReportApiKeyDescriptor
        {
            TenantId = "tenant-a",
            ApplicationId = "embed-app",
            KeyId = "rk_2",
            Permissions = ReportPermission.Render,
        });

        // Without the EditDefinition bit the key still fails the write on the permission scope.
        var result = await resolver.AuthorizeAsync(
            apiKey,
            new ReportPermissionRequirement(ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, RequiresAuthorRole: true));

        result.Allowed.Should().BeFalse("a Render-only API key lacks the EditDefinition scope for a write");
    }

    [Fact]
    public async Task AuthorizeAsync_IsTenantScoped()
    {
        var store = new InMemoryReportPermissionStore();
        await store.SaveFolderAsync(new ReportFolderPermissionNode("finance"), Context("tenant-a"));
        await store.SetAclEntriesAsync(
            "finance",
            [ReportFolderAclEntry.AllowUser("finance", "author-1", ReportPermission.ManageDataSources)],
            Context("tenant-a"));
        var resolver = new ReportPermissionResolver(store);

        var tenantBPrincipal = ReportSecurityContext.ForUser("tenant-b", "author-1", [ReportServerRole.Author]);
        var result = await resolver.AuthorizeAsync(
            tenantBPrincipal,
            new ReportPermissionRequirement(ReportPermission.ManageDataSources, ReportResourceKind.DataSource, RequiresAuthorRole: true),
            "finance");

        result.Allowed.Should().BeFalse();
    }

    private static ReportExecutionContext Context(string tenantId)
        => new(tenantId, "admin", "en-US");
}
