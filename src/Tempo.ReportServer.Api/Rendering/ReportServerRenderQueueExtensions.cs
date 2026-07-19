using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Tempo.ReportServer.Api.Rendering;

/// <summary>
/// Opt-in registration for the SQL-Server-backed distributed render job queue (multi-node scale-out).
/// Additive: <see cref="ReportServerApiExtensions.AddTempoReportServerApi"/> keeps the process-local
/// <see cref="InMemoryReportRenderJobQueue"/> as the default, so every existing host and test is
/// unaffected. A host enables the distributed queue by setting <c>Rendering:JobQueue = SqlServer</c>
/// and calling this after <c>AddTempoReportServerApi</c>.
/// </summary>
public static class ReportServerRenderQueueExtensions
{
    /// <summary>
    /// Selects the render job queue implementation from configuration. When
    /// <c>Rendering:JobQueue = SqlServer</c>, replaces the default in-memory queue with the
    /// <see cref="EfReportRenderJobQueue"/> (scoped, over <c>ReportServerDbContext</c>) and — unless
    /// <c>Rendering:RenderWorker:Enabled = false</c> — registers the hosted
    /// <see cref="DistributedRenderJobWorker"/>. Any other value (or an absent section) is a no-op, so
    /// the in-memory default stands.
    /// </summary>
    public static IServiceCollection AddTempoReportServerRenderJobQueue(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var mode = configuration["Rendering:JobQueue"];
        if (!string.Equals(mode, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            // InMemory (default) or unset: leave the registration from AddTempoReportServerApi in place.
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ReportRenderNodeIdentity>();
        services.Configure<ReportRenderJobQueueOptions>(configuration.GetSection("Rendering:RenderWorker"));

        // Bridge the bound options record to the queue's plain constructor dependency so the queue can be
        // constructed both by DI and directly (e.g. in tests) with the same option values.
        services.TryAddSingleton(provider =>
            provider.GetRequiredService<IOptions<ReportRenderJobQueueOptions>>().Value);

        // The distributed queue is scoped: it uses the request/worker-scoped ReportServerDbContext,
        // store, renderer and request context. Replace the singleton in-memory default.
        services.RemoveAll<IReportRenderJobQueue>();
        services.AddScoped<IReportRenderJobQueue, EfReportRenderJobQueue>();

        var workerEnabled = configuration.GetValue("Rendering:RenderWorker:Enabled", true);
        if (workerEnabled)
        {
            services.AddHostedService<DistributedRenderJobWorker>();
        }

        return services;
    }
}
