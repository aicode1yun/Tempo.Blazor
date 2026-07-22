using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Tempo.ReportServer.Api.Scheduling;

namespace Tempo.ReportServer.Api.Tests.Scheduling;

/// <summary>
/// [TODO] End-to-end email delivery skeleton (Fáze 6, subtask 3).
///
/// This exercises the real <see cref="SmtpScheduledReportEmailSender"/> against a running smtp4dev
/// instance and asserts, via smtp4dev's REST API, that the scheduled report email arrived with its
/// PDF attachment. It is <b>Skipped</b> because smtp4dev is not running in this environment
/// (no listener on SMTP :25/:2525 or UI :1080 at implementation time) and the full live host stack
/// (API + Web + SQL Server) is not started here — so a genuine screenshot-backed UI-to-inbox run
/// cannot be produced without fabricating results.
///
/// To run it: start smtp4dev (`smtp4dev` — SMTP on :25, web UI/API on :1080), remove the Skip, and
/// (for the full UI variant) drive the Schedules page with Playwright to create the schedule, then
/// advance the worker clock. The body below is the real, compilable transport leg of that flow.
/// </summary>
public sealed class SmtpScheduledReportDeliveryE2ESkeleton
{
    private const string Smtp4DevHost = "localhost";
    private const int Smtp4DevSmtpPort = 25;
    private const string Smtp4DevApiBase = "http://localhost:1080";

    [Fact(Skip = "E2E [TODO]: requires a running smtp4dev (SMTP :25, API :1080). See class summary.")]
    public async Task ScheduledReport_IsDeliveredToSmtp4Dev_WithPdfAttachment()
    {
        // Arrange: a PDF artifact and the SMTP sender pointed at smtp4dev.
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        var sender = new SmtpScheduledReportEmailSender(Options.Create(new ScheduledReportSmtpOptions
        {
            Host = Smtp4DevHost,
            Port = Smtp4DevSmtpPort,
            UseSsl = false,
            FromAddress = "reports@tempo.local",
            FromName = "Tempo Report Server",
        }));
        var email = new ScheduledReportEmail(
            To: ["ops@example.test"],
            Subject: "Scheduled report: Executive digest",
            HtmlBody: "<p>The scheduled report is attached.</p>",
            TextBody: "The scheduled report is attached.",
            Attachment: new ScheduledReportArtifact("executive-digest.pdf", "application/pdf", pdfBytes));

        // Act: deliver through the real SMTP transport.
        await sender.SendAsync(email);

        // Assert: smtp4dev's REST API captured the message with a PDF attachment.
        using var http = new HttpClient { BaseAddress = new Uri(Smtp4DevApiBase) };
        var messages = await http.GetFromJsonAsync<Smtp4DevMessages>("/api/Messages");
        messages.Should().NotBeNull();
        messages!.Results.Should().Contain(message =>
            message.Subject == "Scheduled report: Executive digest" && message.AttachmentCount >= 1);
    }

    private sealed record Smtp4DevMessages(IReadOnlyList<Smtp4DevMessage> Results);

    private sealed record Smtp4DevMessage(string Subject, int AttachmentCount);
}
