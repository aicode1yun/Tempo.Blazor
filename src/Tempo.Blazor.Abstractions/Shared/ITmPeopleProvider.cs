namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared provider for searching and resolving users across Tempo.Blazor components.</summary>
public interface ITmPeopleProvider : ITmCapabilityProvider<TmPeopleProviderCapabilities>
{
    /// <summary>Operations this provider supports.</summary>
    new TmPeopleProviderCapabilities Capabilities { get; }

    /// <summary>Searches users matching the query.</summary>
    /// <param name="query">Search and paging options.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmUser>> SearchAsync(TmPeopleQuery query, CancellationToken cancellationToken = default);

    /// <summary>Resolves a single user by id, or null when not found.</summary>
    /// <param name="id">User id to resolve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Resolves multiple users by id.</summary>
    /// <param name="ids">User ids to resolve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmUser>> GetByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
}
