using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Sends a notification digest as an HTML email via the existing <see cref="IEmailSender"/>
/// (SMTP → smtp4dev in the demo).
/// </summary>
public sealed class SmtpNotificationDigestSender : INotificationDigestSender
{
    private readonly IEmailSender _email;

    public SmtpNotificationDigestSender(IEmailSender email) => _email = email;

    public async Task SendAsync(TmNotificationDigest digest, CancellationToken cancellationToken = default)
    {
        var to = digest.RecipientEmail;
        if (string.IsNullOrWhiteSpace(to))
        {
            return; // no address to deliver to
        }

        var subject = $"Your notification digest — {digest.TotalCount} update{(digest.TotalCount == 1 ? "" : "s")}";
        var message = new EmailMessage(
            From: null,
            To: [to],
            Cc: [],
            Subject: subject,
            Html: BuildHtml(digest),
            Text: BuildText(digest));

        await _email.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildHtml(TmNotificationDigest digest)
    {
        var rows = string.Concat(digest.Items.Select(n =>
            $"<tr><td style=\"padding:6px 10px;border-bottom:1px solid #eee\"><strong>{Escape(n.Title)}</strong>" +
            (string.IsNullOrEmpty(n.Body) ? "" : $"<br><span style=\"color:#666\">{Escape(n.Body)}</span>") +
            $"</td><td style=\"padding:6px 10px;border-bottom:1px solid #eee;color:#888;white-space:nowrap\">{n.CreatedAt.ToLocalTime():dd.MM HH:mm}</td></tr>"));

        return $"<div data-testid=\"digest-email\" style=\"font-family:sans-serif;max-width:600px\">" +
               $"<h2>You have {digest.TotalCount} new notification{(digest.TotalCount == 1 ? "" : "s")}</h2>" +
               $"<table style=\"border-collapse:collapse;width:100%\">{rows}</table>" +
               $"<p style=\"color:#888;font-size:12px\">Digest generated {digest.GeneratedAt.ToLocalTime():dd.MM.yyyy HH:mm}</p></div>";
    }

    private static string BuildText(TmNotificationDigest digest)
        => $"You have {digest.TotalCount} new notifications:\n"
         + string.Join("\n", digest.Items.Select(n => $"- {n.Title}"));

    private static string Escape(string? s)
        => (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
