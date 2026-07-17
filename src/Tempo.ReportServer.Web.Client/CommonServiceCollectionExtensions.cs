using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.EmailTemplates.Abstractions;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;
using Tempo.Blazor.Reporting.Configuration;
using Tempo.ReportServer.Web.Services;
using Tempo.Reporting.Abstractions.Dtos;

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

        // Typed Report Server API client, registered symmetrically for both runtimes. The base URL
        // comes from "Api:BaseUrl" (present in both hosts' appsettings) — the API is a different
        // origin by definition. The client is constructed in the *consuming* scope, so it safely
        // resolves that scope's IAccessTokenProvider (ServerAccessTokenProvider on the host,
        // WasmAccessTokenProvider in the browser) and attaches the bearer per request via
        // ApiClientBase. Registered only when configured so the self-contained demo keeps running.
        var apiBaseUrl = configuration["Api:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            services.AddHttpClient<ITempoReportServerClient, TempoReportServerClient>(
                client => client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute));
        }

        return services;
    }
}
