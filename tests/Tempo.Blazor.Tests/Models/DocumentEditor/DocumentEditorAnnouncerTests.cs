using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public sealed class DocumentEditorAnnouncerTests
{
    [Fact]
    public void Announce_ExposesFirstMessageAndQueuesFollowingMessages()
    {
        var announcer = new DocumentEditorAnnouncer();

        var first = announcer.Announce("Saved");
        var second = announcer.Announce("3 of 8", DocumentEditorAnnouncementPoliteness.Assertive);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        announcer.CurrentMessage.Should().Be("Saved");
        announcer.QueuedCount.Should().Be(1);
    }

    [Fact]
    public void DequeueNext_AdvancesQueuedAnnouncementsInOrder()
    {
        var announcer = new DocumentEditorAnnouncer();
        announcer.Announce("Saved");
        announcer.Announce("1 of 3");
        announcer.Announce("Autosave failed", DocumentEditorAnnouncementPoliteness.Assertive);

        var next = announcer.DequeueNext();

        next!.Message.Should().Be("1 of 3");
        announcer.CurrentMessage.Should().Be("1 of 3");
        announcer.QueuedCount.Should().Be(1);

        announcer.DequeueNext()!.Politeness.Should().Be(DocumentEditorAnnouncementPoliteness.Assertive);
        announcer.CurrentMessage.Should().Be("Autosave failed");
        announcer.QueuedCount.Should().Be(0);
    }

    [Fact]
    public void Announce_IgnoresEmptyMessages()
    {
        var announcer = new DocumentEditorAnnouncer();

        announcer.Announce(" ");

        announcer.CurrentMessage.Should().BeNull();
        announcer.QueuedCount.Should().Be(0);
    }
}
