using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tempo.ReportServer.Api.Storage;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>Service registration for the server-tier report scheduling worker and delivery channels.</summary>
public static class ReportServerSchedulingExtensions
{
    /// <summary>
    /// Registers the persistent schedule store, the delivery channels (email/storage/webhook), the
    /// schedule processor and the hosted background worker. Options are bound from the
    /// <c>Scheduling</c>, <c>Scheduling:Smtp</c> and <c>Scheduling:Storage</c> configuration sections
    /// when <paramref name="configuration"/> is supplied.
    /// </summary>
    public static IServiceCollection AddTempoReportServerScheduling(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        if (configuration is not null)
        {
            services.Configure<ReportSchedulingOptions>(configuration.GetSection("Scheduling"));
            services.Configure<ScheduledReportSmtpOptions>(configuration.GetSection("Scheduling:Smtp"));
            services.Configure<ScheduledReportStorageOptions>(configuration.GetSection("Scheduling:Storage"));
            services.Configure<ScheduledReportWebhookOptions>(configuration.GetSection("Scheduling:Webhook"));
        }
        else
        {
            services.Configure<ReportSchedulingOptions>(_ => { });
            services.Configure<ScheduledReportSmtpOptions>(_ => { });
            services.Configure<ScheduledReportStorageOptions>(_ => { });
            services.Configure<ScheduledReportWebhookOptions>(_ => { });
        }

        services.TryAddScoped<IReportScheduleStore, EfReportScheduleStore>();
        services.TryAddScoped<IScheduledReportRenderer, ScheduledReportRenderer>();
        services.TryAddScoped<IReportScheduleProcessor, ReportScheduleProcessor>();

        // Delivery channels + router.
        services.TryAddSingleton<IScheduledReportEmailSender, SmtpScheduledReportEmailSender>();
        services.AddScoped<IScheduledReportDeliveryChannel, EmailScheduledReportDeliveryChannel>();
        services.AddScoped<IScheduledReportDeliveryChannel, StorageScheduledReportDeliveryChannel>();
        services.AddScoped<IScheduledReportDeliveryChannel, WebhookScheduledReportDeliveryChannel>();
        services.AddScoped<ScheduledReportDeliveryRouter>();
        services.AddHttpClient(WebhookScheduledReportDeliveryChannel.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
                client.Timeout = provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ScheduledReportWebhookOptions>>()
                    .Value.Timeout);

        services.AddHostedService<ReportSchedulingWorker>();
        return services;
    }
}
