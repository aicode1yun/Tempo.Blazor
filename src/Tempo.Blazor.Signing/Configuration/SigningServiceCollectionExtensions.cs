using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor signing services.
/// </summary>
public static class SigningServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the signing component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorSigning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        return services;
    }
}
