using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Models;
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
        var thread = new FakeBlockComment(Guid.NewGuid(), new[]
        {
            new FakeEntry(Guid.NewGuid(), "parent", "Parent Author", null),
        });
        var parent = thread.Thread.First();
        var reply = new FakeEntry(Guid.NewGuid(), "replier", "Replier Name", parent.Id);

        await _orchestrator.OnNewReplyAsync(thread, reply);

        var notes = await _store.GetNotificationsAsync("parent");
        notes.Should().ContainSingle();
        notes[0].Event.Type.Should().Be(NotificationType.Reply);
        notes[0].Event.RecipientUserId.Should().Be("parent");
    }

    [Fact]
    public async Task OnNewReplyAsync_SkipsSelfReply()
    {
        var thread = new FakeBlockComment(Guid.NewGuid(), new[]
        {
            new FakeEntry(Guid.NewGuid(), "same", "Same User", null),
        });
        var parent = thread.Thread.First();
        var reply = new FakeEntry(Guid.NewGuid(), "same", "Same User", parent.Id);

        await _orchestrator.OnNewReplyAsync(thread, reply);

        var notes = await _store.GetNotificationsAsync("same");
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OnNewReplyAsync_SkipsWhenNoParent()
    {
        var thread = new FakeBlockComment(Guid.NewGuid(), Array.Empty<FakeEntry>());
        var reply = new FakeEntry(Guid.NewGuid(), "u1", "User", null);

        await _orchestrator.OnNewReplyAsync(thread, reply);

        var notes = await _store.GetNotificationsAsync("u1");
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task OnThreadResolvedAsync_NotifiesParticipants()
    {
        var thread = new FakeBlockComment(Guid.NewGuid(), new[]
        {
            new FakeEntry(Guid.NewGuid(), "alice", "Alice", null),
            new FakeEntry(Guid.NewGuid(), "bob", "Bob", null),
        });

        await _orchestrator.OnThreadResolvedAsync(thread, "alice", "Alice");

        var bobNotes = await _store.GetNotificationsAsync("bob");
        bobNotes.Should().ContainSingle();
        bobNotes[0].Event.Type.Should().Be(NotificationType.ThreadResolved);

        var aliceNotes = await _store.GetNotificationsAsync("alice");
        aliceNotes.Should().BeEmpty(); // resolver not notified
    }

    [Fact]
    public async Task OnMentionAsync_NotifiesMentionedUser()
    {
        var entry = new FakeEntry(Guid.NewGuid(), "author", "Author", null);

        await _orchestrator.OnMentionAsync(entry, new[] { "mentioned" }, "t1", "page-1");

        var notes = await _store.GetNotificationsAsync("mentioned");
        notes.Should().ContainSingle();
        notes[0].Event.Type.Should().Be(NotificationType.Mention);
    }

    [Fact]
    public async Task OnReactionAsync_NotifiesEntryAuthor()
    {
        var entry = new FakeEntry(Guid.NewGuid(), "author", "Author", null);

        await _orchestrator.OnReactionAsync(entry, "👍", "reactor", "Reactor", "t1", "page-1");

        var notes = await _store.GetNotificationsAsync("author");
        notes.Should().ContainSingle();
        notes[0].Event.Type.Should().Be(NotificationType.Reaction);
    }

    // ─── Fakes ──────────────────────────────────────────────────────────────

    private class FakeBlockComment : IBlockComment
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid BlockId { get; }
        public IReadOnlyList<INotionCommentEntry> Thread { get; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedByUserId { get; set; }
        public IReadOnlyList<string> ReadByUserIds { get; } = Array.Empty<string>();
        public DateTime? LastActivityAt { get; set; }
        public IReadOnlyList<string> SubscribedUserIds { get; } = Array.Empty<string>();

        public FakeBlockComment(Guid blockId, IEnumerable<FakeEntry> entries)
        {
            BlockId = blockId;
            Thread = entries.ToList();
        }
    }

    private class FakeEntry : INotionCommentEntry
    {
        public Guid Id { get; }
        public string AuthorUserId { get; }
        public string AuthorDisplayName { get; }
        public string? AuthorAvatarUrl => "";
        public string HtmlContent => "<p>test</p>";
        public DateTime CreatedAt => DateTime.UtcNow;
        public DateTime UpdatedAt => DateTime.UtcNow;
        public bool IsDeleted => false;
        public Guid? ParentEntryId { get; }
        public bool CanEdit => true;
        public bool CanDelete => true;
        public IReadOnlyList<ICommentReaction> Reactions => Array.Empty<ICommentReaction>();

        public FakeEntry(Guid id, string userId, string name, Guid? parentId)
        {
            Id = id;
            AuthorUserId = userId;
            AuthorDisplayName = name;
            ParentEntryId = parentId;
        }
    }
}
