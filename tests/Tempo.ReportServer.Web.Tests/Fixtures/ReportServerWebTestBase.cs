using Bunit;
using Tempo.Blazor.EmailTemplates.Abstractions;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Reporting.Configuration;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests.Fixtures;

public abstract class ReportServerWebTestBase : TestContext
{
    protected ReportServerWebTestBase()
    {
        Services.AddTempoBlazorReporting();
        Services.AddTempoEmailTemplateEngine();
        Services.AddSingleton<DemoReportSourceFactory>();
        Services.AddSingleton<ReportServerCatalogStore>();
        Services.AddSingleton<IReportScheduleClock, SystemReportScheduleClock>();
        Services.AddSingleton<ReportScheduleStore>();
        Services.AddSingleton<ReportRenderJobQueue>();
        Services.AddSingleton<ReportEmailOutbox>();
        Services.AddSingleton<ReportEmailTemplateGalleryStore>();
        Services.AddSingleton<IEmailTemplateStore>(sp => sp.GetRequiredService<ReportEmailTemplateGalleryStore>());
        Services.AddSingleton<IEmailSender, Smtp4DevEmailSender>();
        Services.AddSingleton<IReportScheduledDeliveryService, ReportEmailDeliveryService>();
        Services.AddSingleton<ReportScheduleWorker>();
        Services.AddScoped<ReportServerSessionState>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected ReportServerSessionState SignIn(string userName = "Pavel Author")
    {
        var session = Services.GetRequiredService<ReportServerSessionState>();
        session.SignIn(userName);
        return session;
    }
}
