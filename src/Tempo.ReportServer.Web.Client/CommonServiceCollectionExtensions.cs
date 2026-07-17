using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.EmailTemplates.Abstractions;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;
using Tempo.Blazor.Reporting.Configuration;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Client;

/// <summary>
/// Registers the client-safe UI and data services shared by both hosts of the
/// InteractiveAuto Report Server: the InteractiveServer host (<c>Tempo.ReportServer.Web</c>)
/// and the WebAssembly leg (<c>Tempo.ReportServer.Web.Client</c>). Anything server-only
/// (OIDC/BFF auth providers, the API-key store and report-server security, the minimal API
/// endpoints, the outgoing API client) stays in the host's <c>Program.cs</c>.
/// </summary>
public static class CommonServiceCollectionExtensions
{
    /// <summary>Adds the shared, client-safe Report Server services to <paramref name="services"/>.</summary>
    public static IServiceCollection AddCommonServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Reporting components + email template engine (renderer, generation, validation).
        services.AddTempoBlazorReporting();
        services.AddTempoEmailTemplateEngine();

        // In-memory demo catalog + report source factory.
        services.AddSingleton<DemoReportSourceFactory>();
        services.AddSingleton<ReportServerCatalogStore>();

        // Scheduling + email delivery (all in-memory / abstraction-based, WASM-safe).
        services.AddSingleton<IReportScheduleClock, SystemReportScheduleClock>();
        services.AddSingleton<ReportScheduleStore>();
        services.AddSingleton<ReportRenderJobQueue>();
        services.AddSingleton<ReportEmailOutbox>();
        services.AddSingleton<ReportEmailTemplateGalleryStore>();
        services.AddSingleton<IEmailTemplateStore>(sp => sp.GetRequiredService<ReportEmailTemplateGalleryStore>());
        services.AddSingleton<IEmailSender, Smtp4DevEmailSender>();
        services.AddSingleton<IReportScheduledDeliveryService, ReportEmailDeliveryService>();
        services.AddSingleton<ReportScheduleWorker>();

        // Per-circuit / per-client demo session.
        services.AddScoped<ReportServerSessionState>();

        return services;
    }
}
