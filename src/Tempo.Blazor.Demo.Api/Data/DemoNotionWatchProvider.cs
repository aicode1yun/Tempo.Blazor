using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionWatchProvider : INotionWatchProvider
{
    private readonly object _syncRoot = new();
    private readonly MockNotionDataStore _dataStore;
    private readonly Dictionary<(string PageId, string UserId), NotionWatchSubscriptionDto> _subscriptions = new();

    public DemoNotionWatchProvider(MockNotionDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task WatchAsync(string pageId, string userId, bool includeChildren, CancellationToken cancellationToken = default)
    {
        var normalizedPageId = await NormalizePageIdAsync(pageId, cancellationToken);
        var normalizedUserId = NormalizeUserId(userId);
        var key = (normalizedPageId, normalizedUserId);

        lock (_syncRoot)
        {
            _subscriptions[key] = new NotionWatchSubscriptionDto
            {
                PageId = normalizedPageId,
                UserId = normalizedUserId,
                IncludeChildren = includeChildren,
                CreatedAt = _subscriptions.TryGetValue(key, out var existing) ? existing.CreatedAt : DateTime.UtcNow
            };
        }
    }

    public async Task UnwatchAsync(string pageId, string userId, CancellationToken cancellationToken = default)
    {
        var normalizedPageId = await NormalizePageIdAsync(pageId, cancellationToken);
        var normalizedUserId = NormalizeUserId(userId);
        lock (_syncRoot)
        {
            _subscriptions.Remove((normalizedPageId, normalizedUserId));
        }
    }

    public async Task<IReadOnlyList<NotionWatchSubscriptionDto>> GetWatchersAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var normalizedPageId = await NormalizePageIdAsync(pageId, cancellationToken);
        var ancestorIds = await GetAncestorIdsAsync(normalizedPageId, cancellationToken);

        lock (_syncRoot)
        {
            return _subscriptions.Values
                .Where(s => string.Equals(s.PageId, normalizedPageId, StringComparison.OrdinalIgnoreCase) ||
                            (s.IncludeChildren && ancestorIds.Contains(s.PageId)))
                .GroupBy(s => s.UserId, StringComparer.OrdinalIgnoreCase)
                .Select(g => Clone(g.OrderBy(s => s.PageId == normalizedPageId ? 0 : 1).First()))
                .OrderBy(s => s.UserId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public async Task<bool> IsWatchingAsync(string pageId, string userId, CancellationToken cancellationToken = default)
    {
        var watchers = await GetWatchersAsync(pageId, cancellationToken);
        return watchers.Any(w => string.Equals(w.UserId, NormalizeUserId(userId), StringComparison.OrdinalIgnoreCase));
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _subscriptions.Clear();
        }
    }

    public async Task SeedE2EWatchAsync(CancellationToken cancellationToken = default)
    {
        Reset();
        await WatchAsync(MockNotionDataStore.Page1Id.ToString("D"), "demo", includeChildren: false, cancellationToken);
    }

    private async Task<HashSet<string>> GetAncestorIdsAsync(string pageId, CancellationToken cancellationToken)
    {
        var ancestors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = await _dataStore.GetPageAsync(pageId);
        while (current.ParentId is { } parentId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parentText = parentId.ToString("D");
            if (!ancestors.Add(parentText))
                break;

            current = await _dataStore.GetPageAsync(parentText);
        }

        return ancestors;
    }

    private async Task<string> NormalizePageIdAsync(string pageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await _dataStore.GetPageAsync(pageId);
        return page.Id.ToString("D");
    }

    private static string NormalizeUserId(string userId)
        => string.IsNullOrWhiteSpace(userId)
            ? throw new ArgumentException("User id is required.", nameof(userId))
            : userId.Trim();

    private static NotionWatchSubscriptionDto Clone(NotionWatchSubscriptionDto subscription)
        => new()
        {
            PageId = subscription.PageId,
            UserId = subscription.UserId,
            IncludeChildren = subscription.IncludeChildren,
            CreatedAt = subscription.CreatedAt
        };
}
