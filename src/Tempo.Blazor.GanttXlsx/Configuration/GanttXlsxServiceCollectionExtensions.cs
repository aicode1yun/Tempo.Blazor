using Microsoft.Extensions.DependencyInjection;

namespace Tempo.Blazor.Configuration;

/// <summary>
/// Extension methods for registering optional Tempo.Blazor Gantt XLSX helpers.
/// </summary>
public static class GanttXlsxServiceCollectionExtensions
{
    /// <summary>
    /// Marks the optional Gantt XLSX feature package as registered.
    /// </summary>
    public static IServiceCollection AddTempoBlazorGanttXlsx(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
