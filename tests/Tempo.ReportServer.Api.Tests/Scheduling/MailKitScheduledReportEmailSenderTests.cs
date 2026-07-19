using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MimeKit;
using Tempo.ReportServer.Api.Scheduling;

namespace Tempo.ReportServer.Api.Tests.Scheduling;

/// <summary>
/// Unit specification for the MailKit sender's message construction. Asserts the built
/// <see cref="MimeMessage"/> (from/to/subject/bodies + PDF attachment) without needing a live SMTP
/// server, so the production transport shape is verified deterministically.
/// </summary>
public sealed class MailKitScheduledReportEmailSenderTests
{
    private static readonly ScheduledReportSmtpOptions Options = new()
    {
        FromAddress = "reports@tempo.local",
        FromName = "Tempo Report Server",
    };

    private static ScheduledReportEmail SampleEmail() => new(
        To: ["ops@example.test", "audit@example.test"],
        Subject: "Scheduled report: Executive digest",
        HtmlBody: "<p>The scheduled report is attached.</p>",
        TextBody: "The scheduled report is attached.",
        Attachment: new ScheduledReportArtifact("executive-digest.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]));

    [Fact]
    public void BuildMessage_SetsFromToAndSubject()
    {
        using var message = MailKitScheduledReportEmailSender.BuildMessage(SampleEmail(), Options);

        message.Subject.Should().Be("Scheduled report: Executive digest");
        message.From.Mailboxes.Should().ContainSingle()
            .Which.Address.Should().Be("reports@tempo.local");
        message.To.Mailboxes.Select(m => m.Address)
            .Should().BeEquivalentTo(["ops@example.test", "audit@example.test"]);
    }

    [Fact]
    public void BuildMessage_AttachesPdf_WithContentTypeAndFileName()
    {
        using var message = MailKitScheduledReportEmailSender.BuildMessage(SampleEmail(), Options);

        var attachment = message.Attachments.OfType<MimePart>().Single();
        attachment.ContentType.MimeType.Should().Be("application/pdf");
        attachment.FileName.Should().Be("executive-digest.pdf");
    }

    [Fact]
    public void BuildMessage_CarriesHtmlAndTextBodies()
    {
        using var message = MailKitScheduledReportEmailSender.BuildMessage(SampleEmail(), Options);

        message.HtmlBody.Should().Contain("The scheduled report is attached.");
        message.TextBody.Should().Be("The scheduled report is attached.");
    }

    /// <summary>
    /// Live verification that the MailKit sender delivers to a plain-SMTP smtp4dev (:2525) and the
    /// message — including its PDF attachment — is captured by smtp4dev's REST API. Opt in with
    /// <c>REPORTSERVER_SMTP4DEV=1</c> (SMTP :2525, web/REST :5050); skipped otherwise so the normal suite
    /// stays hermetic. This is the production transport leg the F13 full-stack scheduling E2E relies on.
    /// </summary>
    [Fact]
    public async Task Send_ViaMailKit_ToSmtp4Dev_IsReceivedWithPdfAttachment()
    {
        if (Environment.GetEnvironmentVariable("REPORTSERVER_SMTP4DEV") != "1")
        {
            return;
        }

        var subject = $"MailKit smtp4dev check {Guid.NewGuid():N}";
        var sender = new MailKitScheduledReportEmailSender(Microsoft.Extensions.Options.Options.Create(new ScheduledReportSmtpOptions
        {
            Host = "localhost",
            Port = 2525,
            UseSsl = false,
            FromAddress = "reports@tempo.local",
            FromName = "Tempo Report Server",
        }));
        var email = new ScheduledReportEmail(
            To: ["ops@example.test"],
            Subject: subject,
            HtmlBody: "<p>The scheduled report is attached.</p>",
            TextBody: "The scheduled report is attached.",
            Attachment: new ScheduledReportArtifact("executive-digest.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]));

        await sender.SendAsync(email);

        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5050") };
        Smtp4DevMessages? messages = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            messages = await http.GetFromJsonAsync<Smtp4DevMessages>(
                $"/api/Messages?pageSize=50&sortColumn=receivedDate&sortIsDescending=true");
            if (messages?.Results.Any(m => m.Subject == subject) == true)
            {
                break;
            }

            await Task.Delay(250);
        }

        messages.Should().NotBeNull();
        messages!.Results.Should().Contain(m => m.Subject == subject && m.AttachmentCount >= 1);
    }

    private sealed record Smtp4DevMessages(IReadOnlyList<Smtp4DevMessage> Results);

    private sealed record Smtp4DevMessage(string Subject, int AttachmentCount);
}
