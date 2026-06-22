using Tempo.Blazor.Models;

namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Minimal provider contract for shared activity and audit entries.</summary>
public interface ITmActivityProvider : ITmCapabilityProvider<TmActivityProviderCapabilities>
{
    /// <summary>Operations this provider supports.</summary>
    new TmActivityProviderCapabilities Capabilities { get; }

    /// <summary>Gets activity entries for an entity.</summary>
    /// <param name="entityRef">Entity to load activity for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmActivityEntry>> GetForEntityAsync(
        TmEntityRef entityRef,
        CancellationToken cancellationToken = default);

    /// <summary>Queries activity entries across entities.</summary>
    /// <param name="query">Query and paging options.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<PagedResult<TmActivityEntry>> QueryAsync(
        TmActivityQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a new activity entry.</summary>
    /// <param name="entry">Activity entry to append.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmActivityEntry> AppendAsync(
        TmActivityEntry entry,
        CancellationToken cancellationToken = default);
}
