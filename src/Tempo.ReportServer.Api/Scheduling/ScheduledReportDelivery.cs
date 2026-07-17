using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>A rendered report artifact produced for a scheduled run.</summary>
public sealed record ScheduledReportArtifact(string FileName, string ContentType, byte[] Bytes);

/// <summary>The unit of work handed to a delivery channel.</summary>
public sealed record ScheduledReportDelivery(
    string TenantId,
    string ScheduleId,
    string ScheduleName,
    string ReportId,
    DateTimeOffset OccurrenceUtc,
    string Target,
    ScheduledReportArtifact Artifact);

/// <summary>Delivers a rendered scheduled report through a specific transport (email, storage, webhook).</summary>
public interface IScheduledReportDeliveryChannel
{
    /// <summary>The delivery kind this channel handles.</summary>
    ReportScheduleDeliveryKind Kind { get; }

    /// <summary>Delivers the rendered report.</summary>
    Task DeliverAsync(ScheduledReportDelivery delivery, CancellationToken cancellationToken = default);
}

/// <summary>Resolves the <see cref="IScheduledReportDeliveryChannel"/> registered for a delivery kind.</summary>
public sealed class ScheduledReportDeliveryRouter
{
    private readonly IReadOnlyDictionary<ReportScheduleDeliveryKind, IScheduledReportDeliveryChannel> _channels;

    /// <summary>Creates a router over the registered channels.</summary>
    public ScheduledReportDeliveryRouter(IEnumerable<IScheduledReportDeliveryChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        _channels = channels.ToDictionary(channel => channel.Kind);
    }

    /// <summary>Delivers via the channel registered for <paramref name="kind"/>.</summary>
    public Task DeliverAsync(ReportScheduleDeliveryKind kind, ScheduledReportDelivery delivery, CancellationToken cancellationToken = default)
    {
        if (!_channels.TryGetValue(kind, out var channel))
        {
            throw new InvalidOperationException($"No delivery channel is registered for '{kind}'.");
        }

        return channel.DeliverAsync(delivery, cancellationToken);
    }
}

/// <summary>
/// Email message carrying the rendered report as an attachment. The
/// <c>EmailTemplates.Abstractions.IEmailSender</c> contract cannot carry attachments, so scheduled
/// delivery uses this attachment-aware sender abstraction instead.
/// </summary>
public sealed record ScheduledReportEmail(
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody,
    string TextBody,
    ScheduledReportArtifact Attachment);

/// <summary>Transport that sends a <see cref="ScheduledReportEmail"/> (e.g. SMTP / smtp4dev in dev).</summary>
public interface IScheduledReportEmailSender
{
    /// <summary>Sends the email with its attachment.</summary>
    Task SendAsync(ScheduledReportEmail email, CancellationToken cancellationToken = default);
}

/// <summary>SMTP options for scheduled report email delivery.</summary>
public sealed record ScheduledReportSmtpOptions
{
    /// <summary>SMTP host (smtp4dev default in dev).</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>SMTP port (smtp4dev default 25/2525).</summary>
    public int Port { get; init; } = 25;

    /// <summary>Whether to negotiate TLS.</summary>
    public bool UseSsl { get; init; }

    /// <summary>From address.</summary>
    public string FromAddress { get; init; } = "reports@tempo.local";

    /// <summary>From display name.</summary>
    public string FromName { get; init; } = "Tempo Report Server";
}

/// <summary>
/// SMTP email sender built on <see cref="System.Net.Mail.SmtpClient"/> so no extra dependency is
/// pulled into the API. Suitable for smtp4dev in development and any relay in production.
/// </summary>
public sealed class SmtpScheduledReportEmailSender : IScheduledReportEmailSender
{
    private readonly ScheduledReportSmtpOptions _options;

