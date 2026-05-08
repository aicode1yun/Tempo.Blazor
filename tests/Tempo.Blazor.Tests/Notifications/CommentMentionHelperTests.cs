using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Notifications;

public class CommentMentionHelperTests
{
    [Fact]
    public void ExtractUsernames_FindsUniqueNames()
    {
        var text = "Hey @alice and @bob, also @alice again.";
        var names = MentionParser.ExtractUsernames(text).ToList();
        names.Should().ContainInOrder("alice", "bob");
    }

    [Fact]
    public void ReplaceMentions_EncodesResolvedUsers()
    {
        var text = "Hello @alice and @unknown";
        var result = MentionParser.ReplaceMentions(text, u => u == "alice" ? "u1" : null);
        result.Should().Contain("<span class=\"tm-mention\" data-user-id=\"u1\">@alice</span>");
        result.Should().Contain("@unknown"); // unresolved stays plain
    }

    [Fact]
    public async Task EncodeAsync_ResolvesAndReplaces()
    {
        var provider = new FakeMentionProvider();
        var result = await CommentMentionHelper.EncodeAsync("Hi @alice", provider);
        result.Should().Contain("<span class=\"tm-mention\" data-user-id=\"alice\">@alice</span>");
    }

    [Fact]
    public async Task NotifyAsync_CreatesMentionNotification()
    {
        var store = new InMemoryNotificationStore();
        var orchestrator = new CommentNotificationOrchestrator(store);
        var provider = new FakeMentionProvider();
        var entry = new FakeEntry(Guid.NewGuid(), "author", "Author Name", null);

        await CommentMentionHelper.NotifyAsync("Hi @alice", entry, "t1", "page-1", provider, orchestrator);

        var notes = await store.GetNotificationsAsync("alice");
        notes.Should().ContainSingle();
        notes[0].Event.Type.Should().Be(NotificationType.Mention);
    }

    [Fact]
    public async Task NotifyAsync_DirectOrchestrator_Works()
    {
        var store = new InMemoryNotificationStore();
        var orchestrator = new CommentNotificationOrchestrator(store);
        var entry = new FakeEntry(Guid.NewGuid(), "author", "Author Name", null);

        await orchestrator.OnMentionAsync(entry, new[] { "alice" }, "t1", "page-1");

        var notes = await store.GetNotificationsAsync("alice");
        notes.Should().ContainSingle();
    }

    [Fact]
    public async Task NotifyAsync_SkipsSelfMention()
    {
        var store = new InMemoryNotificationStore();
        var orchestrator = new CommentNotificationOrchestrator(store);
        var provider = new FakeMentionProvider();
        var entry = new FakeEntry(Guid.NewGuid(), "alice", "Alice", null);

        await CommentMentionHelper.NotifyAsync("Hi @alice", entry, "t1", "page-1", provider, orchestrator);

        var notes = await store.GetNotificationsAsync("alice");
        notes.Should().BeEmpty();
    }

    // ─── Fakes ──────────────────────────────────────────────────────────────

    private class FakeMentionProvider : INotionMentionProvider
    {
        private readonly List<FakeUser> _users = new()
        {
            new FakeUser("u1", "alice", "Alice Johnson"),
            new FakeUser("u2", "bob", "Bob Smith"),
        };

        public Task<IEnumerable<IMentionUser>> SearchUsersAsync(string query)
        {
            var results = string.IsNullOrWhiteSpace(query)
                ? _users
                : _users.Where(u => u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(results.Cast<IMentionUser>());
        }

        public Task<IEnumerable<INotionPage>> SearchPagesAsync(string query) => Task.FromResult(Enumerable.Empty<INotionPage>());
    }

    private class FakeUser : IMentionUser
    {
        public string UserId { get; }
        public string DisplayName { get; }
        public string? AvatarUrl => null;
        public string? Email => null;

        public FakeUser(string id, string userId, string displayName)
        {
            UserId = userId;
            DisplayName = displayName;
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
        public Guid? ParentEntryId => null;
        public bool CanEdit => true;
        public bool CanDelete => true;
        public IReadOnlyList<ICommentReaction> Reactions => Array.Empty<ICommentReaction>();

        public FakeEntry(Guid id, string userId, string name, Guid? parentId)
        {
            Id = id;
            AuthorUserId = userId;
            AuthorDisplayName = name;
        }
    }
}
