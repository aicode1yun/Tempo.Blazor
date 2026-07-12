namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared persistent notification model for Tempo components.</summary>
public sealed class TmNotification
{
    /// <summary>Stable notification identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Recipient snapshot, when available.</summary>
    public TmUserRef? Recipient { get; set; }

    /// <summary>Recipient user id for hosts that do not have a full user snapshot.</summary>
    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>User that caused the notification, when known.</summary>
    public TmUserRef? Actor { get; set; }

    /// <summary>Notification type key, for example <c>mention</c> or <c>page-edited</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Short notification title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional detail body.</summary>
    public string? Body { get; set; }

    /// <summary>Severity used by notification UI.</summary>
    public TmNotificationSeverity Severity { get; set; } = TmNotificationSeverity.Info;

    /// <summary>UTC timestamp when the notification was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the notification was delivered to the recipient's client
    /// (a push/realtime channel acknowledged receipt). Distinct from <see cref="ReadAt"/>.</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>UTC timestamp when the recipient read the notification.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>Optional URL opened when the notification is clicked.</summary>
    public string? ActionUrl { get; set; }

    /// <summary>Optional entity related to the notification.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional correlation identifier used to group related notifications.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Returns true when the notification has been read.</summary>
    public bool IsRead => ReadAt.HasValue;

    /// <summary>Returns true when the notification has been delivered to the recipient's client.</summary>
    public bool IsDelivered => DeliveredAt.HasValue;

    /// <summary>Returns the best available recipient user id.</summary>
    public string EffectiveRecipientUserId
        => string.IsNullOrWhiteSpace(RecipientUserId) ? Recipient?.Id ?? string.Empty : RecipientUserId;

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(EffectiveRecipientUserId)
        && !string.IsNullOrWhiteSpace(Type)
        && !string.IsNullOrWhiteSpace(Title);
}
