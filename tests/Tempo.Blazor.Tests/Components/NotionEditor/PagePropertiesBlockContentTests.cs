using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class PagePropertiesBlockContentTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PagePropertiesBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        IBlockContent content = new PagePropertiesBlockContent
        {
            Rows =
            [
                new PagePropertyRow { Key = "Status", ValueHtml = "<strong>Green</strong>" },
                new PagePropertyRow { Key = "Owner", ValueHtml = "Product" }
            ]
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<PagePropertiesBlockContent>();
        var properties = (PagePropertiesBlockContent)restored!;
        properties.Rows.Should().HaveCount(2);
        properties.Rows[0].Key.Should().Be("Status");
        properties.Rows[0].ValueHtml.Should().Be("<strong>Green</strong>");
        properties.Rows[1].Key.Should().Be("Owner");
        properties.Rows[1].ValueHtml.Should().Be("Product");
    }

    [Fact]
    public void PagePropertiesReportBlockContent_RoundtripsThroughPolymorphicBlockContent()
    {
        IBlockContent content = new PagePropertiesReportBlockContent
        {
            Labels = ["release", "customer-facing"],
            Columns = ["Status", "Owner", "Risk"]
        };

        var json = JsonSerializer.Serialize(content, _options);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _options);

        restored.Should().BeOfType<PagePropertiesReportBlockContent>();
        var report = (PagePropertiesReportBlockContent)restored!;
        report.Labels.Should().Equal("release", "customer-facing");
        report.Columns.Should().Equal("Status", "Owner", "Risk");
    }

    [Fact]
    public void PagePropertiesBlockTypes_AreAvailable()
    {
        Enum.GetNames<BlockType>().Should().Contain(
            [nameof(BlockType.PageProperties), nameof(BlockType.PagePropertiesReport)]);
    }
}
