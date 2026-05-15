using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockNotionDataStore
{
    private readonly Dictionary<Guid, NotionPage> _pages = new();

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
            IconEmoji          = "📝",
            CreatedAt          = now.AddDays(-7),
            CreatedByUserId    = "alice",
            LastEditedAt       = now.AddHours(-2),
            LastEditedByUserId = "demo",
            IsFavorite         = true
        };

        _pages[Page2Id] = new NotionPage
        {
            Id                 = Page2Id,
            ParentId           = null,
            Title              = "Product Roadmap",
            Description        = "Quarterly planning and feature roadmap",
            IconEmoji          = "📌",
            CreatedAt          = now.AddDays(-14),
            CreatedByUserId    = "alice",
            LastEditedAt       = now.AddDays(-1),
            LastEditedByUserId = "alice",
            IsFavorite         = true
        };

        _pages[Page3Id] = new NotionPage
        {
            Id                 = Page3Id,
            ParentId           = null,
            Title              = "Meeting Notes",
            Description        = "Weekly team meeting notes and action items",
            IconEmoji          = "🗒️",
            CreatedAt          = now.AddDays(-5),
            CreatedByUserId    = "bob",
            LastEditedAt       = now.AddHours(-1),
            LastEditedByUserId = "bob",
            IsFavorite         = false
        };

        _pages[Page4Id] = new NotionPage
        {
            Id                 = Page4Id,
            ParentId           = null,
            Title              = "Engineering Wiki",
            Description        = "Technical documentation and architecture guides",
            IconEmoji          = "💻",
            CreatedAt          = now.AddDays(-30),
            CreatedByUserId    = "charlie",
            LastEditedAt       = now.AddDays(-3),
            LastEditedByUserId = "charlie",
            IsFavorite         = false
        };

        _pages[Page5Id] = new NotionPage
        {
            Id                 = Page5Id,
            ParentId           = Page4Id,
            Title              = "Architecture Guide",
            Description        = "System architecture, patterns, and design decisions",
            IconEmoji          = "🏗️",
            CreatedAt          = now.AddDays(-25),
            CreatedByUserId    = "charlie",
            LastEditedAt       = now.AddDays(-5),
            LastEditedByUserId = "charlie",
            IsFavorite         = false
        };

        _pages[Page6Id] = new NotionPage
        {
            Id                 = Page6Id,
            ParentId           = Page4Id,
            Title              = "Development Setup",
            Description        = "Local environment setup and tooling guide",
            IconEmoji          = "🔧",
            CreatedAt          = now.AddDays(-20),
            CreatedByUserId    = "bob",
            LastEditedAt       = now.AddDays(-2),
            LastEditedByUserId = "bob",
            IsFavorite         = false
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
                IconEmoji = originalPage.IconEmoji,
                IconImageUrl = originalPage.IconImageUrl,
                CoverImageUrl = originalPage.CoverImageUrl,
                CoverImagePositionY = originalPage.CoverImagePositionY,
                IsFullWidth = originalPage.IsFullWidth,
                IsSmallText = originalPage.IsSmallText,
                IsLocked = originalPage.IsLocked,
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

    public void Reset()
    {
        _pages.Clear();
        InitializeMockData();
    }
}
