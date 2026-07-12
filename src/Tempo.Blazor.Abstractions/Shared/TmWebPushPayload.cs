namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Payload delivered to a browser via Web Push and rendered by the service worker.</summary>
public sealed class TmWebPushPayload
{
    /// <summary>Notification title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Notification body text.</summary>
    public string? Body { get; set; }

    /// <summary>URL opened when the notification is clicked.</summary>
    public string? Url { get; set; }

    /// <summary>Icon URL shown in the notification.</summary>
    public string? Icon { get; set; }

    /// <summary>Badge URL (monochrome) shown on some platforms.</summary>
    public string? Badge { get; set; }

    /// <summary>Grouping tag; a later push with the same tag replaces the earlier one.</summary>
    public string? Tag { get; set; }

    /// <summary>Correlating notification id, forwarded to the client.</summary>
    public string? NotificationId { get; set; }

    /// <summary>Builds a payload from a notification.</summary>
    public static TmWebPushPayload FromNotification(TmNotification notification) => new()
    {
        Title = notification.Title,
        Body = notification.Body,
        Url = notification.ActionUrl,
        Tag = notification.CorrelationId ?? notification.Type,
        NotificationId = notification.Id
    };
}
