using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>
/// Loads and atomically persists complete Notion page aggregates.
/// </summary>
/// <remarks>
/// This is the only persistence boundary for canonical Notion authoring. Implementations must treat
/// every <see cref="SaveAsync"/> call as one all-or-nothing transaction, including requests that
/// contain more than one page. The interface deliberately exposes no granular create, update,
/// delete, move, reorder, or restore methods.
/// </remarks>
public interface INotionAggregateProvider
{
    /// <summary>
    /// Loads the complete aggregate rooted at <paramref name="pageId"/>.
    /// </summary>
    /// <param name="pageId">Stable page identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    /// <returns>The page snapshot, including its opaque concurrency token and content digest.</returns>
    Task<NotionAggregateLoadResult> LoadPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the complete owning page aggregate for <paramref name="blockId"/>.
    /// </summary>
    /// <param name="blockId">Stable block identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the load.</param>
    /// <returns>
    /// The owning page snapshot with <see cref="NotionAggregateLoadResult.MatchedBlockId"/> set to
    /// <paramref name="blockId"/>, or a not-found result.
    /// </returns>
    Task<NotionAggregateLoadResult> LoadBlockAsync(
        Guid blockId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically saves one or more complete page aggregates.
    /// </summary>
    /// <param name="request">
    /// Complete replacement snapshots and the opaque concurrency token on which each replacement is
    /// based. All block identifiers must be allocated before this method is called.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the save before it commits.</param>
    /// <returns>
    /// A result for the whole transaction. A failed or cancelled save must not persist any page from
    /// the request.
    /// </returns>
    Task<NotionAggregateSaveResult> SaveAsync(
        NotionAggregateSaveRequest request,
        CancellationToken cancellationToken = default);
}
