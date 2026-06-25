using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Dependency injection helpers for shared authorization providers.</summary>
public static class TmAuthorizationServiceCollectionExtensions
{
    /// <summary>Registers shared authorization services. Safe to call multiple times.</summary>
    /// <param name="services">Service collection to update.</param>
    public static IServiceCollection AddTmAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    /// <summary>Registers an <see cref="ITmAuthorizationProvider"/> implementation.</summary>
    /// <typeparam name="TProvider">Provider implementation type.</typeparam>
    /// <param name="services">Service collection to update.</param>
    /// <param name="lifetime">Provider lifetime.</param>
    public static IServiceCollection AddTmAuthorizationProvider<TProvider>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TProvider : class, ITmAuthorizationProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTmAuthorization();
        services.Add(new ServiceDescriptor(typeof(ITmAuthorizationProvider), typeof(TProvider), lifetime));
        return services;
    }

    /// <summary>Registers an <see cref="ITmAuthorizationProvider"/> instance.</summary>
    /// <param name="services">Service collection to update.</param>
    /// <param name="provider">Provider instance to register.</param>
    public static IServiceCollection AddTmAuthorizationProvider(
        this IServiceCollection services,
        ITmAuthorizationProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);
        services.AddTmAuthorization();
        services.AddSingleton(provider);
        return services;
    }
}
