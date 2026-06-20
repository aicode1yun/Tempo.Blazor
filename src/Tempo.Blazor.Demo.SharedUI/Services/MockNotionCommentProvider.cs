using Tempo.Blazor.NotionEditor.Helpers;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory scoped comment provider for the demo. Pre-seeded with two threads on the
/// "Getting Started" page. All mutation methods update in-memory state only.
/// </summary>
public class MockNotionCommentProvider : INotionCommentProvider
{
    // commentId → BlockComment
    private readonly Dictionary<Guid, BlockComment> _byId = new();
    // blockId or pageId → ordered list of comment IDs
    private readonly Dictionary<Guid, List<Guid>> _idx = new();

    // Fixed demo page IDs — duplicated from MockNotionDataStore (different assembly)
    private static readonly Guid _page1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _page2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly CommentNotificationOrchestrator? _orchestrator;

    public MockNotionCommentProvider(INotificationService? notificationService = null)
    {
        _orchestrator = notificationService is not null
            ? new CommentNotificationOrchestrator(notificationService)
            : null;
        Seed();
    }

    // ── Seeding ───────────────────────────────────────────────────────────────

    private void Seed()
    {
        var pageId = _page1Id;

        // Thread 1 — resolved (Alice → Bob)
        var c1e1 = MakeEntry("alice",   "Alice Johnson", "https://i.pravatar.cc/150?u=alice",
            "Great overview! Should we add a keyboard shortcut cheat sheet?",
            DateTime.UtcNow.AddDays(-3));
        var c1e2 = MakeEntry("bob",     "Bob Smith",     "https://i.pravatar.cc/150?u=bob",
            "Good idea — added a <strong>Keyboard Shortcuts</strong> callout below 👍",
            DateTime.UtcNow.AddDays(-2));
        c1e1.Reactions.Add(new CommentReaction { Emoji = "👍", UserIds = new() { "bob", "charlie" } });
        c1e1.Reactions.Add(new CommentReaction { Emoji = "🔥", UserIds = new() { "diana" } });
        var c1 = NewComment(pageId, c1e1, c1e2);
        c1.IsResolved       = true;
        c1.ResolvedAt       = DateTime.UtcNow.AddDays(-2).AddHours(1);
        c1.ResolvedByUserId = "bob";
        Register(pageId, c1);

        // Thread 2 — open (Charlie)
        var c2e1 = MakeEntry("charlie", "Charlie Brown", null,
            "Can we add a <em>board view</em> database demo in a separate sub-page?",
            DateTime.UtcNow.AddHours(-6));
        c2e1.Reactions.Add(new CommentReaction { Emoji = "👍", UserIds = new() { "bob" } });
        var c2 = NewComment(pageId, c2e1);
        Register(pageId, c2);

        // Page 2 — one open comment
        var c3 = NewComment(_page2Id,
            MakeEntry("diana", "Diana Prince", "https://i.pravatar.cc/150?u=diana",
                "Should the Q2 items be moved into a separate database for tracking?",
                DateTime.UtcNow.AddHours(-3))
        );
        Register(_page2Id, c3);
    }

    private static BlockComment NewComment(Guid ownerId, params NotionCommentEntry[] entries) => new()
    {
        Id      = Guid.NewGuid(),
        BlockId = ownerId,
        Thread  = new List<INotionCommentEntry>(entries),
        LastActivityAt = entries.Length > 0 ? entries.Max(e => e.CreatedAt) : DateTime.UtcNow
    };

    private static NotionCommentEntry MakeEntry(string userId, string name, string? avatar, string html, DateTime at, Guid? parentEntryId = null) => new()
    {
        Id                = Guid.NewGuid(),
        ParentEntryId     = parentEntryId,
        AuthorUserId      = userId,
        AuthorDisplayName = name,
        AuthorAvatarUrl   = avatar,
        HtmlContent       = html,
        CreatedAt         = at,
        UpdatedAt         = at,
        CanEdit           = false,
        CanDelete         = false,
        Reactions         = new()
    };

    private void Register(Guid ownerId, BlockComment comment)
    {
        _byId[comment.Id] = comment;
        if (!_idx.TryGetValue(ownerId, out var list)) _idx[ownerId] = list = new();
        list.Add(comment.Id);
    }

    // ── INotionCommentProvider ────────────────────────────────────────────────

    public Task<IEnumerable<IBlockComment>> GetBlockCommentsAsync(string blockId)
    {
        var id  = Parse(blockId);
        var res = _idx.TryGetValue(id, out var ids)
            ? ids.Where(_byId.ContainsKey).Select(x => (IBlockComment)_byId[x])
            : Enumerable.Empty<IBlockComment>();
        return Task.FromResult(res);
    }

