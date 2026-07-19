using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>
/// Production <see cref="IScheduledReportEmailSender"/> built on MailKit. Replaces the
/// <see cref="System.Net.Mail.SmtpClient"/> transport (which is <c>SYSLIB0014</c>-obsolete) with the
/// maintained MailKit/MimeKit stack. Works against a plain-SMTP dev server (smtp4dev on :2525) as well
/// as an authenticated STARTTLS relay in production.
/// </summary>
public sealed class MailKitScheduledReportEmailSender : IScheduledReportEmailSender
{
    private readonly ScheduledReportSmtpOptions _options;

    /// <summary>Creates the MailKit sender.</summary>
    public MailKitScheduledReportEmailSender(IOptions<ScheduledReportSmtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task SendAsync(ScheduledReportEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        using var message = BuildMessage(email, _options);
        using var client = new SmtpClient();

        // UseSsl selects STARTTLS negotiation for a relay; plain SMTP (smtp4dev) connects without TLS.
        var security = _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the <see cref="MimeMessage"/> for a scheduled report email — from/to, subject, HTML+text
    /// bodies and the rendered report attachment. Exposed so the message shape is unit-testable without a
    /// live SMTP server.
    /// </summary>
    public static MimeMessage BuildMessage(ScheduledReportEmail email, ScheduledReportSmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(options);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        foreach (var recipient in email.To)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = email.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody,
        };
        var contentType = ContentType.Parse(email.Attachment.ContentType);
        body.Attachments.Add(email.Attachment.FileName, email.Attachment.Bytes, contentType);
        message.Body = body.ToMessageBody();

        return message;
    }
}
