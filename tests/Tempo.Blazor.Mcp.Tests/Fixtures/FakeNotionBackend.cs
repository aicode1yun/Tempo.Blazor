using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests.Fixtures;

public sealed class FakeNotionBackend : INotionDataProvider, INotionBlockProvider
{
    private readonly Dictionary<Guid, NotionPage> _pages = new();
    private readonly Dictionary<Guid, PageBlock> _blocks = new();

    /// <summary>Last scopeAppId received by an app-scoped overload (for asserting MCP forwarding).</summary>
    public string? LastScopeAppId { get; private set; }

    // App-scoped overloads capture the scope then delegate to the unscoped logic.
    public Task<INotionPage> CreatePageAsync(string? parentId, string title, string? scopeAppId)
    {
        LastScopeAppId = scopeAppId;
        return CreatePageAsync(parentId, title);
    }

    public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId, string? scopeAppId)
    {
        LastScopeAppId = scopeAppId;
        return GetChildPagesAsync(parentId);
    }

    public Task<IEnumerable<INotionPage>> GetFavoritesAsync(string? scopeAppId)
    {
        LastScopeAppId = scopeAppId;
        return GetFavoritesAsync();
    }

    public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count, string? scopeAppId)
    {
        LastScopeAppId = scopeAppId;
        return GetRecentPagesAsync(count);
    }

    public Task<IEnumerable<INotionPage>> GetTrashAsync(string? scopeAppId)
    {
        LastScopeAppId = scopeAppId;
        return GetTrashAsync();
    }

    public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, string? scopeAppId, CancellationToken cancellationToken = default)
    {
        LastScopeAppId = scopeAppId;
        return GetPagesByLabelAsync(label, cancellationToken);
    }

    public Guid AddPage(string title, Guid? parentId = null)
    {
        var page = new NotionPage
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };
        _pages[page.Id] = page;
        return page.Id;
    }

    public Guid AddBlock(Guid pageId, BlockType type, IBlockContent content, Guid? parentBlockId = null, int? order = null)
    {
        var block = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            ParentBlockId = parentBlockId,
            Type = type,
            Content = content,
            Order = order ?? _blocks.Values.Count(b => b.PageId == pageId && b.ParentBlockId == parentBlockId)
        };
        _blocks[block.Id] = block;
        return block.Id;
    }

    public Task<INotionPage> GetPageAsync(string pageId)
        => Task.FromResult<INotionPage>(_pages[Guid.Parse(pageId)]);

    public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
    {
        var parent = string.IsNullOrWhiteSpace(parentId) ? (Guid?)null : Guid.Parse(parentId);
        return Task.FromResult(_pages.Values.Where(p => p.ParentId == parent && !p.IsDeleted).Cast<INotionPage>());
    }

    public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
        => Task.FromResult(_pages.Values.Where(p => p.IsFavorite && !p.IsDeleted).Cast<INotionPage>());

    public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
        => Task.FromResult(_pages.Values.OrderByDescending(p => p.LastEditedAt).Take(count).Cast<INotionPage>());

    public Task<IEnumerable<INotionPage>> GetTrashAsync()
        => Task.FromResult(_pages.Values.Where(p => p.IsDeleted).Cast<INotionPage>());

    public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<INotionPage>>(
            _pages.Values.Where(p => p.Labels.Contains(label, StringComparer.OrdinalIgnoreCase)).Cast<INotionPage>().ToList());

    public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(
            _pages.Values.SelectMany(p => p.Labels).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        _pages[pageId].Labels = labels;
        Touch(pageId);
        return Task.CompletedTask;
    }

    public Task<INotionPage> CreatePageAsync(string? parentId, string title)
    {
        var id = AddPage(title, string.IsNullOrWhiteSpace(parentId) ? null : Guid.Parse(parentId));
        return Task.FromResult<INotionPage>(_pages[id]);
    }

    public Task UpdatePageAsync(INotionPage page)
    {
        var updated = CopyPage(page);
        updated.LastEditedAt = DateTime.UtcNow;
        _pages[updated.Id] = updated;
        return Task.CompletedTask;
    }

    public Task DeletePageAsync(string pageId)
    {
        var page = _pages[Guid.Parse(pageId)];
        page.IsDeleted = true;
        page.DeletedAt = DateTime.UtcNow;
        Touch(page.Id);
        return Task.CompletedTask;
    }

    public Task RestorePageAsync(string pageId)
    {
        var page = _pages[Guid.Parse(pageId)];
        page.IsDeleted = false;
        page.DeletedAt = null;
        Touch(page.Id);
        return Task.CompletedTask;
    }

    public Task PermanentlyDeletePageAsync(string pageId)
    {
        _pages.Remove(Guid.Parse(pageId));
        return Task.CompletedTask;
    }

    public Task ToggleFavoriteAsync(string pageId, bool isFavorite)
    {
        var page = _pages[Guid.Parse(pageId)];
        page.IsFavorite = isFavorite;
        Touch(page.Id);
        return Task.CompletedTask;
    }

    public Task MovePageAsync(string pageId, string? newParentId)
    {
        var page = _pages[Guid.Parse(pageId)];
        page.ParentId = string.IsNullOrWhiteSpace(newParentId) ? null : Guid.Parse(newParentId);
        Touch(page.Id);
        return Task.CompletedTask;
    }

    public Task<INotionPage> DuplicatePageAsync(string pageId)
    {
        var source = _pages[Guid.Parse(pageId)];
        var id = AddPage(source.Title + " copy", source.ParentId);
        return Task.FromResult<INotionPage>(_pages[id]);
    }

    public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
    {
        var id = Guid.Parse(pageId);
        return Task.FromResult(_blocks.Values
            .Where(b => b.PageId == id && b.ParentBlockId is null)
            .OrderBy(b => b.Order)
            .Cast<IPageBlock>());
    }

    public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
    {
        var id = Guid.Parse(parentBlockId);
        return Task.FromResult(_blocks.Values
            .Where(b => b.ParentBlockId == id)
            .OrderBy(b => b.Order)
            .Cast<IPageBlock>());
    }

    public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
    {
        var pageGuid = Guid.Parse(pageId);
        var concrete = CopyBlock(block);
        concrete.PageId = pageGuid;
        if (concrete.Id == Guid.Empty)
        {
            concrete.Id = Guid.NewGuid();
        }
        concrete.Order = ResolveOrder(pageGuid, concrete.ParentBlockId, afterBlockId);
        concrete.LastEditedAt = DateTime.UtcNow;
        _blocks[concrete.Id] = concrete;
        Touch(pageGuid);
        return Task.FromResult<IPageBlock>(concrete);
    }

    public async Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
    {
        var created = new List<IPageBlock>();
        string? after = afterBlockId;
        foreach (var block in blocks)
        {
            var createdBlock = await CreateBlockAsync(pageId, block, after);
            created.Add(createdBlock);
            after = createdBlock.Id.ToString();
        }
        return created;
    }

    public Task UpdateBlockAsync(IPageBlock block)
    {
        var concrete = CopyBlock(block);
        concrete.LastEditedAt = DateTime.UtcNow;
        _blocks[concrete.Id] = concrete;
        Touch(concrete.PageId);
        return Task.CompletedTask;
    }

    public Task DeleteBlockAsync(string blockId)
    {
        var id = Guid.Parse(blockId);
        foreach (var child in _blocks.Values.Where(b => b.ParentBlockId == id).Select(b => b.Id).ToList())
        {
            _blocks.Remove(child);
        }
        _blocks.Remove(id);
        return Task.CompletedTask;
    }

    public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds)
    {
        var order = 0;
        foreach (var id in orderedBlockIds.Select(Guid.Parse))
        {
            _blocks[id].Order = order++;
        }
        Touch(Guid.Parse(pageId));
        return Task.CompletedTask;
    }

    public Task MoveBlockAsync(MoveNotionBlockRequest request)
    {
        var block = _blocks[Guid.Parse(request.BlockId)];
        block.PageId = Guid.Parse(request.TargetPageId);
        block.ParentBlockId = string.IsNullOrWhiteSpace(request.TargetParentBlockId) ? null : Guid.Parse(request.TargetParentBlockId);
        block.Order = request.TargetIndex;
        Touch(block.PageId);
        return Task.CompletedTask;
    }

    public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId)
    {
        var block = _blocks[Guid.Parse(blockId)];
        block.PageId = Guid.Parse(targetPageId);
        block.ParentBlockId = null;
        block.Order = ResolveOrder(block.PageId, null, afterBlockId);
        Touch(block.PageId);
        return Task.CompletedTask;
    }

    public Task<IPageBlock> DuplicateBlockAsync(string blockId)
    {
        var source = _blocks[Guid.Parse(blockId)];
        var copy = CopyBlock(source);
        copy.Id = Guid.NewGuid();
        copy.Order = source.Order + 1;
        copy.LastEditedAt = DateTime.UtcNow;
        _blocks[copy.Id] = copy;
        Touch(copy.PageId);
        return Task.FromResult<IPageBlock>(copy);
    }

    public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
    {
        var block = _blocks[Guid.Parse(blockId)];
        block.Type = newType;
        block.Content = DefaultContent(newType);
        block.LastEditedAt = DateTime.UtcNow;
        Touch(block.PageId);
        return Task.FromResult<IPageBlock>(block);
    }

    public Task<string> GetBlockLinkAsync(string blockId)
        => Task.FromResult("#" + blockId);

    private int ResolveOrder(Guid pageId, Guid? parentId, string? afterBlockId)
    {
        if (!string.IsNullOrWhiteSpace(afterBlockId) && _blocks.TryGetValue(Guid.Parse(afterBlockId), out var after))
        {
            return after.Order + 1;
        }

        return _blocks.Values.Count(b => b.PageId == pageId && b.ParentBlockId == parentId);
    }

    private void Touch(Guid pageId)
    {
        if (_pages.TryGetValue(pageId, out var page))
        {
            page.LastEditedAt = DateTime.UtcNow;
        }
    }

    private static NotionPage CopyPage(INotionPage page) => new()
    {
        Id = page.Id,
        ParentId = page.ParentId,
        Title = page.Title,
        Description = page.Description,
        SpaceId = page.SpaceId,
        Labels = page.Labels.ToList(),
        IconEmoji = page.IconEmoji,
        IconImageUrl = page.IconImageUrl,
        CoverImageUrl = page.CoverImageUrl,
        CoverImagePositionY = page.CoverImagePositionY,
        IsFullWidth = page.IsFullWidth,
        IsSmallText = page.IsSmallText,
        IsLocked = page.IsLocked,
        CreatedAt = page.CreatedAt,
        CreatedByUserId = page.CreatedByUserId,
        LastEditedAt = page.LastEditedAt,
        LastEditedByUserId = page.LastEditedByUserId,
        IsDeleted = page.IsDeleted,
        DeletedAt = page.DeletedAt,
        IsFavorite = page.IsFavorite
    };

    private static PageBlock CopyBlock(IPageBlock block) => new()
    {
        Id = block.Id,
        PageId = block.PageId,
        ParentBlockId = block.ParentBlockId,
        Type = block.Type,
        Order = block.Order,
        Content = block.Content,
        CreatedAt = block.CreatedAt,
        LastEditedAt = block.LastEditedAt
    };

    private static IBlockContent DefaultContent(BlockType type)
        => type switch
        {
            BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3 => new HeadingBlockContent(),
            BlockType.BulletList or BlockType.NumberedList => new ListBlockContent(),
            BlockType.TodoItem => new TodoBlockContent(),
            BlockType.Toggle => new ToggleBlockContent(),
            BlockType.Table => new TableBlockContent(),
            BlockType.TableRow => new TableRowBlockContent(),
            BlockType.Diagram => new DiagramBlockContent(),
            BlockType.Wireframe => new WireframeBlockContent(),
            BlockType.Spreadsheet => new SpreadsheetBlockContent(),
            _ => new TextBlockContent()
        };
}
