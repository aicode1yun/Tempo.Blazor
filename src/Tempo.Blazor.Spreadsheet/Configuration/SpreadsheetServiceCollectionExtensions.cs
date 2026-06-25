using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering Tempo.Blazor spreadsheet services.
/// </summary>
public static class SpreadsheetServiceCollectionExtensions
{
    /// <summary>
    /// Registers services required by the spreadsheet component group.
    /// </summary>
    public static IServiceCollection AddTempoBlazorSpreadsheet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTempoBlazor();
        return services;
    }
}