    public Task<IBlockComment> AddBlockCommentAsync(string blockId, string htmlContent)
    {
        var id = Parse(blockId);
        var entry = DemoEntry(htmlContent);
        var c  = NewComment(id, entry);
        c.SubscribedUserIds.Add(entry.AuthorUserId);
        Register(id, c);
        return Task.FromResult<IBlockComment>(c);
    }

    public async Task<INotionCommentEntry> ReplyToCommentAsync(string commentId, string htmlContent, string? parentEntryId = null)
    {
        var c = Require(commentId);
        var e = DemoEntry(htmlContent);
        if (!string.IsNullOrEmpty(parentEntryId))
            e.ParentEntryId = Parse(parentEntryId);
        ((List<INotionCommentEntry>)c.Thread).Add(e);
        c.LastActivityAt = DateTime.UtcNow;
        if (!c.SubscribedUserIds.Contains(e.AuthorUserId))
            c.SubscribedUserIds.Add(e.AuthorUserId);

        if (_orchestrator is not null)
            await _orchestrator.OnNewReplyAsync(c, e);

        return e;
    }

    public Task<INotionCommentEntry> EditCommentAsync(string entryId, string htmlContent)
    {
        var eid = Parse(entryId);
        foreach (var c in _byId.Values)
            foreach (var e in c.Thread.OfType<NotionCommentEntry>())
                if (e.Id == eid)
                {
                    e.HtmlContent = htmlContent;
                    e.UpdatedAt   = DateTime.UtcNow;
                    return Task.FromResult<INotionCommentEntry>(e);
                }
        throw new KeyNotFoundException(entryId);
    }

    public Task DeleteCommentAsync(string commentId)
    {
        var cid = Parse(commentId);
        if (_byId.TryGetValue(cid, out var c))
        {
            _byId.Remove(cid);
            if (_idx.TryGetValue(c.BlockId, out var list)) list.Remove(cid);
        }
        return Task.CompletedTask;
    }

    public Task DeleteCommentEntryAsync(string entryId)
    {
        var eid = Parse(entryId);
        foreach (var c in _byId.Values)
        {
            var thread = c.Thread as List<INotionCommentEntry>;
            if (thread is null) continue;
            for (int i = 0; i < thread.Count; i++)
            {
                if (thread[i].Id == eid)
                {
                    thread.RemoveAt(i);
                    return Task.CompletedTask;
                }
            }
        }
        return Task.CompletedTask;
    }

    public async Task<IBlockComment> ResolveCommentAsync(string commentId)
    {
        var c = Require(commentId);
        c.IsResolved       = true;
        c.ResolvedAt       = DateTime.UtcNow;
        c.ResolvedByUserId = "demo";
        c.LastActivityAt   = DateTime.UtcNow;

        if (_orchestrator is not null)
            await _orchestrator.OnThreadResolvedAsync(c, "demo", "Demo User");

        return c;
    }

    public Task<IBlockComment> UnresolveCommentAsync(string commentId)
    {
        var c = Require(commentId);
        c.IsResolved       = false;
        c.ResolvedAt       = null;
        c.ResolvedByUserId = null;
        return Task.FromResult<IBlockComment>(c);
    }

    public Task<IBlockComment> AddTextAnchorCommentAsync(
        string blockId, int _startOffset, int _endOffset, string _highlightedText, string htmlContent, string commentId)
    {
        var ownerId = Parse(blockId);
        var cid     = Parse(commentId);
        var entry = DemoEntry(htmlContent);
        var c = new BlockComment
        {
            Id      = cid,
            BlockId = ownerId,
            Thread  = new List<INotionCommentEntry> { entry }
        };
        c.SubscribedUserIds.Add(entry.AuthorUserId);
        Register(ownerId, c);
        return Task.FromResult<IBlockComment>(c);
    }

    public async Task<IEnumerable<IPageComment>> GetPageCommentsAsync(string pageId)
    {
        var comments = await GetBlockCommentsAsync(pageId);
        return comments.Select(comment => new PageComment
        {
            Id = comment.Id,
            BlockId = comment.BlockId,
            PageId = pageId,
            Thread = comment.Thread,
            IsResolved = comment.IsResolved,
            ResolvedAt = comment.ResolvedAt,
            ResolvedByUserId = comment.ResolvedByUserId,
            LastActivityAt = comment.LastActivityAt,
            ReadByUserIds = comment.ReadByUserIds.ToList(),
            SubscribedUserIds = comment.SubscribedUserIds.ToList()
        }).ToArray();
    }

    public async Task<IPageComment> AddPageCommentAsync(string pageId, string htmlContent)
    {
        var comment = await AddBlockCommentAsync(pageId, htmlContent);
        return new PageComment
        {
            Id = comment.Id,
            BlockId = comment.BlockId,
            PageId = pageId,
            Thread = comment.Thread,
            IsResolved = comment.IsResolved,
            ResolvedAt = comment.ResolvedAt,
            ResolvedByUserId = comment.ResolvedByUserId,
            LastActivityAt = comment.LastActivityAt,
            ReadByUserIds = comment.ReadByUserIds.ToList(),
            SubscribedUserIds = comment.SubscribedUserIds.ToList()
        };
    }

