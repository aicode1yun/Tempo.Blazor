using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Notifications;

public class CommentNotificationOrchestratorTests
{
    private readonly InMemoryNotificationStore _store = new();
    private readonly CommentNotificationOrchestrator _orchestrator;

    public CommentNotificationOrchestratorTests()
    {
        _orchestrator = new CommentNotificationOrchestrator(_store);
    }

    [Fact]
    public async Task OnNewReplyAsync_NotifiesParentAuthor()
    {
        var thread = Thread(Entry("parent", "Parent Author"));
        var parent = thread.Entries.First();
        var reply = Entry("replier", "Replier Name", parent.Id);

        await _orchestrator.OnNewReplyAsync(thread, reply);

        var notes = await GetNotificationsAsync("parent");
        notes.Should().ContainSingle();
        notes[0].Type.Should().Be(TmNotificationTypes.Reply);
        notes[0].EffectiveRecipientUserId.Should().Be("parent");
    }

    [Fact]
    public async Task OnNewReplyAsync_SkipsSelfReply()
    {
        var thread = Thread(Entry("same", "Same User"));
        var parent = thread.Entries.First();
        var reply = Entry("same", "Same User", parent.Id);

        await _orchestrator.OnNewReplyAsync(thread, reply);

        var notes = await GetNotificationsAsync("same");
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OnNewReplyAsync_SkipsWhenNoParent()
    {
        var thread = Thread();
        var reply = Entry("u1", "User");

        await _orchestrator.OnNewReplyAsync(thread, reply);

        var notes = await GetNotificationsAsync("u1");
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OnThreadResolvedAsync_NotifiesParticipants()
    {
        var thread = Thread(
            Entry("alice", "Alice"),
            Entry("bob", "Bob"));

        await _orchestrator.OnThreadResolvedAsync(thread, "alice", "Alice");

        var bobNotes = await GetNotificationsAsync("bob");
        bobNotes.Should().ContainSingle();
        bobNotes[0].Type.Should().Be(TmNotificationTypes.ThreadResolved);

        var aliceNotes = await GetNotificationsAsync("alice");
        aliceNotes.Should().BeEmpty(); // resolver not notified
    }

    [Fact]
    public async Task OnMentionAsync_NotifiesMentionedUser()
    {
        var entry = Entry("author", "Author");

        await _orchestrator.OnMentionAsync(entry, new[] { "mentioned" }, "t1", "page-1");

        var notes = await GetNotificationsAsync("mentioned");
        notes.Should().ContainSingle();
        notes[0].Type.Should().Be(TmNotificationTypes.Mention);
    }

    [Fact]
    public async Task OnReactionAsync_NotifiesEntryAuthor()
    {
        var entry = Entry("author", "Author");

        await _orchestrator.OnReactionAsync(entry, "👍", "reactor", "Reactor", "t1", "page-1");

        var notes = await GetNotificationsAsync("author");
        notes.Should().ContainSingle();
        notes[0].Type.Should().Be(TmNotificationTypes.Reaction);
    }

    private Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(string recipient)
        => _store.GetNotificationsAsync(new TmNotificationQuery { RecipientUserId = recipient });

    // ─── Fakes ──────────────────────────────────────────────────────────────

    private static TmCommentThread Thread(params TmCommentEntry[] entries)
        => new()
        {
            Id = "thread-1",
            EntityRef = TmEntityRef.Create("notion-page", "page-1"),
            Anchor = TmCommentAnchor.Block("block-1"),
            Entries = entries.ToList()
        };

    private static TmCommentEntry Entry(string userId, string name, string? parentEntryId = null)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = "thread-1",
            ParentEntryId = parentEntryId,
            Author = new TmUserRef { Id = userId, DisplayName = name, AvatarUrl = "" },
            Body = "<p>test</p>",
            BodyFormat = TmCommentBodyFormat.Html,
            CreatedAt = DateTimeOffset.UtcNow,
            CanEdit = true,
            CanDelete = true
        };
}
