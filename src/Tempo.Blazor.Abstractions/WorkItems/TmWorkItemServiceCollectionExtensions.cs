using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// DI helpers for registering the unified work-item providers in one place,
/// so every task-bearing component in the application shares the same source(s).
/// </summary>
public static class TmWorkItemServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="TmWorkItemProviderRegistry"/>. Call <see cref="AddTmWorkItemProvider{T}"/>
    /// for each source. Safe to call multiple times.
    /// </summary>
    public static IServiceCollection AddTmWorkItems(this IServiceCollection services)
    {
        services.TryAddScoped<TmWorkItemProviderRegistry>();
        return services;
    }

    /// <summary>Registers a work-item provider implementation and ensures the registry is available.</summary>
    public static IServiceCollection AddTmWorkItemProvider<TProvider>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TProvider : class, ITmWorkItemProvider
    {
        services.AddTmWorkItems();
        services.Add(new ServiceDescriptor(typeof(ITmWorkItemProvider), typeof(TProvider), lifetime));
        return services;
    }

    /// <summary>Registers a work-item provider instance and ensures the registry is available.</summary>
    public static IServiceCollection AddTmWorkItemProvider(
        this IServiceCollection services,
        ITmWorkItemProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        services.AddTmWorkItems();
        services.AddSingleton(provider);
        return services;
    }
}
