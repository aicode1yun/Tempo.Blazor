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

        // One stable lease-owner identity per worker process, shared by every scoped processor so the
        // atomic schedule claim attributes ownership consistently across polls.
        services.TryAddSingleton<ReportSchedulingInstanceIdentity>();

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

        // Email transport. MailKit is the default production sender (System.Net.Mail.SmtpClient is
        // SYSLIB0014-obsolete and kept only for the opt-in "SystemNetMail" provider). Both bind the same
        // Scheduling:Smtp options and deliver to a plain-SMTP smtp4dev in dev as well as a relay in prod.
        var emailProvider = configuration?["Scheduling:Email:Provider"];
        if (string.Equals(emailProvider, "SystemNetMail", StringComparison.OrdinalIgnoreCase)
            || string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IScheduledReportEmailSender, SmtpScheduledReportEmailSender>();
        }
        else
        {
            services.TryAddSingleton<IScheduledReportEmailSender, MailKitScheduledReportEmailSender>();
        }
        services.AddScoped<IScheduledReportDeliveryChannel, EmailScheduledReportDeliveryChannel>();
        services.AddScoped<IScheduledReportDeliveryChannel, StorageScheduledReportDeliveryChannel>();
        services.AddScoped<IScheduledReportDeliveryChannel, WebhookScheduledReportDeliveryChannel>();
        services.AddScoped<ScheduledReportDeliveryRouter>();
        services.AddHttpClient(WebhookScheduledReportDeliveryChannel.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
                client.Timeout = provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ScheduledReportWebhookOptions>>()
                    .Value.Timeout)
            // Close the DNS-rebinding TOCTOU: the primary handler resolves the target once, validates
            // every returned address, and pins the socket to a validated address instead of letting the
            // handler perform a fresh (rebindable) DNS lookup at connect time. See ScheduledReportWebhookConnector.
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var options = provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ScheduledReportWebhookOptions>>()
                    .Value;
                return new System.Net.Http.SocketsHttpHandler
                {
                    ConnectCallback = (context, cancellationToken) => ScheduledReportWebhookConnector.ConnectValidatedAsync(
                        context.DnsEndPoint.Host,
                        context.DnsEndPoint.Port,
                        options,
                        host => System.Net.Dns.GetHostAddresses(host),
                        ScheduledReportWebhookConnector.ConnectSocketAsync,
                        cancellationToken),
                };
            });

        services.AddHostedService<ReportSchedulingWorker>();
        return services;
    }
}
