using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class PageReactionDtoTests
{
    [Fact]
    public void PageReactionDto_RoundTripsAndDerivesCount()
    {
        var dto = new PageReactionDto
        {
            Reaction = "🎉",
            UserIds = ["alice", "bob"]
        };

        var json = JsonSerializer.Serialize(dto);
        var restored = JsonSerializer.Deserialize<PageReactionDto>(json);

        restored.Should().NotBeNull();
        restored!.Reaction.Should().Be("🎉");
        restored.UserIds.Should().BeEquivalentTo(["alice", "bob"]);
        restored.Count.Should().Be(2);
    }
}