    /// <summary>Creates the SMTP sender.</summary>
    public SmtpScheduledReportEmailSender(IOptions<ScheduledReportSmtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task SendAsync(ScheduledReportEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = email.Subject,
            Body = email.HtmlBody,
            IsBodyHtml = true,
        };
        foreach (var recipient in email.To)
        {
            message.To.Add(recipient);
        }

        using var stream = new MemoryStream(email.Attachment.Bytes, writable: false);
        var attachment = new Attachment(stream, email.Attachment.FileName, email.Attachment.ContentType)
        {
            TransferEncoding = TransferEncoding.Base64,
        };
        message.Attachments.Add(attachment);

#pragma warning disable SYSLIB0014 // SmtpClient is adequate for smtp4dev/relay delivery in this host.
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
#pragma warning restore SYSLIB0014
        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Email delivery channel: renders the report as an attachment and hands it to the sender.</summary>
public sealed class EmailScheduledReportDeliveryChannel : IScheduledReportDeliveryChannel
{
    private readonly IScheduledReportEmailSender _sender;

    /// <summary>Creates the email channel.</summary>
    public EmailScheduledReportDeliveryChannel(IScheduledReportEmailSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <inheritdoc />
    public ReportScheduleDeliveryKind Kind => ReportScheduleDeliveryKind.Email;

    /// <inheritdoc />
    public Task DeliverAsync(ScheduledReportDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var recipients = delivery.Target
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Email delivery requires at least one recipient.");
        }

        var email = new ScheduledReportEmail(
            recipients,
            Subject: $"Scheduled report: {delivery.ScheduleName}",
            HtmlBody: $"<p>The scheduled report <strong>{WebUtility.HtmlEncode(delivery.ScheduleName)}</strong> is attached.</p>"
                + $"<p>Occurrence: {delivery.OccurrenceUtc:u}</p>",
            TextBody: $"The scheduled report {delivery.ScheduleName} is attached. Occurrence: {delivery.OccurrenceUtc:u}",
            delivery.Artifact);
        return _sender.SendAsync(email, cancellationToken);
    }
}

/// <summary>Options for the storage delivery channel.</summary>
public sealed record ScheduledReportStorageOptions
{
    /// <summary>Root directory that scheduled report artifacts are written under.</summary>
    public string RootPath { get; init; } = Path.Combine(Path.GetTempPath(), "tempo-report-server", "scheduled");
}

/// <summary>Storage delivery channel: writes the rendered artifact to a per-tenant/target directory.</summary>
public sealed class StorageScheduledReportDeliveryChannel : IScheduledReportDeliveryChannel
{
    private readonly ScheduledReportStorageOptions _options;

    /// <summary>Creates the storage channel.</summary>
    public StorageScheduledReportDeliveryChannel(IOptions<ScheduledReportStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public ReportScheduleDeliveryKind Kind => ReportScheduleDeliveryKind.Storage;

    /// <inheritdoc />
    public async Task DeliverAsync(ScheduledReportDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var subFolder = string.IsNullOrWhiteSpace(delivery.Target) ? delivery.ScheduleId : delivery.Target;
        var directory = Path.Combine(_options.RootPath, Sanitize(delivery.TenantId), Sanitize(subFolder));
        Directory.CreateDirectory(directory);
        var fileName = $"{delivery.OccurrenceUtc:yyyyMMddHHmmss}-{delivery.Artifact.FileName}";
        var path = Path.Combine(directory, Sanitize(fileName));
        await File.WriteAllBytesAsync(path, delivery.Artifact.Bytes, cancellationToken).ConfigureAwait(false);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }
}

/// <summary>Webhook delivery channel: POSTs the rendered artifact to the target URL.</summary>
public sealed class WebhookScheduledReportDeliveryChannel : IScheduledReportDeliveryChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookScheduledReportDeliveryChannel> _logger;
    private readonly ScheduledReportWebhookOptions _options;

    /// <summary>HTTP client name used for webhook delivery.</summary>
    public const string HttpClientName = "ScheduledReportWebhook";

    /// <summary>Creates the webhook channel.</summary>
    public WebhookScheduledReportDeliveryChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookScheduledReportDeliveryChannel> logger,
        IOptions<ScheduledReportWebhookOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    }

    /// <inheritdoc />
    public ReportScheduleDeliveryKind Kind => ReportScheduleDeliveryKind.Webhook;

    /// <inheritdoc />
    public async Task DeliverAsync(ScheduledReportDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        // SSRF guard: reject disallowed schemes and non-public targets before any outbound request.
        var uri = ScheduledReportWebhookGuard.Validate(delivery.Target, _options);

        using var content = new ByteArrayContent(delivery.Artifact.Bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(delivery.Artifact.ContentType);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = delivery.Artifact.FileName,
        };
        content.Headers.Add("X-Tempo-Schedule-Id", delivery.ScheduleId);
        content.Headers.Add("X-Tempo-Tenant-Id", delivery.TenantId);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Webhook delivery for schedule {ScheduleId} returned {StatusCode}.",
                delivery.ScheduleId,
                (int)response.StatusCode);
            throw new InvalidOperationException($"Webhook returned status {(int)response.StatusCode}.");
        }
    }
}
