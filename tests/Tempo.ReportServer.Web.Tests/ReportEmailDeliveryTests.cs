using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;
using Tempo.Blazor.EmailTemplates.Abstractions.Templating;
using Tempo.Reporting.Abstractions.Data;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

public sealed class ReportEmailDeliveryTests
{
    [Fact]
    public async Task DeliverAsync_RendersGalleryTemplateAndAttachesPdfReport()
    {
        var clock = new ManualReportScheduleClock(new DateTimeOffset(2026, 6, 22, 8, 0, 0, TimeSpan.Zero));
        var sender = new CapturingEmailSender();
        var outbox = new ReportEmailOutbox();
        var service = new ReportEmailDeliveryService(
            new ReportEmailTemplateGalleryStore(),
            CreateRenderer(),
            sender,
            new DemoReportSourceFactory(),
            new ReportServerCatalogStore(),
            outbox,
            clock);
        var job = new ScheduledReportJob
        {
            JobId = "job-1",
            ScheduleId = "sales-dashboard-digest",
            TenantId = "northwind",
            UserId = "pavel.author",
            ReportId = "sales-dashboard",
            Format = ReportScheduleOutputFormat.Pdf,
            EmailTemplateId = ReportEmailTemplateGalleryStore.ReportDigestTemplateId,
            CultureName = "en-US",
            Recipients = [new ReportScheduleRecipient("finance@example.test", "Finance")],
            Parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
            {
                ["Region"] = ReportParameterValue.Scalar("EU"),
                ["MinimumTotal"] = ReportParameterValue.Scalar(0),
                ["IncludeClosed"] = ReportParameterValue.Scalar(true),
            },
            QueuedAtUtc = clock.UtcNow,
            DueAtUtc = clock.UtcNow,
        };

        var delivered = await service.DeliverAsync(job);

        sender.Messages.Should().ContainSingle();
        sender.Messages[0].Subject.Should().Contain("Dashboard prodejů");
        sender.Messages[0].Html.Should().Contain("sales-dashboard.pdf");
        delivered.Attachments.Should().ContainSingle();
        delivered.Attachments[0].FileName.Should().Be("sales-dashboard.pdf");
        delivered.Attachments[0].Bytes.Take(4).Should().Equal(0x25, 0x50, 0x44, 0x46);
        outbox.Messages.Should().ContainSingle().Which.Message.Transport.Should().Be("smtp4dev://localhost:2525");
    }

    private static IEmailTemplateRenderer CreateRenderer()
        => new EmailTemplateRenderer(
            new ScribanTemplateEngine(),
            new MjmlGenerator(),
            new MjmlNetCompiler(),
            new TextVersionGenerator());

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
