using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tempo.Blazor.DocumentFormats.HeadlessLayout;

/// <summary>DI registration for the headless document layout service.</summary>
public static class TempoDocumentLayoutServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITempoDocumentLayoutService"/> backed by the pooled Jint host
    /// (<see cref="JintDocumentLayoutEngine"/>) as a singleton. Idempotent.
    /// </summary>
    public static IServiceCollection AddTempoDocumentLayout(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITempoDocumentLayoutService, JintDocumentLayoutEngine>();
        return services;
    }
}

/// <summary>DI registration for the headless document facade.</summary>
public static class TempoDocumentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full headless document pipeline: <see cref="ITempoDocumentLayoutService"/>
    /// (pooled Jint host) and the <see cref="ITempoDocumentService"/> facade (assembly → layout →
    /// PDF / PNG previews) as idempotent singletons. The facade resolves a registered
    /// <see cref="TimeProvider"/> when present, otherwise uses the system clock.
    /// </summary>
    public static IServiceCollection AddTempoDocumentServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoDocumentLayout();
        services.TryAddSingleton<ITempoDocumentService>(provider => new TempoDocumentService(
            provider.GetRequiredService<ITempoDocumentLayoutService>(),
            provider.GetService<TimeProvider>()));
        return services;
    }
}
