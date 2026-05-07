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

    public MockNotionCommentProvider() => Seed();

    // ── Seeding ───────────────────────────────────────────────────────────────

    private void Seed()
    {
        var pageId = _page1Id;

        // Thread 1 — resolved (Alice → Bob)
        var c1 = NewComment(pageId,
            MakeEntry("alice",   "Alice Johnson", "https://i.pravatar.cc/150?u=alice",
                "Great overview! Should we add a keyboard shortcut cheat sheet?",
                DateTime.UtcNow.AddDays(-3)),
            MakeEntry("bob",     "Bob Smith",     "https://i.pravatar.cc/150?u=bob",
                "Good idea — added a <strong>Keyboard Shortcuts</strong> callout below 👍",
                DateTime.UtcNow.AddDays(-2))
        );
        c1.IsResolved       = true;
        c1.ResolvedAt       = DateTime.UtcNow.AddDays(-2).AddHours(1);
        c1.ResolvedByUserId = "bob";
        Register(pageId, c1);

        // Thread 2 — open (Charlie)
        var c2 = NewComment(pageId,
            MakeEntry("charlie", "Charlie Brown", null,
                "Can we add a <em>board view</em> database demo in a separate sub-page?",
                DateTime.UtcNow.AddHours(-6))
        );
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
        Thread  = new List<INotionCommentEntry>(entries)
    };

    private static NotionCommentEntry MakeEntry(string userId, string name, string? avatar, string html, DateTime at) => new()
    {
        Id                = Guid.NewGuid(),
        AuthorUserId      = userId,
        AuthorDisplayName = name,
        AuthorAvatarUrl   = avatar,
        HtmlContent       = html,
        CreatedAt         = at,
        UpdatedAt         = at,
        CanEdit           = false,
        CanDelete         = false
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
        var c  = NewComment(id, DemoEntry(htmlContent));
        Register(id, c);
        return Task.FromResult<IBlockComment>(c);
    }

    public Task<INotionCommentEntry> ReplyToCommentAsync(string commentId, string htmlContent)
    {
        var c = Require(commentId);
        var e = DemoEntry(htmlContent);
        ((List<INotionCommentEntry>)c.Thread).Add(e);
        return Task.FromResult<INotionCommentEntry>(e);
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

    public Task<IBlockComment> ResolveCommentAsync(string commentId)
    {
        var c = Require(commentId);
        c.IsResolved       = true;
        c.ResolvedAt       = DateTime.UtcNow;
        c.ResolvedByUserId = "demo";
        return Task.FromResult<IBlockComment>(c);
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
        var c = new BlockComment
        {
            Id      = cid,
            BlockId = ownerId,
            Thread  = new List<INotionCommentEntry> { DemoEntry(htmlContent) }
        };
        Register(ownerId, c);
        return Task.FromResult<IBlockComment>(c);
    }

    public Task<IEnumerable<IBlockComment>> GetPageCommentsAsync(string pageId)
        => GetBlockCommentsAsync(pageId);

    public Task<IBlockComment> AddPageCommentAsync(string pageId, string htmlContent)
        => AddBlockCommentAsync(pageId, htmlContent);

    public Task<int> GetUnresolvedCommentsCountAsync(string pageId)
    {
        var pid   = Parse(pageId);
        var count = _idx.TryGetValue(pid, out var ids)
            ? ids.Where(_byId.ContainsKey).Count(x => !_byId[x].IsResolved)
            : 0;
        return Task.FromResult(count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private BlockComment Require(string id)
        => _byId.TryGetValue(Parse(id), out var c) ? c : throw new KeyNotFoundException(id);

    private static Guid Parse(string id) => Guid.TryParse(id, out var g) ? g : Guid.Empty;

    private static NotionCommentEntry DemoEntry(string html) => new()
    {
        Id                = Guid.NewGuid(),
        AuthorUserId      = "demo",
        AuthorDisplayName = "Demo User",
        HtmlContent       = html,
        CreatedAt         = DateTime.UtcNow,
        UpdatedAt         = DateTime.UtcNow,
        CanEdit           = true,
        CanDelete         = true
    };
}