    public Task<int> GetUnresolvedCommentsCountAsync(string pageId)
    {
        var pid   = Parse(pageId);
        var count = _idx.TryGetValue(pid, out var ids)
            ? ids.Where(_byId.ContainsKey).Count(x => !_byId[x].IsResolved)
            : 0;
        return Task.FromResult(count);
    }

    public Task MarkThreadAsReadAsync(string commentId, string userId)
    {
        var c = Require(commentId);
        if (!c.ReadByUserIds.Contains(userId))
            c.ReadByUserIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task MarkThreadAsUnreadAsync(string commentId, string userId)
    {
        var c = Require(commentId);
        c.ReadByUserIds.Remove(userId);
        return Task.CompletedTask;
    }

    public Task MarkAllThreadsAsReadAsync(string ownerId, string userId)
    {
        var oid = Parse(ownerId);
        if (_idx.TryGetValue(oid, out var ids))
        {
            foreach (var id in ids)
            {
                if (_byId.TryGetValue(id, out var c) && !c.ReadByUserIds.Contains(userId))
                    c.ReadByUserIds.Add(userId);
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ICommentReaction>> GetReactionsAsync(string entryId)
    {
        var eid = Parse(entryId);
        foreach (var c in _byId.Values)
            foreach (var e in c.Thread.OfType<NotionCommentEntry>())
                if (e.Id == eid)
                    return Task.FromResult<IReadOnlyList<ICommentReaction>>(e.Reactions);
        return Task.FromResult<IReadOnlyList<ICommentReaction>>(Array.Empty<ICommentReaction>());
    }

    public async Task AddReactionAsync(string entryId, string emoji, string userId)
    {
        var eid = Parse(entryId);
        foreach (var c in _byId.Values)
            foreach (var e in c.Thread.OfType<NotionCommentEntry>())
                if (e.Id == eid)
                {
                    var r = e.Reactions.OfType<CommentReaction>().FirstOrDefault(x => x.Emoji == emoji);
                    if (r is null)
                    {
                        r = new CommentReaction { Emoji = emoji };
                        e.Reactions.Add(r);
                    }
                    if (!r.UserIds.Contains(userId))
                        r.UserIds.Add(userId);

                    c.LastActivityAt = DateTime.UtcNow;
                    if (!c.SubscribedUserIds.Contains(userId))
                        c.SubscribedUserIds.Add(userId);

                    if (_orchestrator is not null)
                        await _orchestrator.OnReactionAsync(e, emoji, userId, "Demo User", c.Id.ToString(), c.BlockId.ToString());
                    return;
                }
        throw new KeyNotFoundException(entryId);
    }

    public Task RemoveReactionAsync(string entryId, string emoji, string userId)
    {
        var eid = Parse(entryId);
        foreach (var c in _byId.Values)
            foreach (var e in c.Thread.OfType<NotionCommentEntry>())
                if (e.Id == eid)
                {
                    var r = e.Reactions.OfType<CommentReaction>().FirstOrDefault(x => x.Emoji == emoji);
                    if (r is not null)
                    {
                        r.UserIds.Remove(userId);
                        if (r.UserIds.Count == 0)
                            e.Reactions.Remove(r);
                    }
                    c.LastActivityAt = DateTime.UtcNow;
                    return Task.CompletedTask;
                }
        throw new KeyNotFoundException(entryId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private BlockComment Require(string id)
        => _byId.TryGetValue(Parse(id), out var c) ? c : throw new KeyNotFoundException(id);

    private static Guid Parse(string id) => Guid.TryParse(id, out var g) ? g : Guid.Empty;

    public Task SubscribeToThreadAsync(string commentId, string userId)
    {
        var c = Require(commentId);
        if (!c.SubscribedUserIds.Contains(userId))
            c.SubscribedUserIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task UnsubscribeFromThreadAsync(string commentId, string userId)
    {
        var c = Require(commentId);
        c.SubscribedUserIds.Remove(userId);
        return Task.CompletedTask;
    }

    private static NotionCommentEntry DemoEntry(string html, Guid? parentEntryId = null) => new()
    {
        Id                = Guid.NewGuid(),
        ParentEntryId     = parentEntryId,
        AuthorUserId      = "demo",
        AuthorDisplayName = "Demo User",
        HtmlContent       = html,
        CreatedAt         = DateTime.UtcNow,
        UpdatedAt         = DateTime.UtcNow,
        CanEdit           = true,
        CanDelete         = true,
        Reactions         = new()
    };
}
