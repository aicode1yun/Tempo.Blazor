using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class ExcerptBlockContentTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ExcerptBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        IBlockContent content = new ExcerptBlockContent
        {
            Html = "Release <strong>summary</strong>"
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<ExcerptBlockContent>();
        ((ExcerptBlockContent)restored!).Html.Should().Be("Release <strong>summary</strong>");
    }

    [Fact]
    public void ExcerptIncludeBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        var sourcePageId = Guid.Parse("cf140000-0000-0000-0000-000000000002");
        IBlockContent content = new ExcerptIncludeBlockContent
        {
            SourcePageId = sourcePageId
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<ExcerptIncludeBlockContent>();
        ((ExcerptIncludeBlockContent)restored!).SourcePageId.Should().Be(sourcePageId);
    }

    [Fact]
    public void ExcerptBlockTypes_AreAvailable()
    {
        Enum.GetNames<BlockType>().Should().Contain([nameof(BlockType.Excerpt), nameof(BlockType.ExcerptInclude)]);
    }
}
