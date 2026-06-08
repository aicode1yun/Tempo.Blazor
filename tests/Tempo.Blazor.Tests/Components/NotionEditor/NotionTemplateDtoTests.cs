using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionTemplateDtoTests
{
    [Fact]
    public void NotionTemplateDto_RoundtripsBlocksWithPolymorphicContent()
    {
        var template = new NotionTemplateDto
        {
            Id = "meeting-notes",
            Name = "Meeting notes",
            Description = "Capture agenda, decisions, and actions.",
            IconEmoji = "M",
            Category = "team",
            Blocks =
            [
                new PageBlock
                {
                    Id = Guid.Parse("cf900000-0000-0000-0000-000000000001"),
                    PageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Type = BlockType.Heading1,
                    Order = 0,
                    Content = new HeadingBlockContent { Level = 1, Html = "Meeting notes" }
                },
                new PageBlock
                {
                    Id = Guid.Parse("cf900000-0000-0000-0000-000000000002"),
                    PageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Type = BlockType.TodoItem,
                    Order = 1,
                    Content = new TodoBlockContent { Html = "Follow up", AssigneeId = "alice" }
                }
            ]
        };

        var json = JsonSerializer.Serialize(template);
        var roundtrip = JsonSerializer.Deserialize<NotionTemplateDto>(json);

        roundtrip.Should().NotBeNull();
        roundtrip!.Id.Should().Be("meeting-notes");
        roundtrip.Blocks.Should().HaveCount(2);
        roundtrip.Blocks[0].Content.Should().BeOfType<HeadingBlockContent>()
            .Which.Html.Should().Be("Meeting notes");
        roundtrip.Blocks[1].Content.Should().BeOfType<TodoBlockContent>()
            .Which.AssigneeId.Should().Be("alice");
    }
}
