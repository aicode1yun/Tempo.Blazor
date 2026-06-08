using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockNotionDataStore
{
    private readonly Dictionary<Guid, NotionPage> _pages = new();

    public sealed record CopyPageTreeResult(NotionPage RootPage, IReadOnlyDictionary<Guid, Guid> PageIdMap);

    public MockNotionDataStore()
    {
        InitializeMockData();
    }

    // Fixed page IDs for stable cross-references
    public static readonly Guid Page1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Page2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Page3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Page4Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid Page5Id = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid Page6Id = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private void InitializeMockData()
    {
        var now = DateTime.UtcNow;

        _pages[Page1Id] = new NotionPage
        {
            Id                 = Page1Id,
            ParentId           = null,
            Title              = "Getting Started with Notion Editor",
            Description        = "A demo page to test the Notion Editor functionality",
            SpaceId            = "getting-started",
            IconEmoji          = "📝",
            CreatedAt          = now.AddDays(-7),
            CreatedByUserId    = "alice",
            LastEditedAt       = now.AddHours(-2),
            LastEditedByUserId = "demo",
            IsFavorite         = true,
            Labels             = ["getting-started", "demo"]
        };

        _pages[Page2Id] = new NotionPage
        {
            Id                 = Page2Id,
            ParentId           = null,
            Title              = "Product Roadmap",
            Description        = "Quarterly planning and feature roadmap",
            SpaceId            = "product",
            IconEmoji          = "📌",
            CreatedAt          = now.AddDays(-14),
            CreatedByUserId    = "alice",
            LastEditedAt       = now.AddDays(-1),
            LastEditedByUserId = "alice",
            IsFavorite         = true,
            Labels             = ["product", "roadmap"]
        };

        _pages[Page3Id] = new NotionPage
        {
            Id                 = Page3Id,
            ParentId           = null,
            Title              = "Meeting Notes",
            Description        = "Weekly team meeting notes and action items",
            SpaceId            = "team",
            IconEmoji          = "🗒️",
            CreatedAt          = now.AddDays(-5),
            CreatedByUserId    = "bob",
            LastEditedAt       = now.AddHours(-1),
            LastEditedByUserId = "bob",
            IsFavorite         = false,
            Labels             = ["meeting", "team"]
        };

        _pages[Page4Id] = new NotionPage
        {
            Id                 = Page4Id,
            ParentId           = null,
            Title              = "Engineering Wiki",
            Description        = "Technical documentation and architecture guides",
            SpaceId            = "engineering",
            IconEmoji          = "💻",
            CreatedAt          = now.AddDays(-30),
            CreatedByUserId    = "charlie",
            LastEditedAt       = now.AddDays(-3),
            LastEditedByUserId = "charlie",
            IsFavorite         = false,
            Labels             = ["engineering", "wiki"]
        };

        _pages[Page5Id] = new NotionPage
        {
            Id                 = Page5Id,
            ParentId           = Page4Id,
            Title              = "Architecture Guide",
            Description        = "System architecture, patterns, and design decisions",
            SpaceId            = "engineering",
            IconEmoji          = "🏗️",
            CreatedAt          = now.AddDays(-25),
            CreatedByUserId    = "charlie",
            LastEditedAt       = now.AddDays(-5),
            LastEditedByUserId = "charlie",
            IsFavorite         = false,
            Labels             = ["engineering", "architecture"]
        };

        _pages[Page6Id] = new NotionPage
        {
            Id                 = Page6Id,
            ParentId           = Page4Id,
            Title              = "Development Setup",
            Description        = "Local environment setup and tooling guide",
            SpaceId            = "engineering",
            IconEmoji          = "🔧",
            CreatedAt          = now.AddDays(-20),
            CreatedByUserId    = "bob",
            LastEditedAt       = now.AddDays(-2),
            LastEditedByUserId = "bob",
            IsFavorite         = false,
            Labels             = ["engineering", "setup"]
        };
    }

    public async Task<INotionPage> GetPageAsync(string pageId)
    {
        if (Guid.TryParse(pageId, out var id) && _pages.TryGetValue(id, out var page))
        {
            return await Task.FromResult(page);
        }
        throw new KeyNotFoundException($"Page {pageId} not found");
    }

    public async Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
    {
        var parentGuid = parentId != null ? Guid.Parse(parentId) : (Guid?)null;
        var children = _pages.Values.Where(p => p.ParentId == parentGuid).Cast<INotionPage>();
        return await Task.FromResult(children);
    }

    public async Task<IEnumerable<INotionPage>> GetFavoritesAsync()
    {
        var favorites = _pages.Values.Where(p => p.IsFavorite).Cast<INotionPage>();
        return await Task.FromResult(favorites);
    }

    public async Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
    {
        var recent = _pages.Values
            .OrderByDescending(p => p.LastEditedAt)
            .Take(count)
            .Cast<INotionPage>();
        return await Task.FromResult(recent);
    }

    public async Task<IEnumerable<INotionPage>> GetTrashAsync()
    {
        var trash = _pages.Values.Where(p => p.IsDeleted).Cast<INotionPage>();
        return await Task.FromResult(trash);
    }

    public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeLabel(label);
        if (string.IsNullOrWhiteSpace(normalized))
            return Task.FromResult<IReadOnlyList<INotionPage>>([]);

        var pages = _pages.Values
            .Where(page => !page.IsDeleted)
            .Where(page => page.Labels.Any(existing => string.Equals(NormalizeLabel(existing), normalized, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
            .Cast<INotionPage>()
            .ToArray();

        return Task.FromResult<IReadOnlyList<INotionPage>>(pages);
    }

    public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
    {
        var labels = _pages.Values
            .Where(page => !page.IsDeleted)
            .SelectMany(page => page.Labels)
            .Select(NormalizeLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(labels);
    }

    public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        if (!_pages.TryGetValue(pageId, out var page))
            throw new KeyNotFoundException($"Page {pageId} not found");

        page.Labels = NormalizeLabels(labels);
        page.LastEditedAt = DateTime.UtcNow;
        page.LastEditedByUserId = "demo-user";
        _pages[pageId] = page;
        return Task.CompletedTask;
    }

    public async Task<INotionPage> CreatePageAsync(string? parentId, string title)
    {
        var parentGuid = parentId != null ? Guid.Parse(parentId) : (Guid?)null;
        var page = new NotionPage
        {
            Id = Guid.NewGuid(),
            ParentId = parentGuid,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = "demo-user",
            LastEditedAt = DateTime.UtcNow,
            LastEditedByUserId = "demo-user"
        };

        _pages[page.Id] = page;
        return await Task.FromResult(page);
    }

    public async Task UpdatePageAsync(INotionPage page)
    {
        if (page is NotionPage notionPage && _pages.ContainsKey(notionPage.Id))
        {
            notionPage.LastEditedAt = DateTime.UtcNow;
            notionPage.LastEditedByUserId = "demo-user";
            _pages[notionPage.Id] = notionPage;
        }
        await Task.CompletedTask;
    }

    public async Task DeletePageAsync(string pageId)
    {
        if (Guid.TryParse(pageId, out var id) && _pages.TryGetValue(id, out var page))
        {
            page.IsDeleted = true;
            page.DeletedAt = DateTime.UtcNow;
            _pages[id] = page;
        }
        await Task.CompletedTask;
    }

    public async Task RestorePageAsync(string pageId)
    {
        if (Guid.TryParse(pageId, out var id) && _pages.TryGetValue(id, out var page))
        {
            page.IsDeleted = false;
            page.DeletedAt = null;
            _pages[id] = page;
        }
        await Task.CompletedTask;
    }

    public async Task PermanentlyDeletePageAsync(string pageId)
    {
        if (Guid.TryParse(pageId, out var id))
        {
            _pages.Remove(id);
        }
        await Task.CompletedTask;
    }

    public async Task ToggleFavoriteAsync(string pageId, bool isFavorite)
    {
        if (Guid.TryParse(pageId, out var id) && _pages.TryGetValue(id, out var page))
        {
            page.IsFavorite = isFavorite;
            _pages[id] = page;
        }
        await Task.CompletedTask;
    }

    public async Task MovePageAsync(string pageId, string? newParentId)
    {
        if (Guid.TryParse(pageId, out var id) && _pages.TryGetValue(id, out var page))
        {
            page.ParentId = newParentId != null ? Guid.Parse(newParentId) : null;
            _pages[id] = page;
        }
        await Task.CompletedTask;
    }

    public async Task<INotionPage> DuplicatePageAsync(string pageId)
    {
        if (Guid.TryParse(pageId, out var id) && _pages.TryGetValue(id, out var originalPage))
        {
            var duplicated = new NotionPage
            {
                Id = Guid.NewGuid(),
                ParentId = originalPage.ParentId,
                Title = $"{originalPage.Title} (Copy)",
                Description = originalPage.Description,
                SpaceId = originalPage.SpaceId,
                IconEmoji = originalPage.IconEmoji,
                IconImageUrl = originalPage.IconImageUrl,
                CoverImageUrl = originalPage.CoverImageUrl,
                CoverImagePositionY = originalPage.CoverImagePositionY,
                IsFullWidth = originalPage.IsFullWidth,
                IsSmallText = originalPage.IsSmallText,
                IsLocked = originalPage.IsLocked,
                Labels = originalPage.Labels.ToArray(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = "demo-user",
                LastEditedAt = DateTime.UtcNow,
                LastEditedByUserId = "demo-user"
            };

            _pages[duplicated.Id] = duplicated;
            return await Task.FromResult(duplicated);
        }
        throw new KeyNotFoundException($"Page {pageId} not found");
    }

    public IEnumerable<INotionPage> GetAllPages()
    {
        return _pages.Values.Cast<INotionPage>();
    }

    public Task<IReadOnlyList<INotionPage>> GetPagesInSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(spaceId))
            return Task.FromResult<IReadOnlyList<INotionPage>>(_pages.Values.Cast<INotionPage>().ToArray());

        var normalized = spaceId.Trim();
        var pages = _pages.Values
            .Where(page =>
            {
                if (string.Equals(page.SpaceId, normalized, StringComparison.OrdinalIgnoreCase))
                    return true;

                var root = ResolveRootPage(page);
                return root is not null &&
                       (string.Equals(root.Id.ToString("D"), normalized, StringComparison.OrdinalIgnoreCase) ||
                        root.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase));
            })
            .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
            .Cast<INotionPage>()
            .ToArray();

        return Task.FromResult<IReadOnlyList<INotionPage>>(pages);
    }

    public void SeedE2ESearchPage()
    {
        _pages.Clear();
        var created = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        _pages[Page1Id] = new NotionPage
        {
            Id = Page1Id,
            Title = "CF22 Knowledge Space",
            Description = "Engineering reference space",
            SpaceId = "cf22-knowledge",
            IconEmoji = "K",
            CreatedAt = created,
            CreatedByUserId = "alice",
            LastEditedAt = new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc),
            LastEditedByUserId = "alice",
            Labels = ["engineering", "knowledge"]
        };

        _pages[Page2Id] = new NotionPage
        {
            Id = Page2Id,
            Title = "CF22 Produktová strategie",
            Description = "Produktový plán pro český trh",
            SpaceId = "cf22-product",
            IconEmoji = "P",
            CreatedAt = created.AddDays(1),
            CreatedByUserId = "bob",
            LastEditedAt = new DateTime(2026, 1, 22, 12, 0, 0, DateTimeKind.Utc),
            LastEditedByUserId = "bob",
            Labels = ["product"]
        };

        _pages[Page3Id] = new NotionPage
        {
            Id = Page3Id,
            Title = "CF22 Support Space",
            Description = "Support knowledge base",
            SpaceId = "cf22-support",
            IconEmoji = "S",
            CreatedAt = created.AddDays(2),
            CreatedByUserId = "dana",
            LastEditedAt = new DateTime(2026, 1, 23, 12, 0, 0, DateTimeKind.Utc),
            LastEditedByUserId = "dana",
            Labels = ["support"]
        };

        _pages[Page4Id] = new NotionPage
        {
            Id = Page4Id,
            ParentId = Page3Id,
            Title = "CF22 Escalation Notes",
            Description = "Customer escalation details",
            SpaceId = "cf22-support",
            IconEmoji = "E",
            CreatedAt = created.AddDays(3),
            CreatedByUserId = "dana",
            LastEditedAt = new DateTime(2026, 1, 24, 12, 0, 0, DateTimeKind.Utc),
            LastEditedByUserId = "dana",
            Labels = ["support", "customer"]
        };
    }

    public void SeedE2EBulkPages()
    {
        _pages.Clear();
        var now = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        _pages[Page1Id] = new NotionPage
        {
            Id = Page1Id,
            Title = "CF24 Source Root",
            SpaceId = "cf24-source",
            CreatedAt = now,
            LastEditedAt = now,
            CreatedByUserId = "alice",
            LastEditedByUserId = "alice",
            Labels = ["bulk"]
        };

        _pages[Page2Id] = new NotionPage
        {
            Id = Page2Id,
            ParentId = Page1Id,
            Title = "CF24 Child A",
            SpaceId = "cf24-source",
            CreatedAt = now.AddMinutes(1),
            LastEditedAt = now.AddMinutes(1),
            CreatedByUserId = "alice",
            LastEditedByUserId = "alice",
            Labels = ["bulk"]
        };

        _pages[Page3Id] = new NotionPage
        {
            Id = Page3Id,
            ParentId = Page2Id,
            Title = "CF24 Grandchild A1",
            SpaceId = "cf24-source",
            CreatedAt = now.AddMinutes(2),
            LastEditedAt = now.AddMinutes(2),
            CreatedByUserId = "alice",
            LastEditedByUserId = "alice",
            Labels = ["bulk"]
        };

        _pages[Page4Id] = new NotionPage
        {
            Id = Page4Id,
            Title = "CF24 Target",
            SpaceId = "cf24-target",
            CreatedAt = now.AddMinutes(3),
            LastEditedAt = now.AddMinutes(3),
            CreatedByUserId = "bob",
            LastEditedByUserId = "bob",
            Labels = ["bulk-target"]
        };
    }

    public void SeedE2ERestrictionsPage() => SeedE2EBulkPages();

    public void SeedE2ESidebarEmptyNavigation()
    {
        _pages.Clear();
        _pages[Page1Id] = new NotionPage
        {
            Id = Page1Id,
            Title = "Hidden task host",
            SpaceId = "hidden",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastEditedAt = DateTime.UtcNow.AddDays(-1),
            CreatedByUserId = "system",
            LastEditedByUserId = "system",
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
    }

    public Task<CopyPageTreeResult> CopyPageTreeAsync(string pageId, string? newParentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceId = Guid.Parse(pageId);
        Guid? parentId = string.IsNullOrWhiteSpace(newParentId) ? null : Guid.Parse(newParentId);

        if (!_pages.TryGetValue(sourceId, out var source))
            throw new KeyNotFoundException($"Page {pageId} not found");

        var map = new Dictionary<Guid, Guid>();
        var copiedRoot = CopyPageRecursive(source, parentId, true, map, cancellationToken);
        return Task.FromResult(new CopyPageTreeResult(copiedRoot, map));
    }

    public Task MovePagesAsync(IReadOnlyList<string> pageIds, string? newParentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = pageIds.Select(Guid.Parse).Distinct().ToArray();
        Guid? parentId = string.IsNullOrWhiteSpace(newParentId) ? null : Guid.Parse(newParentId);

        if (parentId is not null)
        {
            foreach (var id in ids)
            {
                if (id == parentId.Value || IsDescendantOf(parentId.Value, id))
                    throw new InvalidOperationException("Pages cannot be moved into their own descendants.");
            }
        }

        foreach (var id in ids)
        {
            if (!_pages.TryGetValue(id, out var page))
                throw new KeyNotFoundException($"Page {id} not found");
        }

        foreach (var id in ids)
        {
            var page = _pages[id];
            page.ParentId = parentId;
            page.LastEditedAt = DateTime.UtcNow;
            page.LastEditedByUserId = "demo-user";
        }

        return Task.CompletedTask;
    }

    public Task DeletePagesAsync(IReadOnlyList<string> pageIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = pageIds.Select(Guid.Parse).Distinct().ToArray();
        var toDelete = ids.SelectMany(GetPageAndDescendants).Distinct().ToArray();
        var now = DateTime.UtcNow;

        foreach (var id in toDelete)
        {
            if (!_pages.TryGetValue(id, out var page))
                continue;

            page.IsDeleted = true;
            page.DeletedAt = now;
            page.LastEditedAt = now;
            page.LastEditedByUserId = "demo-user";
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        _pages.Clear();
        InitializeMockData();
    }

    private static string NormalizeLabel(string? label)
        => label?.Trim() ?? string.Empty;

    private static IReadOnlyList<string> NormalizeLabels(IEnumerable<string>? labels)
    {
        var normalized = new List<string>();
        foreach (var label in labels ?? [])
        {
            var value = NormalizeLabel(label);
            if (string.IsNullOrWhiteSpace(value) ||
                normalized.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
                continue;

            normalized.Add(value);
        }

        return normalized;
    }

    private NotionPage? ResolveRootPage(NotionPage page)
    {
        var current = page;
        while (current.ParentId is { } parentId)
        {
            if (!_pages.TryGetValue(parentId, out var parent))
                return current;

            current = parent;
        }

        return current;
    }

    private NotionPage CopyPageRecursive(
        NotionPage source,
        Guid? parentId,
        bool isRoot,
        Dictionary<Guid, Guid> map,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copy = new NotionPage
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Title = isRoot ? $"{source.Title} (Copy)" : source.Title,
            Description = source.Description,
            SpaceId = source.SpaceId,
            Labels = source.Labels.ToArray(),
            IconEmoji = source.IconEmoji,
            IconImageUrl = source.IconImageUrl,
            CoverImageUrl = source.CoverImageUrl,
            CoverImagePositionY = source.CoverImagePositionY,
            IsFullWidth = source.IsFullWidth,
            IsSmallText = source.IsSmallText,
            IsLocked = source.IsLocked,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = "demo-user",
            LastEditedAt = DateTime.UtcNow,
            LastEditedByUserId = "demo-user",
            IsFavorite = false
        };

        map[source.Id] = copy.Id;
        _pages[copy.Id] = copy;

        var children = _pages.Values
            .Where(page => page.ParentId == source.Id && page.Id != copy.Id)
            .OrderBy(page => page.CreatedAt)
            .ToArray();

        foreach (var child in children)
            CopyPageRecursive(child, copy.Id, false, map, cancellationToken);

        return copy;
    }

    private bool IsDescendantOf(Guid candidateId, Guid ancestorId)
    {
        var currentId = candidateId;
        var visited = new HashSet<Guid>();

        while (visited.Add(currentId) && _pages.TryGetValue(currentId, out var page) && page.ParentId is { } parentId)
        {
            if (parentId == ancestorId)
                return true;

            currentId = parentId;
        }

        return false;
    }

    private IEnumerable<Guid> GetPageAndDescendants(Guid pageId)
    {
        yield return pageId;

        foreach (var childId in _pages.Values.Where(page => page.ParentId == pageId).Select(page => page.Id).ToArray())
        {
            foreach (var descendantId in GetPageAndDescendants(childId))
                yield return descendantId;
        }
    }
}
