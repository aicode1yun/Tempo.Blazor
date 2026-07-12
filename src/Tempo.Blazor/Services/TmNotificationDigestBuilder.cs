using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Services;

/// <summary>Builds a <see cref="TmNotificationDigest"/> from a recipient's notifications (pure, testable).</summary>
public static class TmNotificationDigestBuilder
{
    /// <summary>
    /// Produces a digest for <paramref name="recipient"/> over the given period, or <c>null</c>
    /// when fewer than <see cref="TmNotificationDigestOptions.MinItems"/> notifications qualify.
    /// </summary>
    public static TmNotificationDigest? Build(
        TmUserRef recipient,
        IReadOnlyList<TmNotification> notifications,
        TmNotificationDigestOptions options,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(options);

        var items = notifications
            .Where(n => n.CreatedAt >= periodStart && n.CreatedAt <= periodEnd)
            .Where(n => !options.UnreadOnly || !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        if (items.Count < Math.Max(1, options.MinItems))
        {
            return null;
        }

        return new TmNotificationDigest
        {
            RecipientUserId = string.IsNullOrWhiteSpace(recipient.Id) ? items[0].EffectiveRecipientUserId : recipient.Id,
            Recipient = recipient,
            GeneratedAt = periodEnd,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Items = items
        };
    }
}
