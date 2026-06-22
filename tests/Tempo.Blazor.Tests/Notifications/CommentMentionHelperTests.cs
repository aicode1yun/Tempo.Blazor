using FluentAssertions;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Helpers;
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
        var entry = Entry("author", "Author Name");

        await CommentMentionHelper.NotifyAsync("Hi @alice", entry, "t1", "page-1", provider, orchestrator);

        var notes = await store.GetNotificationsAsync(new TmNotificationQuery { RecipientUserId = "alice" });
        notes.Should().ContainSingle();
        notes[0].Type.Should().Be(TmNotificationTypes.Mention);
    }

    [Fact]
    public async Task NotifyAsync_DirectOrchestrator_Works()
    {
        var store = new InMemoryNotificationStore();
        var orchestrator = new CommentNotificationOrchestrator(store);
        var entry = Entry("author", "Author Name");

        await orchestrator.OnMentionAsync(entry, new[] { "alice" }, "t1", "page-1");

        var notes = await store.GetNotificationsAsync(new TmNotificationQuery { RecipientUserId = "alice" });
        notes.Should().ContainSingle();
    }

    [Fact]
    public async Task NotifyAsync_SkipsSelfMention()
    {
        var store = new InMemoryNotificationStore();
        var orchestrator = new CommentNotificationOrchestrator(store);
        var provider = new FakeMentionProvider();
        var entry = Entry("alice", "Alice");

        await CommentMentionHelper.NotifyAsync("Hi @alice", entry, "t1", "page-1", provider, orchestrator);

        var notes = await store.GetNotificationsAsync(new TmNotificationQuery { RecipientUserId = "alice" });
        notes.Should().BeEmpty();
    }

    // ─── Fakes ──────────────────────────────────────────────────────────────

    private class FakeMentionProvider : TmPeopleProviderBase
    {
        private readonly List<TmUser> _users =
        [
            new() { Id = "alice", UserName = "alice", DisplayName = "Alice Johnson" },
            new() { Id = "bob", UserName = "bob", DisplayName = "Bob Smith" },
        ];

        public override Task<IReadOnlyList<TmUser>> SearchAsync(TmPeopleQuery query, CancellationToken cancellationToken = default)
        {
            var searchText = query.SearchText ?? string.Empty;
            IEnumerable<TmUser> results = string.IsNullOrWhiteSpace(searchText)
                ? _users
                : _users.Where(u =>
                    u.Id.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    u.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            if (query.Ids.Count > 0)
            {
                var ids = query.Ids.ToHashSet(StringComparer.Ordinal);
                results = results.Where(user => ids.Contains(user.Id));
            }

            return Task.FromResult<IReadOnlyList<TmUser>>(results.ToArray());
        }
    }

    private static TmCommentEntry Entry(string userId, string name)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = "t1",
            Author = new TmUserRef { Id = userId, DisplayName = name },
            Body = "<p>test</p>",
            BodyFormat = TmCommentBodyFormat.Html,
            CreatedAt = DateTimeOffset.UtcNow,
            CanEdit = true,
            CanDelete = true
        };
}
