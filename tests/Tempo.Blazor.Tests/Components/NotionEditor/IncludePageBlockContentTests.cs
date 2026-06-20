using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class IncludePageBlockContentTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void IncludePageBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        var sourcePageId = Guid.Parse("cf120000-0000-0000-0000-000000000002");
        IBlockContent content = new IncludePageBlockContent
        {
            SourcePageId = sourcePageId
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<IncludePageBlockContent>();
        ((IncludePageBlockContent)restored!).SourcePageId.Should().Be(sourcePageId);
    }

    [Fact]
    public void IncludePageBlockType_IsAvailable()
    {
        Enum.GetNames<BlockType>().Should().Contain(nameof(BlockType.IncludePage));
    }
}
