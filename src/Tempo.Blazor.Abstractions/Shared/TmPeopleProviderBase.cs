namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Convenience base class for read-only people providers.</summary>
public abstract class TmPeopleProviderBase : ITmPeopleProvider
{
    /// <inheritdoc />
    public virtual TmPeopleProviderCapabilities Capabilities => TmPeopleProviderCapabilities.Read;

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<TmUser>> SearchAsync(
        TmPeopleQuery query,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task<TmUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var users = await GetByIdsAsync([id], cancellationToken).ConfigureAwait(false);
        return users.FirstOrDefault(user => string.Equals(user.Id, id, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TmUser>> GetByIdsAsync(
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        var distinctIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinctIds.Length == 0)
            return [];

        var users = await SearchAsync(new TmPeopleQuery
        {
            Ids = distinctIds,
            IncludeVirtual = true,
            Take = distinctIds.Length
        }, cancellationToken).ConfigureAwait(false);

        var byId = users.ToDictionary(user => user.Id, StringComparer.Ordinal);
        return distinctIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
    }
}
