using Microsoft.Extensions.Logging;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Registry of external work-item providers discovered through dependency injection.</summary>
public sealed class WorkItemProviderRegistry
{
    private readonly Dictionary<string, IWorkItemProvider> _providers;

    /// <summary>Creates a registry from provider implementations.</summary>
    public WorkItemProviderRegistry(
        IEnumerable<IWorkItemProvider>? providers,
        ILogger<WorkItemProviderRegistry>? logger = null)
    {
        _providers = new Dictionary<string, IWorkItemProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers ?? [])
        {
            RegisterProvider(provider, logger);
        }
    }

    /// <summary>Total number of usable providers.</summary>
    public int Count => _providers.Count;

    /// <summary>Returns a provider by key, or null when not registered.</summary>
    public IWorkItemProvider? GetProvider(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            return null;

        return _providers.TryGetValue(providerKey.Trim(), out var provider)
            ? provider
            : null;
    }

    /// <summary>Returns all providers in stable display order.</summary>
    public IReadOnlyCollection<IWorkItemProvider> GetAll()
        => _providers.Values
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void RegisterProvider(IWorkItemProvider provider, ILogger<WorkItemProviderRegistry>? logger)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var providerKey = provider.ProviderKey?.Trim();
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            logger?.LogWarning("Skipping work item provider with an empty provider key.");
            return;
        }

        if (_providers.ContainsKey(providerKey))
        {
            logger?.LogWarning(
                "Duplicate work item provider key '{ProviderKey}' was ignored. The first registered provider is used.",
                providerKey);
            return;
        }

        _providers[providerKey] = provider;
    }
}
