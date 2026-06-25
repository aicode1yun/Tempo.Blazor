using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Tests.Shared;

public class TmResourceTests
{
    [Fact]
    public void ToRef_CopiesIdentityAndDisplaySnapshot()
    {
        var resource = new TmResource
        {
            Id = "room-a",
            DisplayName = "Room A",
            ResourceType = "room",
            Color = "var(--tm-color-primary)",
            SourceKey = "facility",
            TenantId = "tenant-a"
        };

        var resourceRef = resource.ToRef();

        resourceRef.Id.Should().Be("room-a");
        resourceRef.DisplayName.Should().Be("Room A");
        resourceRef.ResourceType.Should().Be("room");
        resourceRef.Color.Should().Be("var(--tm-color-primary)");
        resourceRef.SourceKey.Should().Be("facility");
        resourceRef.TenantId.Should().Be("tenant-a");
    }

    [Fact]
    public void Equality_UsesScopedIdentityAndIgnoresDisplaySnapshot()
    {
        var first = new TmResource
        {
            Id = "resource-1",
            DisplayName = "Room A",
            SourceKey = "Facilities",
            TenantId = "Tenant-A"
        };
        var second = new TmResource
        {
            Id = "resource-1",
            DisplayName = "Renamed Room",
            SourceKey = "facilities",
            TenantId = "tenant-a"
        };
        var differentSource = new TmResource
        {
            Id = "resource-1",
            SourceKey = "equipment",
            TenantId = "tenant-a"
        };

        first.Should().Be(second);
        first.Should().NotBe(differentSource);
    }

    [Fact]
    public void ScheduleResource_CanRoundTripThroughSharedResource()
    {
        var scheduleResource = new TmScheduleResource
        {
            Id = "laser-cutter",
            Name = "Laser Cutter",
            ResourceType = "equipment",
            Color = "var(--tm-color-warning)",
            GroupId = "workshop",
            SortOrder = 3,
            SourceKey = "assets",
            TenantId = "tenant-a"
        };

        var shared = scheduleResource.ToResource();
        var restored = TmScheduleResource.FromResource(shared);

        shared.Id.Should().Be("laser-cutter");
        shared.DisplayName.Should().Be("Laser Cutter");
        restored.Id.Should().Be(scheduleResource.Id);
        restored.Name.Should().Be(scheduleResource.Name);
        restored.ResourceType.Should().Be(scheduleResource.ResourceType);
        restored.Color.Should().Be(scheduleResource.Color);
        restored.GroupId.Should().Be(scheduleResource.GroupId);
        restored.SortOrder.Should().Be(scheduleResource.SortOrder);
        restored.SourceKey.Should().Be(scheduleResource.SourceKey);
        restored.TenantId.Should().Be(scheduleResource.TenantId);
    }
}
