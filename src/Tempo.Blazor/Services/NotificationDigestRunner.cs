using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Services;

/// <summary>
/// Runs one notification-digest pass: for each recipient it queries the notification service,
/// builds a digest, and hands it to an <see cref="INotificationDigestSender"/>. Has no hosting
/// dependency so it is fully unit-testable; a hosted <c>BackgroundService</c> wraps it with a timer.
/// </summary>
public sealed class NotificationDigestRunner
{
    private readonly ITmNotificationService _notifications;
    private readonly INotificationRecipientSource _recipients;
    private readonly INotificationDigestSender _sender;
    private readonly TmNotificationDigestOptions _options;

    /// <summary>Creates a runner over the given collaborators.</summary>
    public NotificationDigestRunner(
        ITmNotificationService notifications,
        INotificationRecipientSource recipients,
        INotificationDigestSender sender,
        TmNotificationDigestOptions? options = null)
    {
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _recipients = recipients ?? throw new ArgumentNullException(nameof(recipients));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _options = options ?? new TmNotificationDigestOptions();
    }

    /// <summary>Runs one digest pass over <paramref name="periodStart"/>..<paramref name="periodEnd"/>.</summary>
    /// <returns>The digests that were sent.</returns>
    public async Task<IReadOnlyList<TmNotificationDigest>> RunOnceAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        var sent = new List<TmNotificationDigest>();
        var recipients = await _recipients.GetRecipientsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(recipient.Id)) continue;

            var notifications = await _notifications.GetNotificationsAsync(new TmNotificationQuery
            {
                RecipientUserId = recipient.Id,
                IncludeRead = !_options.UnreadOnly,
                Take = 200
            }, cancellationToken).ConfigureAwait(false);

            var digest = TmNotificationDigestBuilder.Build(recipient, notifications, _options, periodStart, periodEnd);
            if (digest is null) continue;

            await _sender.SendAsync(digest, cancellationToken).ConfigureAwait(false);
            sent.Add(digest);
        }

        return sent;
    }
}
