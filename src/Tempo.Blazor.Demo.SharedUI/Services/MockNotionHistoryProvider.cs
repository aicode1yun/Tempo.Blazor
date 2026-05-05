using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory scoped history provider. Pre-seeds three versions for "Getting Started".
/// RestoreVersionAsync inserts a new "restored" version at the top and is a no-op for
/// the live block store (acceptable in a demo context).
/// </summary>
public class MockNotionHistoryProvider : INotionHistoryProvider
{
    private static readonly Guid _page1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _page2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // pageId → versions ordered newest-first
    private readonly Dictionary<Guid, List<PageVersion>> _store = new();

    public MockNotionHistoryProvider() => Seed();

    // ── Seeding ───────────────────────────────────────────────────────────────

    private void Seed()
    {
        var pid = _page1Id;

        _store[pid] = new List<PageVersion>
        {
            new()
            {
                Id                  = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
                PageId              = pid,
                EditedAt            = DateTime.UtcNow.AddHours(-2),
                EditedByUserId      = "demo",
                EditedByDisplayName = "Demo User",
                ChangeDescription   = "Added Phase 3 inline database section",
                BlocksSnapshot      = Snapshot(pid,
                    (BlockType.Heading1,   "Welcome to Notion Editor"),
                    (BlockType.Paragraph,  "A comprehensive block-based editor with full keyboard support and collaborative features."),
                    (BlockType.Heading2,   "Features"),
                    (BlockType.BulletList, "Slash menu with /"),
                    (BlockType.BulletList, "Drag & drop reordering"),
                    (BlockType.BulletList, "Inline toolbar on text selection"),
                    (BlockType.Heading2,   "Phase 2: Media Blocks"),
                    (BlockType.Paragraph,  "Image, video, file, code, and callout blocks."),
                    (BlockType.Heading2,   "Phase 3: Inline Database"),
                    (BlockType.Paragraph,  "Full database with 6 view types and 8 fields."))
            },
            new()
            {
                Id                  = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
                PageId              = pid,
                EditedAt            = DateTime.UtcNow.AddDays(-1),
                EditedByUserId      = "bob",
                EditedByDisplayName = "Bob Smith",
                ChangeDescription   = "Added media blocks and code examples",
                BlocksSnapshot      = Snapshot(pid,
                    (BlockType.Heading1,   "Welcome to Notion Editor"),
                    (BlockType.Paragraph,  "A block-based editor with keyboard support."),
                    (BlockType.Heading2,   "Features"),
                    (BlockType.BulletList, "Slash menu with /"),
                    (BlockType.BulletList, "Drag & drop reordering"),
                    (BlockType.Heading2,   "Phase 2: Media Blocks"),
                    (BlockType.Paragraph,  "Image, video, and file blocks added."),
                    (BlockType.BulletList, "Code block with syntax highlighting"))
            },
            new()
            {
                Id                  = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                PageId              = pid,
                EditedAt            = DateTime.UtcNow.AddDays(-3),
                EditedByUserId      = "alice",
                EditedByDisplayName = "Alice Johnson",
                ChangeDescription   = "Initial draft",
                BlocksSnapshot      = Snapshot(pid,
                    (BlockType.Heading1,   "Welcome to Notion Editor"),
                    (BlockType.Paragraph,  "This is the initial draft of the editor documentation."),
                    (BlockType.Heading2,   "Getting Started"),
                    (BlockType.BulletList, "Open the slash menu with /"),
                    (BlockType.BulletList, "Press Enter to create a new block"))
            }
        };

        // Seed minimal history for Page 2 as well
        _store[_page2Id] = new List<PageVersion>
        {
            new()
            {
                Id                  = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                PageId              = _page2Id,
                EditedAt            = DateTime.UtcNow.AddDays(-1),
                EditedByUserId      = "alice",
                EditedByDisplayName = "Alice Johnson",
                ChangeDescription   = "Added Q2 items",
                BlocksSnapshot      = Snapshot(_page2Id,
                    (BlockType.Heading1,   "Product Roadmap"),
                    (BlockType.Heading2,   "Q1 2025 — In Progress"),
                    (BlockType.TodoItem,   "Phase 4 providers"))
            }
        };
    }

