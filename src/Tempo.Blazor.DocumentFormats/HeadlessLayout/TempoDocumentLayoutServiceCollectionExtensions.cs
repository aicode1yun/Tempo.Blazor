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
