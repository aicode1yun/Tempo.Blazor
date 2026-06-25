using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Reporting.Services;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.Reporting.Configuration;

/// <summary>Service registration extensions for Tempo.Blazor.Reporting.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers services required by reporting viewer components.</summary>
    public static IServiceCollection AddTempoBlazorReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTempoBlazor();
        services.AddHttpClient();
        services.TryAddSingleton<ITextMeasurer, DefaultReportViewerTextMeasurer>();
        services.TryAddSingleton<ReportPdfRenderer>();
        return services;
    }
}
