using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockNotionDataStore
{
    private readonly Dictionary<Guid, NotionPage> _pages = new();

    public MockNotionDataStore()
    {
        InitializeMockData();
    }

    private void InitializeMockData()
    {
        var pageId = Guid.NewGuid();

        var page = new NotionPage
        {
            Id = pageId,
            ParentId = null,
            Title = "Getting Started with Notion Editor",
            Description = "A demo page to test the Notion Editor functionality",
            IconEmoji = "📝",
            IsFullWidth = false,
            IsSmallText = false,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = "demo-user",
            LastEditedAt = DateTime.UtcNow,
            LastEditedByUserId = "demo-user",
            IsFavorite = true
        };

        _pages[page.Id] = page;
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
}
