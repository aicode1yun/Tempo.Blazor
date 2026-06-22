using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Dependency injection helpers for shared people providers.</summary>
public static class TmPeopleServiceCollectionExtensions
{
    /// <summary>Registers shared people services. Safe to call multiple times.</summary>
    /// <param name="services">Service collection to update.</param>
    public static IServiceCollection AddTmPeople(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    /// <summary>Registers an <see cref="ITmPeopleProvider"/> implementation.</summary>
    /// <typeparam name="TProvider">Provider implementation type.</typeparam>
    /// <param name="services">Service collection to update.</param>
    /// <param name="lifetime">Provider lifetime.</param>
    public static IServiceCollection AddTmPeopleProvider<TProvider>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TProvider : class, ITmPeopleProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTmPeople();
        services.Add(new ServiceDescriptor(typeof(ITmPeopleProvider), typeof(TProvider), lifetime));
        return services;
    }

    /// <summary>Registers an <see cref="ITmPeopleProvider"/> instance.</summary>
    /// <param name="services">Service collection to update.</param>
    /// <param name="provider">Provider instance to register.</param>
    public static IServiceCollection AddTmPeopleProvider(
        this IServiceCollection services,
        ITmPeopleProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);
        services.AddTmPeople();
        services.AddSingleton(provider);
        return services;
    }

    /// <summary>Registers an <see cref="ITmCurrentUser"/> implementation if none is registered yet.</summary>
    /// <typeparam name="TCurrentUser">Current-user provider implementation type.</typeparam>
    /// <param name="services">Service collection to update.</param>
    /// <param name="lifetime">Provider lifetime.</param>
    public static IServiceCollection AddTmCurrentUser<TCurrentUser>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCurrentUser : class, ITmCurrentUser
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTmPeople();
        services.TryAdd(new ServiceDescriptor(typeof(ITmCurrentUser), typeof(TCurrentUser), lifetime));
        return services;
    }
}
