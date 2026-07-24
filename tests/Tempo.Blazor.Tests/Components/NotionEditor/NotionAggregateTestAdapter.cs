using System.Text.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Testing;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

internal sealed class NotionAggregateTestAdapter(
    INotionDataProvider dataProvider,
    INotionEditorBlockService blockService) : INotionAggregateProvider
{
    private readonly FakeNotionAggregateProvider _inner = new();
    private readonly HashSet<Guid> _loadedPages = [];
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public async Task<NotionAggregateLoadResult> LoadPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            if (_loadedPages.Add(pageId))
            {
                try
                {
                    _inner.Seed(await BuildSnapshotAsync(pageId));
                }
                catch
                {
                    _loadedPages.Remove(pageId);
                    throw;
                }
            }
        }
        finally
        {
            _loadGate.Release();
        }

        return await _inner.LoadPageAsync(pageId, cancellationToken);
    }

    public Task<NotionAggregateLoadResult> LoadBlockAsync(
        Guid blockId,
        CancellationToken cancellationToken = default)
        => _inner.LoadBlockAsync(blockId, cancellationToken);

    public Task<NotionAggregateSaveResult> SaveAsync(
        NotionAggregateSaveRequest request,
        CancellationToken cancellationToken = default)
        => _inner.SaveAsync(request, cancellationToken);

    private async Task<NotionPageSnapshot> BuildSnapshotAsync(Guid pageId)
    {
        var page = await dataProvider.GetPageAsync(pageId.ToString("D"));
        var blocks = new List<IPageBlock>();
        var pending = new Queue<IPageBlock>(
            (await blockService.GetBlocksAsync(pageId.ToString("D")))
            .OrderBy(block => block.Order));

        while (pending.TryDequeue(out var block))
        {
            blocks.Add(block);
            foreach (var child in (await blockService.GetChildBlocksAsync(block.Id.ToString("D")))
                         .OrderBy(child => child.Order))
            {
                pending.Enqueue(child);
            }
        }

        return new NotionPageSnapshot
        {
            Page = new NotionPageState
            {
                Id = page.Id,
                ParentPageId = page.ParentId,
                Title = page.Title,
                Description = page.Description,
                SpaceId = page.SpaceId,
                Labels = page.Labels,
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
            },
            Blocks = blocks.Select(block => new NotionBlockSnapshot
            {
                Id = block.Id,
                PageId = block.PageId,
                ParentBlockId = block.ParentBlockId,
                Type = block.Type,
                Order = block.Order,
                Content = JsonSerializer.SerializeToElement(
                    block.Content,
                    block.Content.GetType(),
                    NotionAggregateJson.Options),
                CreatedAt = block.CreatedAt,
                LastEditedAt = block.LastEditedAt
            }).ToList()
        };
    }
}
