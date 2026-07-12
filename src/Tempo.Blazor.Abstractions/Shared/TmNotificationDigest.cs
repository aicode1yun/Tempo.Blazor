namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>A per-recipient summary of notifications accumulated over a period, for a digest email.</summary>
public sealed class TmNotificationDigest
{
    /// <summary>Recipient user id.</summary>
    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>Recipient snapshot, when available (used for the email address / display name).</summary>
    public TmUserRef? Recipient { get; set; }

    /// <summary>When the digest was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Start of the period covered by the digest.</summary>
    public DateTimeOffset PeriodStart { get; set; }

    /// <summary>End of the period covered by the digest.</summary>
    public DateTimeOffset PeriodEnd { get; set; }

    /// <summary>Notifications included in the digest, newest first.</summary>
    public IReadOnlyList<TmNotification> Items { get; set; } = [];

    /// <summary>Total number of notifications in the digest.</summary>
    public int TotalCount => Items.Count;

    /// <summary>Notification counts grouped by <see cref="TmNotification.Type"/>.</summary>
    public IReadOnlyDictionary<string, int> CountsByType
        => Items.GroupBy(n => string.IsNullOrWhiteSpace(n.Type) ? "other" : n.Type, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Best available recipient email address.</summary>
    public string? RecipientEmail => Recipient?.Email;
}
