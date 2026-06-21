using Microsoft.Extensions.Logging;

namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Registry of <see cref="ITmWorkItemProvider"/>s discovered through dependency injection,
/// keyed by <see cref="ITmWorkItemProvider.SourceKey"/>. Components resolve a provider by
/// key (or use the single registered provider) so they all share the same task source.
/// </summary>
public sealed class TmWorkItemProviderRegistry
{
    private readonly Dictionary<string, ITmWorkItemProvider> _providers;

    /// <summary>Creates a registry from provider implementations.</summary>
    public TmWorkItemProviderRegistry(
        IEnumerable<ITmWorkItemProvider>? providers,
        ILogger<TmWorkItemProviderRegistry>? logger = null)
    {
        _providers = new Dictionary<string, ITmWorkItemProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers ?? [])
        {
            RegisterProvider(provider, logger);
        }
    }

    /// <summary>Total number of usable providers.</summary>
    public int Count => _providers.Count;

    /// <summary>Returns a provider by key, or null when not registered.</summary>
    public ITmWorkItemProvider? GetProvider(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return null;

        return _providers.TryGetValue(sourceKey.Trim(), out var provider)
            ? provider
            : null;
    }

    /// <summary>
    /// Returns the single registered provider when exactly one exists, otherwise null.
    /// Convenient for the common case where an application has one task source.
    /// </summary>
    public ITmWorkItemProvider? GetDefault()
        => _providers.Count == 1 ? _providers.Values.First() : null;

    /// <summary>Returns all providers in stable display order.</summary>
    public IReadOnlyCollection<ITmWorkItemProvider> GetAll()
        => _providers.Values
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(provider => provider.SourceKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void RegisterProvider(ITmWorkItemProvider provider, ILogger<TmWorkItemProviderRegistry>? logger)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var sourceKey = provider.SourceKey?.Trim();
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            logger?.LogWarning("Skipping work item provider with an empty source key.");
            return;
        }

        if (_providers.ContainsKey(sourceKey))
        {
            logger?.LogWarning(
                "Duplicate work item provider source key '{SourceKey}' was ignored. The first registered provider is used.",
                sourceKey);
            return;
        }

        _providers[sourceKey] = provider;
    }
}
