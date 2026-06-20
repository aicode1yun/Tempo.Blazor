using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Tempo.Blazor.EmailTemplates.Abstractions.Contracts;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// MailKit-backed <see cref="IEmailSender"/>. Builds a multipart HTML+text message and sends it with
/// a bounded retry (exponential backoff) on transient SMTP failures.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly ISmtpClientFactory _factory;
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>Initializes the sender.</summary>
    public SmtpEmailSender(ISmtpClientFactory factory, IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = BuildMimeMessage(message);
        var security = ParseSecurity(_options.Security);

        using var client = _factory.Create();
        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);

        await SendWithRetryAsync(client, mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private async Task SendWithRetryAsync(ISmtpClientWrapper client, MimeMessage message, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await client.SendAsync(message, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxRetries && IsTransient(ex))
            {
                var delay = _options.RetryDelay * Math.Pow(2, attempt - 1);
                _logger.LogWarning(ex, "SMTP send failed (attempt {Attempt}/{Max}); retrying in {Delay}.",
                    attempt, _options.MaxRetries, delay);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(string.IsNullOrEmpty(message.From)
            ? new MailboxAddress(_options.FromName, _options.FromAddress)
            : MailboxAddress.Parse(message.From));
        foreach (var to in message.To) mime.To.Add(MailboxAddress.Parse(to));
        foreach (var cc in message.Cc) mime.Cc.Add(MailboxAddress.Parse(cc));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();
        return mime;
    }

    private static SecureSocketOptions ParseSecurity(string security) => security switch
    {
        "StartTls" => SecureSocketOptions.StartTls,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.None,
    };

    private static bool IsTransient(Exception ex)
        => ex is IOException or TimeoutException or MailKit.Net.Smtp.SmtpProtocolException;
}
