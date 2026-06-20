using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class ChildrenDisplayBlockContentTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ChildrenDisplayBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        var rootPageId = Guid.Parse("cf130000-0000-0000-0000-000000000002");
        IBlockContent content = new ChildrenDisplayBlockContent
        {
            RootPageId = rootPageId,
            Depth = 3,
            ShowIcons = false
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<ChildrenDisplayBlockContent>();
        var children = (ChildrenDisplayBlockContent)restored!;
        children.RootPageId.Should().Be(rootPageId);
        children.Depth.Should().Be(3);
        children.ShowIcons.Should().BeFalse();
    }

    [Fact]
    public void ChildrenDisplayBlockContent_NormalizesInvalidDepthAndDefaultsIcons()
    {
        var content = new ChildrenDisplayBlockContent
        {
            Depth = -1
        };

        content.Depth.Should().Be(0);
        content.ShowIcons.Should().BeTrue();
    }

    [Fact]
    public void ChildrenDisplayBlockType_IsAvailable()
    {
        Enum.GetNames<BlockType>().Should().Contain(nameof(BlockType.ChildrenDisplay));
    }
}
