using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.Export;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Configuration;

/// <summary>Dependency-injection registration for the optional data-table XLSX exporter.</summary>
public static class DataTableXlsxServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DataTableXlsxExporter"/> as the XLSX service consumed by
    /// <c>TmDataTable</c>. Existing custom registrations are preserved.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddTempoBlazorDataTableXlsx(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DataTableXlsxExporter>();
        services.TryAddSingleton<IDataTableXlsxExporter>(provider =>
            provider.GetRequiredService<DataTableXlsxExporter>());
        return services;
    }
}
