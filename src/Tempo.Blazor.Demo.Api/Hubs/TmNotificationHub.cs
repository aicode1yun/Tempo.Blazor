using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Notifications;

namespace Tempo.Blazor.Demo.Api.Hubs;

/// <summary>
/// SignalR hub for real-time user notifications. Clients join their per-user group and receive
/// <c>NotificationReceived</c> / <c>NotificationsChanged</c> pushes; reads and writes delegate to
/// the server-side <see cref="ITmNotificationService"/> (a broadcaster decorating the store).
/// Method names mirror <see cref="SignalRNotificationService.HubMethods"/>.
/// </summary>
public sealed class TmNotificationHub : Hub
{
    private readonly ITmNotificationService _service;

    public TmNotificationHub(ITmNotificationService service) => _service = service;

    /// <summary>Subscribes the connection to a user's notification group.</summary>
    public Task JoinUser(string userId)
        => Groups.AddToGroupAsync(Context.ConnectionId, SignalRNotificationService.GroupName(userId));

    /// <summary>Unsubscribes the connection from a user's notification group.</summary>
    public Task LeaveUser(string userId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRNotificationService.GroupName(userId));

    /// <summary>Publishes a notification (broadcaster pushes it to the recipient group).</summary>
    public Task<TmNotification> Publish(TmNotification notification)
        => _service.PublishAsync(notification);

    /// <summary>Queries notifications for a recipient.</summary>
    public Task<IReadOnlyList<TmNotification>> GetNotifications(TmNotificationQuery query)
        => _service.GetNotificationsAsync(query);

    /// <summary>Returns the unread count for a recipient.</summary>
    public Task<int> GetUnreadCount(string userId)
        => _service.GetUnreadCountAsync(userId);

    /// <summary>Marks one notification as read.</summary>
    public Task MarkAsRead(string notificationId, string userId)
        => _service.MarkAsReadAsync(notificationId, userId);

    /// <summary>Marks all notifications as read.</summary>
    public Task MarkAllAsRead(string userId)
        => _service.MarkAllAsReadAsync(userId);

    /// <summary>Records a delivery acknowledgement.</summary>
    public Task MarkAsDelivered(string notificationId, string userId)
        => _service.MarkAsDeliveredAsync(notificationId, userId);
}
