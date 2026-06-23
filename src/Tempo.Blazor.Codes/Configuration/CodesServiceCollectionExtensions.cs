using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Configuration;

/// <summary>Extension methods for registering Tempo.Blazor QR code and barcode services.</summary>
public static class CodesServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the QR code and barcode component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorCodes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        return services;
    }
}
