using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionWatchProvider
{
    Task WatchAsync(string pageId, string userId, bool includeChildren, CancellationToken cancellationToken = default);
    Task UnwatchAsync(string pageId, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotionWatchSubscriptionDto>> GetWatchersAsync(string pageId, CancellationToken cancellationToken = default);
    Task<bool> IsWatchingAsync(string pageId, string userId, CancellationToken cancellationToken = default);
}
