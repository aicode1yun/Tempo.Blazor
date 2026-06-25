namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Query options for resolving shared notifications.</summary>
public sealed class TmNotificationQuery
{
    /// <summary>Recipient user id whose notifications should be returned.</summary>
    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>Optional exact entity reference filter.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional entity type filter.</summary>
    public string? EntityType { get; set; }

    /// <summary>Optional entity id filter.</summary>
    public string? EntityId { get; set; }

    /// <summary>Optional notification type key filter.</summary>
    public string? Type { get; set; }

    /// <summary>Optional free-text filter matched against title, body, and actor fields.</summary>
    public string? SearchText { get; set; }

    /// <summary>When false, only unread notifications are returned.</summary>
    public bool IncludeRead { get; set; } = true;

    /// <summary>Number of matching notifications to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of matching notifications to return.</summary>
    public int Take { get; set; } = 20;
}
