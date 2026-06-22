using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Tests.Shared;

public class TmTagAndCustomFieldTests
{
    [Fact]
    public void TmTag_ToRef_Copies_Display_Fields()
    {
        var tag = new TmTag
        {
            Id = "tag-1",
            Label = "Critical",
            Color = "var(--tm-color-danger)",
            SourceKey = "demo",
            TenantId = "tenant-1"
        };

        var reference = tag.ToRef();

        reference.Id.Should().Be("tag-1");
        reference.Label.Should().Be("Critical");
        reference.Color.Should().Be("var(--tm-color-danger)");
        reference.SourceKey.Should().Be("demo");
        reference.TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public void TmTag_Implements_TagPicker_Interface()
    {
        ITag tag = new TmTag { Id = "feature", Label = "Feature", Color = "#3b82f6" };

        tag.Id.Should().Be("feature");
        tag.Name.Should().Be("Feature");
        tag.Color.Should().Be("#3b82f6");
    }

    [Fact]
    public void TmTagRef_FromLabel_Normalizes_Label()
    {
        var tag = TmTagRef.FromLabel("  Backend  ", color: " #10b981 ", sourceKey: " demo ");

        tag.Id.Should().Be("Backend");
        tag.Label.Should().Be("Backend");
        tag.Color.Should().Be("#10b981");
        tag.SourceKey.Should().Be("demo");
        tag.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TmWorkItem_SetTagLabels_Replaces_String_Labels_With_TagRefs()
    {
        var item = new TmWorkItem();

        item.SetTagLabels([" sprint ", "", "backend"]);

        item.Tags.Should().HaveCount(2);
        item.Tags.Select(tag => tag.Label).Should().Equal("sprint", "backend");
        item.TagLabels.Should().Equal("sprint", "backend");
    }

    [Fact]
    public void TmCustomFieldDefinition_AppliesTo_All_When_EntityTypes_Are_Empty()
    {
        var definition = new TmCustomFieldDefinition
        {
            Id = "cf1",
            Name = "Sprint",
            Type = TmCustomFieldType.List,
            Options = ["Sprint 1", "Sprint 2"]
        };

        definition.IsValid.Should().BeTrue();
        definition.AppliesTo("work-item").Should().BeTrue();
        definition.AppliesTo("page").Should().BeTrue();
    }

    [Fact]
    public void TmCustomFieldDefinition_AppliesTo_Configured_EntityTypes_CaseInsensitive()
    {
        var definition = new TmCustomFieldDefinition
        {
            Id = "cf1",
            Name = "Sprint",
            AppliesToEntityTypes = ["work-item"]
        };

        definition.AppliesTo("WORK-ITEM").Should().BeTrue();
        definition.AppliesTo("page").Should().BeFalse();
    }

    [Fact]
    public void TmCustomFieldValue_Requires_Definition_And_Entity()
    {
        var value = new TmCustomFieldValue
        {
            DefinitionId = "cf1",
            EntityRef = TmEntityRef.Create("work-item", "task-1"),
            Value = "Sprint 1"
        };

        value.IsValid.Should().BeTrue();
    }
}
