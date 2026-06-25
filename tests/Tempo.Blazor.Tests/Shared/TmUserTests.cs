using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Abstractions.WorkItems;

namespace Tempo.Blazor.Tests.Shared;

public class TmUserTests
{
    [Fact]
    public void ToRef_CopiesIdentityAndDisplaySnapshot()
    {
        var user = new TmUser
        {
            Id = "u1",
            DisplayName = "Ada Lovelace",
            UserName = "ada",
            Email = "ada@example.test",
            AvatarUrl = "https://example.test/ada.png",
            Color = "var(--tm-color-primary)",
            IsVirtual = true,
            SourceKey = "directory",
            TenantId = "tenant-a"
        };

        var userRef = user.ToRef();

        userRef.Id.Should().Be("u1");
        userRef.DisplayName.Should().Be("Ada Lovelace");
        userRef.UserName.Should().Be("ada");
        userRef.Email.Should().Be("ada@example.test");
        userRef.AvatarUrl.Should().Be("https://example.test/ada.png");
        userRef.Color.Should().Be("var(--tm-color-primary)");
        userRef.IsVirtual.Should().BeTrue();
        userRef.SourceKey.Should().Be("directory");
        userRef.TenantId.Should().Be("tenant-a");
    }

    [Fact]
    public void Equality_UsesScopedIdentityAndIgnoresDisplaySnapshot()
    {
        var first = new TmUser
        {
            Id = "u1",
            DisplayName = "Ada",
            SourceKey = "Directory",
            TenantId = "Tenant-A"
        };
        var second = new TmUser
        {
            Id = "u1",
            DisplayName = "Ada Lovelace",
            SourceKey = "directory",
            TenantId = "tenant-a"
        };
        var differentTenant = new TmUser
        {
            Id = "u1",
            SourceKey = "directory",
            TenantId = "tenant-b"
        };

        first.Should().Be(second);
        first.Should().NotBe(differentTenant);
    }

    [Fact]
    public void WorkItemAssignee_CanRoundTripThroughUserRef()
    {
        var userRef = new TmUserRef
        {
            Id = "u1",
            DisplayName = "Grace Hopper",
            UserName = "grace",
            Email = "grace@example.test",
            AvatarUrl = "https://example.test/grace.png",
            Color = "var(--tm-color-accent)",
            SourceKey = "hr",
            TenantId = "tenant-a"
        };

        var assignee = TmWorkItemAssignee.FromUserRef(userRef, hourlyRate: 125);

        assignee.Name.Should().Be("Grace Hopper");
        assignee.HourlyRate.Should().Be(125);
        assignee.ToUserRef().Should().BeEquivalentTo(userRef);
    }
}
