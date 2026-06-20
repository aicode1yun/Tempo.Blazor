using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Services;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionHistoryStore
{
    private static readonly DateTime SeedNow = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
    private readonly Dictionary<Guid, List<NotionPageVersionDto>> _versionsByPage = new();

    public DemoNotionHistoryStore() => Reset();

    public void Reset()
    {
        _versionsByPage.Clear();
        SeedDefaultHistory();
    }

    public void SeedEmptyHistory()
    {
        _versionsByPage[MockNotionDataStore.Page1Id] = [];
    }

    public void SeedManyHistory()
    {
        var pageId = MockNotionDataStore.Page1Id;
        var versions = new List<NotionPageVersionDto>();

        for (var index = 0; index < 46; index++)
        {
            var versionNumber = 46 - index;
            versions.Add(new NotionPageVersionDto
            {
                Id = Guid.Parse($"eb130000-0000-0000-0000-{(1000 + index):000000000000}"),
                PageId = pageId,
                EditedAt = SeedNow.AddHours(-index * 3),
                EditedByUserId = (index % 3) switch
                {
                    0 => "ava",
                    1 => "ben",
                    _ => "clara"
                },
                EditedByDisplayName = (index % 3) switch
                {
                    0 => "Ava Novak",
                    1 => "Ben Smith",
                    _ => "Clara Dvorak"
                },
                ChangeDescription = $"History checkpoint {versionNumber:00}",
                BlocksSnapshot = Snapshot(pageId, versionNumber)
            });
        }

        _versionsByPage[pageId] = versions;
    }

    public PagedResult<NotionPageVersionDto> GetVersions(string pageId, int page, int pageSize)
    {
        var pageGuid = Guid.Parse(pageId);
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var all = _versionsByPage.TryGetValue(pageGuid, out var versions) ? versions : [];

        return new PagedResult<NotionPageVersionDto>
        {
            Items = all.Skip((safePage - 1) * safePageSize).Take(safePageSize).Select(CloneVersion).ToList(),
            TotalCount = all.Count,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public NotionPageVersionDto? GetVersion(string pageId, string versionId)
    {
        var pageGuid = Guid.Parse(pageId);
        var versionGuid = Guid.Parse(versionId);
        return _versionsByPage.TryGetValue(pageGuid, out var versions)
            ? versions.FirstOrDefault(version => version.Id == versionGuid) is { } found ? CloneVersion(found) : null
            : null;
    }

    public NotionPageVersionDto? FindVersion(string versionId)
    {
        var versionGuid = Guid.Parse(versionId);
        var found = _versionsByPage.Values.SelectMany(version => version).FirstOrDefault(version => version.Id == versionGuid);
        return found is null ? null : CloneVersion(found);
    }

    public IReadOnlyList<BlockDiff>? GetDiff(string pageId, string versionIdA, string versionIdB)
    {
        var before = GetVersion(pageId, versionIdA);
        var after = GetVersion(pageId, versionIdB);
        if (before is null || after is null)
            return null;

        return NotionBlockDiffService.Compare(before.BlocksSnapshot, after.BlocksSnapshot);
    }

    public void SeedDiffHistory()
    {
        var pageId = MockNotionDataStore.Page1Id;
        _versionsByPage[pageId] =
        [
            new()
            {
                Id = Guid.Parse("cf230000-0000-0000-0000-000000000003"),
                PageId = pageId,
                EditedAt = SeedNow,
                EditedByUserId = "mira",
                EditedByDisplayName = "Mira Novak",
                ChangeDescription = "Published comparison-ready version",
                BlocksSnapshot = DiffSnapshot(pageId, 3)
            },
            new()
            {
                Id = Guid.Parse("cf230000-0000-0000-0000-000000000002"),
                PageId = pageId,
                EditedAt = SeedNow.AddHours(-4),
                EditedByUserId = "bob",
                EditedByDisplayName = "Bob Smith",
                ChangeDescription = "Moved rollout notes and updated body",
                BlocksSnapshot = DiffSnapshot(pageId, 2)
            },
            new()
            {
                Id = Guid.Parse("cf230000-0000-0000-0000-000000000001"),
                PageId = pageId,
                EditedAt = SeedNow.AddDays(-1),
                EditedByUserId = "alice",
                EditedByDisplayName = "Alice Johnson",
                ChangeDescription = "Initial comparison baseline",
                BlocksSnapshot = DiffSnapshot(pageId, 1)
            },
            new()
            {
                Id = Guid.Parse("cf230000-0000-0000-0000-000000000004"),
                PageId = pageId,
                EditedAt = SeedNow.AddDays(-2),
                EditedByUserId = "alice",
                EditedByDisplayName = "Alice Johnson",
                ChangeDescription = "Identical baseline copy",
                BlocksSnapshot = DiffSnapshot(pageId, 1)
            }
        ];
    }

    public IReadOnlyList<PageBlock>? RestoreVersion(string pageId, string versionId)
    {
        var pageGuid = Guid.Parse(pageId);
        var versionGuid = Guid.Parse(versionId);
        if (!_versionsByPage.TryGetValue(pageGuid, out var versions))
            return null;

        var target = versions.FirstOrDefault(version => version.Id == versionGuid);
        if (target is null)
            return null;

        var restored = CloneVersion(target);
        restored.Id = Guid.NewGuid();
        restored.EditedAt = DateTime.UtcNow;
        restored.EditedByUserId = "demo";
        restored.EditedByDisplayName = "Demo User";
        restored.ChangeDescription = $"Restored {target.ChangeDescription}";
        versions.Insert(0, restored);

        return target.BlocksSnapshot.Select(CloneBlock).ToList();
    }

    private void SeedDefaultHistory()
    {
        var pageId = MockNotionDataStore.Page1Id;
        _versionsByPage[pageId] =
        [
            new()
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
                PageId = pageId,
                EditedAt = SeedNow.AddHours(-2),
                EditedByUserId = "demo",
                EditedByDisplayName = "Demo User",
                ChangeDescription = "Added Phase 3 inline database section",
                BlocksSnapshot = Snapshot(pageId, 3)
            },
            new()
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
                PageId = pageId,
                EditedAt = SeedNow.AddDays(-1),
                EditedByUserId = "bob",
                EditedByDisplayName = "Bob Smith",
                ChangeDescription = "Added media blocks and code examples",
                BlocksSnapshot = Snapshot(pageId, 2)
            },
            new()
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                PageId = pageId,
                EditedAt = SeedNow.AddDays(-3),
                EditedByUserId = "alice",
                EditedByDisplayName = "Alice Johnson",
                ChangeDescription = "Initial draft",
                BlocksSnapshot = Snapshot(pageId, 1)
            }
        ];

        _versionsByPage[MockNotionDataStore.Page2Id] =
        [
            new()
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                PageId = MockNotionDataStore.Page2Id,
                EditedAt = SeedNow.AddDays(-1),
                EditedByUserId = "alice",
                EditedByDisplayName = "Alice Johnson",
                ChangeDescription = "Added Q2 items",
                BlocksSnapshot = Snapshot(MockNotionDataStore.Page2Id, 7)
            }
        ];
    }

    private static List<PageBlock> Snapshot(Guid pageId, int versionNumber)
    {
        var createdAt = SeedNow.AddDays(-versionNumber);
        return
        [
            CreateBlock(pageId, versionNumber, 1, BlockType.Heading1, new HeadingBlockContent
            {
                Level = 1,
                Html = $"EB13 History Version {versionNumber:00}"
            }, createdAt),
            CreateBlock(pageId, versionNumber, 2, BlockType.Paragraph, new TextBlockContent
            {
                Html = $"Restorable body snapshot for version {versionNumber:00} stored by the HTTPS Demo API."
            }, createdAt),
            CreateBlock(pageId, versionNumber, 3, BlockType.Callout, new CalloutBlockContent
            {
                IconEmoji = "i",
                Html = $"Checkpoint {versionNumber:00} keeps a deterministic preview for screenshot review.",
                BackgroundColor = versionNumber % 2 == 0 ? "blue" : "gray"
            }, createdAt)
        ];
    }

    private static List<PageBlock> DiffSnapshot(Guid pageId, int versionNumber)
    {
        var createdAt = SeedNow.AddDays(-7);
        var headingId = Guid.Parse("cf230001-0000-0000-0000-000000000001");
        var bodyId = Guid.Parse("cf230001-0000-0000-0000-000000000002");
        var movedId = Guid.Parse("cf230001-0000-0000-0000-000000000003");
        var removedId = Guid.Parse("cf230001-0000-0000-0000-000000000004");
        var addedId = Guid.Parse("cf230001-0000-0000-0000-000000000005");
        var footerId = Guid.Parse("cf230001-0000-0000-0000-000000000006");

        return versionNumber switch
        {
            1 =>
            [
                CreateStableBlock(pageId, headingId, 0, BlockType.Heading1, new HeadingBlockContent { Level = 1, Html = "CF23 Page Diff Baseline" }, createdAt),
                CreateStableBlock(pageId, bodyId, 1, BlockType.Paragraph, new TextBlockContent { Html = "Original launch narrative with unchanged planning notes." }, createdAt),
                CreateStableBlock(pageId, movedId, 2, BlockType.Heading2, new HeadingBlockContent { Level = 2, Html = "Rollout notes" }, createdAt),
                CreateStableBlock(pageId, removedId, 3, BlockType.Callout, new CalloutBlockContent { IconEmoji = "i", Html = "Temporary approval note removed later.", BackgroundColor = "gray" }, createdAt),
                CreateStableBlock(pageId, footerId, 4, BlockType.Paragraph, new TextBlockContent { Html = "Stable footer kept for no-change verification." }, createdAt)
            ],
            2 =>
            [
                CreateStableBlock(pageId, headingId, 0, BlockType.Heading1, new HeadingBlockContent { Level = 1, Html = "CF23 Page Diff Baseline" }, createdAt),
                CreateStableBlock(pageId, movedId, 1, BlockType.Heading2, new HeadingBlockContent { Level = 2, Html = "Rollout notes" }, createdAt),
                CreateStableBlock(pageId, bodyId, 2, BlockType.Paragraph, new TextBlockContent { Html = "Updated launch narrative with reviewer notes and release context." }, createdAt),
                CreateStableBlock(pageId, addedId, 3, BlockType.TodoItem, new TodoBlockContent { Html = "Added validation checklist item", IsChecked = false }, createdAt),
                CreateStableBlock(pageId, footerId, 4, BlockType.Paragraph, new TextBlockContent { Html = "Stable footer kept for no-change verification." }, createdAt)
            ],
            _ =>
            [
                CreateStableBlock(pageId, headingId, 0, BlockType.Heading1, new HeadingBlockContent { Level = 1, Html = "CF23 Page Diff Baseline" }, createdAt),
                CreateStableBlock(pageId, movedId, 1, BlockType.Heading2, new HeadingBlockContent { Level = 2, Html = "Rollout notes" }, createdAt),
                CreateStableBlock(pageId, bodyId, 2, BlockType.Paragraph, new TextBlockContent { Html = "Updated launch narrative with reviewer notes and release context." }, createdAt),
                CreateStableBlock(pageId, addedId, 3, BlockType.TodoItem, new TodoBlockContent { Html = "Added validation checklist item", IsChecked = true }, createdAt),
                ..CreateAddedChecklistBlocks(pageId, createdAt, 4),
                CreateStableBlock(pageId, footerId, 20, BlockType.Paragraph, new TextBlockContent { Html = "Stable footer kept for no-change verification." }, createdAt)
            ]
        };
    }

    private static PageBlock CreateBlock(Guid pageId, int versionNumber, int order, BlockType type, IBlockContent content, DateTime createdAt) => new()
    {
        Id = Guid.Parse($"eb130001-0000-{versionNumber:0000}-{order:0000}-{versionNumber * 100 + order:000000000000}"),
        PageId = pageId,
        Type = type,
        Order = order - 1,
        Content = content,
        CreatedAt = createdAt,
        LastEditedAt = createdAt.AddMinutes(order)
    };

    private static PageBlock CreateStableBlock(Guid pageId, Guid id, int order, BlockType type, IBlockContent content, DateTime createdAt) => new()
    {
        Id = id,
        PageId = pageId,
        Type = type,
        Order = order,
        Content = content,
        CreatedAt = createdAt,
        LastEditedAt = createdAt.AddMinutes(order)
    };

    private static IEnumerable<PageBlock> CreateAddedChecklistBlocks(Guid pageId, DateTime createdAt, int firstOrder)
    {
        for (var index = 0; index < 16; index++)
        {
            yield return CreateStableBlock(
                pageId,
                Guid.Parse($"cf230005-0000-0000-0000-{index + 1:000000000000}"),
                firstOrder + index,
                BlockType.TodoItem,
                new TodoBlockContent { Html = $"Large diff checklist row {index + 1}", IsChecked = index % 2 == 0 },
                createdAt);
        }
    }

    private static NotionPageVersionDto CloneVersion(NotionPageVersionDto source) => new()
    {
        Id = source.Id,
        PageId = source.PageId,
        EditedAt = source.EditedAt,
        EditedByUserId = source.EditedByUserId,
        EditedByDisplayName = source.EditedByDisplayName,
        ChangeDescription = source.ChangeDescription,
        BlocksSnapshot = source.BlocksSnapshot.Select(CloneBlock).ToList()
    };

    private static PageBlock CloneBlock(PageBlock source) => new()
    {
        Id = source.Id,
        PageId = source.PageId,
        ParentBlockId = source.ParentBlockId,
        Type = source.Type,
        Order = source.Order,
        Content = CloneContent(source.Content),
        CreatedAt = source.CreatedAt,
        LastEditedAt = source.LastEditedAt
    };

    private static IBlockContent CloneContent(IBlockContent content)
    {
        var json = JsonSerializer.Serialize(content, content.GetType());
        return (IBlockContent)(JsonSerializer.Deserialize(json, content.GetType())
            ?? throw new InvalidOperationException($"Failed to clone Notion block content type {content.GetType().Name}."));
    }
}
