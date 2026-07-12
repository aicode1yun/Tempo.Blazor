namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Operations supported by an <see cref="ITmNotificationService"/>.</summary>
[Flags]
public enum TmNotificationServiceCapabilities
{
    /// <summary>No optional operations are supported.</summary>
    None = 0,

    /// <summary>Service can publish notifications.</summary>
    Publish = 1 << 0,

    /// <summary>Service can read notification lists.</summary>
    Read = 1 << 1,

    /// <summary>Service can query notifications by filters beyond recipient and paging.</summary>
    Query = 1 << 2,

    /// <summary>Service can return unread counts.</summary>
    UnreadCount = 1 << 3,

    /// <summary>Service can mutate read state.</summary>
    ReadState = 1 << 4,

    /// <summary>Service can record delivery acknowledgements (DeliveredAt), distinct from read state.</summary>
    DeliveryAck = 1 << 5,

    /// <summary>Service pushes new notifications to clients in real time (e.g. SignalR).</summary>
    RealtimePush = 1 << 6
}
