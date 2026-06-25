using System.Text.Json;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Tests.Shared;

public sealed class TmEntityRefTests
{
    [Fact]
    public void DefaultReference_IsEmptyAndInvalid()
    {
        var reference = new TmEntityRef();

        reference.IsEmpty.Should().BeTrue();
        reference.IsValid.Should().BeFalse();
        reference.ToQualifiedKey().Should().BeEmpty();
    }

    [Fact]
    public void Create_TrimsValuesAndClearsBlankOptionals()
    {
        var reference = TmEntityRef.Create(
            " work-item ",
            " TASK-1 ",
            sourceKey: " demo ",
            tenantId: " tenant-a ",
            displayName: " Build UI ",
            url: " ");

        reference.EntityType.Should().Be("work-item");
        reference.EntityId.Should().Be("TASK-1");
        reference.SourceKey.Should().Be("demo");
        reference.TenantId.Should().Be("tenant-a");
        reference.DisplayName.Should().Be("Build UI");
        reference.Url.Should().BeNull();
        reference.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_RequiresTypeAndId()
    {
        var missingType = () => TmEntityRef.Create("", "1");
        var missingId = () => TmEntityRef.Create("work-item", " ");

        missingType.Should().Throw<ArgumentException>().WithParameterName("entityType");
        missingId.Should().Throw<ArgumentException>().WithParameterName("entityId");
    }

    [Fact]
    public void Equality_UsesIdentityFieldsOnly()
    {
        var first = TmEntityRef.Create(
            "Work-Item",
            "TASK-1",
            sourceKey: "Demo",
            tenantId: "Tenant",
            displayName: "Old title",
            url: "/old");
        var second = TmEntityRef.Create(
            "work-item",
            "TASK-1",
            sourceKey: "demo",
            tenantId: "tenant",
            displayName: "New title",
            url: "/new");

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equality_TreatsEntityIdAsCaseSensitive()
    {
        var upper = TmEntityRef.Create("work-item", "TASK-1", sourceKey: "demo");
        var lower = TmEntityRef.Create("work-item", "task-1", sourceKey: "demo");

        upper.Should().NotBe(lower);
    }

    [Fact]
    public void ToQualifiedKey_IncludesOptionalScope()
    {
        var reference = TmEntityRef.Create("document", "doc-1", sourceKey: "library", tenantId: "tenant-a");

        reference.ToQualifiedKey().Should().Be("tenant:tenant-a|source:library|type:document|id:doc-1");
        reference.ToString().Should().Be("tenant:tenant-a|source:library|type:document|id:doc-1");
    }

    [Fact]
    public void Normalize_TreatsNullRequiredValuesAsEmpty()
    {
        var reference = new TmEntityRef
        {
            EntityType = null!,
            EntityId = null!,
            SourceKey = " ",
            TenantId = " tenant-a "
        };

        var normalized = reference.Normalize();

        normalized.EntityType.Should().BeEmpty();
        normalized.EntityId.Should().BeEmpty();
        normalized.SourceKey.Should().BeNull();
        normalized.TenantId.Should().Be("tenant-a");
        normalized.ToQualifiedKey().Should().BeEmpty();
    }

    [Fact]
    public void Json_RoundtripsPublicProperties()
    {
        var reference = TmEntityRef.Create(
            "page",
            "page-1",
            sourceKey: "notion",
            tenantId: "space-a",
            displayName: "Roadmap",
            url: "/pages/page-1");

        var json = JsonSerializer.Serialize(reference, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<TmEntityRef>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().Be(reference);
        restored!.DisplayName.Should().Be("Roadmap");
        restored.Url.Should().Be("/pages/page-1");
    }
}