    private static IReadOnlyList<IPageBlock> Snapshot(Guid pageId, params (BlockType Type, string Html)[] items)
        => items.Select((x, i) => (IPageBlock)new PageBlock
        {
            Id           = Guid.NewGuid(),
            PageId       = pageId,
            Type         = x.Type,
            Order        = i,
            Content      = x.Type switch
            {
                BlockType.Heading1   => new HeadingBlockContent { Level = 1, Html = x.Html },
                BlockType.Heading2   => new HeadingBlockContent { Level = 2, Html = x.Html },
                BlockType.BulletList => new ListBlockContent    { Html = x.Html },
                BlockType.TodoItem   => new TodoBlockContent    { Html = x.Html },
                _                   => new TextBlockContent    { Html = x.Html }
            },
            CreatedAt    = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        }).ToList();

    // ── INotionHistoryProvider ────────────────────────────────────────────────

    public Task<PagedResult<IPageVersion>> GetVersionsAsync(string pageId, int page, int pageSize)
    {
        var pid  = Guid.Parse(pageId);
        var all  = _store.TryGetValue(pid, out var vs) ? vs : new List<PageVersion>();
        var skip = (page - 1) * pageSize;
        return Task.FromResult(new PagedResult<IPageVersion>
        {
            Items      = all.Skip(skip).Take(pageSize).Cast<IPageVersion>().ToList(),
            TotalCount = all.Count,
            Page       = page,
            PageSize   = pageSize
        });
    }

    public Task<IPageVersion> GetVersionAsync(string pageId, string versionId)
    {
        var pid = Guid.Parse(pageId);
        var vid = Guid.Parse(versionId);
        var v   = _store.TryGetValue(pid, out var list)
            ? list.FirstOrDefault(x => x.Id == vid) : null;
        return v is not null
            ? Task.FromResult<IPageVersion>(v)
            : Task.FromException<IPageVersion>(new KeyNotFoundException(versionId));
    }

    public Task RestoreVersionAsync(string pageId, string versionId)
    {
        var pid    = Guid.Parse(pageId);
        var vid    = Guid.Parse(versionId);
        if (!_store.TryGetValue(pid, out var list)) return Task.CompletedTask;

        var target = list.FirstOrDefault(v => v.Id == vid);
        if (target is null) return Task.CompletedTask;

        var restored = new PageVersion
        {
            Id                  = Guid.NewGuid(),
            PageId              = pid,
            EditedAt            = DateTime.UtcNow,
            EditedByUserId      = "demo",
            EditedByDisplayName = "Demo User",
            ChangeDescription   = $"Restored version from {target.EditedAt:g}",
            BlocksSnapshot      = target.BlocksSnapshot
        };
        list.Insert(0, restored);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<BlockDiff>> CompareVersionsAsync(string versionId1, string versionId2)
    {
        var v1 = FindVersion(Guid.Parse(versionId1));
        var v2 = FindVersion(Guid.Parse(versionId2));
        if (v1 is null || v2 is null)
            return Task.FromResult(Enumerable.Empty<BlockDiff>());

        var before = v1.BlocksSnapshot.ToDictionary(b => b.Id.ToString());
        var after  = v2.BlocksSnapshot.ToDictionary(b => b.Id.ToString());
        var diffs  = new List<BlockDiff>();

        foreach (var (id, b) in before)
            diffs.Add(after.TryGetValue(id, out var a)
                ? new BlockDiff(id, BlockDiffType.Modified, b, a)
                : new BlockDiff(id, BlockDiffType.Removed,  b, null));

        foreach (var (id, a) in after)
            if (!before.ContainsKey(id))
                diffs.Add(new BlockDiff(id, BlockDiffType.Added, null, a));

        return Task.FromResult<IEnumerable<BlockDiff>>(diffs);
    }

    private PageVersion? FindVersion(Guid versionId)
        => _store.Values.SelectMany(x => x).FirstOrDefault(v => v.Id == versionId);
}
