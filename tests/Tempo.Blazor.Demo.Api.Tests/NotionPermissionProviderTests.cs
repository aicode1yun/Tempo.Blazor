using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public sealed class NotionPermissionProviderTests
{
    [Fact]
    public async Task EffectivePermission_InheritsNearestRestrictedAncestor()
    {
        var pages = new MockNotionDataStore();
        pages.SeedE2ERestrictionsPage();
        var provider = new DemoNotionPermissionProvider(pages);

        await provider.SetRestrictionsAsync(new PageRestrictionDto
        {
            PageId = MockNotionDataStore.Page1Id,
            Mode = PageRestrictionMode.Open
        });

        var root = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page1Id, "bob");
        root.Permission.Should().Be(PageRestrictionPermission.Edit);

        await provider.SetRestrictionsAsync(new PageRestrictionDto
        {
            PageId = MockNotionDataStore.Page2Id,
            Mode = PageRestrictionMode.EditForSome,
            Entries =
            [
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.User,
                    SubjectId = "alice",
                    Permission = PageRestrictionPermission.Edit
                },
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.Group,
                    SubjectId = "readers",
                    Permission = PageRestrictionPermission.View
                }
            ]
        });

        var child = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page2Id, "bob", ["readers"]);
        child.Permission.Should().Be(PageRestrictionPermission.View);
        child.IsInherited.Should().BeFalse();

        var grandchild = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page3Id, "bob", ["readers"]);
        grandchild.Permission.Should().Be(PageRestrictionPermission.View);
        grandchild.IsInherited.Should().BeTrue();
        grandchild.SourcePageId.Should().Be(MockNotionDataStore.Page2Id);
    }

    [Fact]
    public async Task SetRestrictions_ReplacesEntriesAtomicallyAndUserEntryWinsOverGroup()
    {
        var pages = new MockNotionDataStore();
        var provider = new DemoNotionPermissionProvider(pages);

        await provider.SetRestrictionsAsync(new PageRestrictionDto
        {
            PageId = MockNotionDataStore.Page1Id,
            Mode = PageRestrictionMode.EditForSome,
            Entries =
            [
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.Group,
                    SubjectId = "readers",
                    Permission = PageRestrictionPermission.Edit
                },
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.User,
                    SubjectId = "bob",
                    Permission = PageRestrictionPermission.View
                }
            ]
        });

        var effective = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page1Id, "bob", ["readers"]);
        effective.Permission.Should().Be(PageRestrictionPermission.View);

        await provider.SetRestrictionsAsync(new PageRestrictionDto
        {
            PageId = MockNotionDataStore.Page1Id,
            Mode = PageRestrictionMode.EditForSome,
            Entries =
            [
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.User,
                    SubjectId = "alice",
                    Permission = PageRestrictionPermission.Edit
                }
            ]
        });

        var replaced = await provider.GetRestrictionsAsync(MockNotionDataStore.Page1Id);
        replaced.Entries.Should().ContainSingle();
        replaced.Entries.Single().SubjectId.Should().Be("alice");

        var removed = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page1Id, "bob", ["readers"]);
        removed.Permission.Should().Be(PageRestrictionPermission.None);
    }

    [Fact]
    public async Task EffectivePermission_MissingPage_ReturnsOpenEditInsteadOfThrowing()
    {
        var provider = new DemoNotionPermissionProvider(new MockNotionDataStore());

        var effective = await provider.GetEffectivePermissionAsync(Guid.NewGuid(), "alice", ["editors"]);

        effective.Mode.Should().Be(PageRestrictionMode.Open);
        effective.Permission.Should().Be(PageRestrictionPermission.Edit);
    }

    [Fact]
    public async Task EffectivePermission_OrphanParent_ReturnsOpenEditInsteadOfThrowing()
    {
        var pages = new MockNotionDataStore();
        var parent = await pages.CreatePageAsync(null, "Deleted parent");
        var child = await pages.CreatePageAsync(parent.Id.ToString("D"), "Orphan child");
        await pages.PermanentlyDeletePageAsync(parent.Id.ToString("D"));
        var provider = new DemoNotionPermissionProvider(pages);

        var effective = await provider.GetEffectivePermissionAsync(child.Id, "alice", ["editors"]);

        effective.Mode.Should().Be(PageRestrictionMode.Open);
        effective.Permission.Should().Be(PageRestrictionPermission.Edit);
    }

    [Fact]
    public async Task EffectivePermission_GroupNoneEntryDeniesInsteadOfFallingBackToDefaultEdit()
    {
        var pages = new MockNotionDataStore();
        var provider = new DemoNotionPermissionProvider(pages);

        await provider.SetRestrictionsAsync(new PageRestrictionDto
        {
            PageId = MockNotionDataStore.Page1Id,
            Mode = PageRestrictionMode.ReadOnlyForSome,
            Entries =
            [
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.Group,
                    SubjectId = "guests",
                    Permission = PageRestrictionPermission.None
                }
            ]
        });

        var effective = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page1Id, "charlie", ["guests"]);

        effective.Permission.Should().Be(PageRestrictionPermission.None);
    }

    [Fact]
    public async Task EffectivePermission_MultipleMatchingGroups_ReturnsMostPermissiveEntry()
    {
        var pages = new MockNotionDataStore();
        var provider = new DemoNotionPermissionProvider(pages);

        await provider.SetRestrictionsAsync(new PageRestrictionDto
        {
            PageId = MockNotionDataStore.Page1Id,
            Mode = PageRestrictionMode.EditForSome,
            Entries =
            [
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.Group,
                    SubjectId = "readers",
                    Permission = PageRestrictionPermission.View
                },
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.Group,
                    SubjectId = "editors",
                    Permission = PageRestrictionPermission.Edit
                }
            ]
        });

        var effective = await provider.GetEffectivePermissionAsync(MockNotionDataStore.Page1Id, "alice", ["readers", "editors"]);

        effective.Permission.Should().Be(PageRestrictionPermission.Edit);
    }
}
