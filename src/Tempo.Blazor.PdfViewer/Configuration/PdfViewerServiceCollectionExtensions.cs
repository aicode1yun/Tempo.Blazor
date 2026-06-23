using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Configuration;

/// <summary>Extension methods for registering Tempo.Blazor PDF viewer services.</summary>
public static class PdfViewerServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the PDF viewer component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorPdfViewer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        return services;
    }
}
