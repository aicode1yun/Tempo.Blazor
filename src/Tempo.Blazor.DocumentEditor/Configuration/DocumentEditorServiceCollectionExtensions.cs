using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor document editor services.
/// </summary>
public static class DocumentEditorServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the document editor component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorDocumentEditor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        return services;
    }
}
