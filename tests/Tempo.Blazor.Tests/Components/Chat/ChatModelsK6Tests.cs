using FluentAssertions;
using Tempo.Blazor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Chat;

/// <summary>K6: additive ChatMessage fields (threads, edit, reactions, receipts) and helpers.</summary>
public class ChatModelsK6Tests
{
    private static ChatUser User(string id) => new() { Id = id, Name = id };

    [Fact]
    public void ChatMessage_Defaults_AreBackwardCompatible()
    {
        var m = new ChatMessage { Id = "1", Text = "hi" };
        m.Reactions.Should().BeEmpty();
        m.ReadBy.Should().BeEmpty();
        m.IsEdited.Should().BeFalse();
        m.IsReply.Should().BeFalse();
        m.IsDeleted.Should().BeFalse();
        m.ReplyCount.Should().Be(0);
    }

    [Fact]
    public void IsEdited_TracksEditedAt()
    {
        new ChatMessage { Id = "1", Text = "x" }.IsEdited.Should().BeFalse();
        new ChatMessage { Id = "1", Text = "x", EditedAt = DateTimeOffset.UtcNow }.IsEdited.Should().BeTrue();
    }

    [Fact]
    public void IsReply_TrueForThreadReplies()
    {
        new ChatMessage { Id = "2", Text = "re", ReplyToId = "1" }.IsReply.Should().BeTrue();
        new ChatMessage { Id = "3", Text = "re", ThreadRootId = "1" }.IsReply.Should().BeTrue();
        new ChatMessage { Id = "1", Text = "root" }.IsReply.Should().BeFalse();
    }

    [Fact]
    public void IsReadByUser_ChecksReceiptsAndLegacyFlag()
    {
        var m = new ChatMessage
        {
            Id = "1",
            Text = "x",
            ReadBy = [new ChatReadReceipt(User("bob"))]
        };
        m.IsReadByUser("bob").Should().BeTrue();
        m.IsReadByUser("carol").Should().BeFalse();

        // Legacy IsRead bool still counts as read.
        new ChatMessage { Id = "1", Text = "x", IsRead = true }.IsReadByUser("anyone").Should().BeTrue();
    }

    [Fact]
    public void ChatReaction_CountAndReactedBy()
    {
        var r = new ChatReaction("👍", [User("a"), User("b")]);
        r.Count.Should().Be(2);
        r.ReactedBy("a").Should().BeTrue();
        r.ReactedBy("z").Should().BeFalse();
    }
}
