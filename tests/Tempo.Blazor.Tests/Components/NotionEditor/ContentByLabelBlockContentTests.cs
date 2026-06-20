using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class ContentByLabelBlockContentTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ContentByLabelBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        IBlockContent content = new ContentByLabelBlockContent
        {
            Labels = ["release", "customer success"],
            MaxItems = 7,
            SortBy = ContentByLabelSortBy.TitleDescending
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<ContentByLabelBlockContent>();
        var contentByLabel = (ContentByLabelBlockContent)restored!;
        contentByLabel.Labels.Should().Equal("release", "customer success");
        contentByLabel.MaxItems.Should().Be(7);
        contentByLabel.SortBy.Should().Be(ContentByLabelSortBy.TitleDescending);
    }

    [Fact]
    public void ContentByLabelBlockContent_NormalizesNullAndInvalidValues()
    {
        var content = new ContentByLabelBlockContent
        {
            Labels = null!,
            MaxItems = 0
        };

        content.Labels.Should().BeEmpty();
        content.MaxItems.Should().Be(10);
    }
}
