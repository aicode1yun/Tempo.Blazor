namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared service contract for persistent user notifications.</summary>
public interface ITmNotificationService : ITmCapabilityProvider<TmNotificationServiceCapabilities>
{
    /// <summary>Raised after notifications or read state change.</summary>
    event Action? OnChanged;

    /// <summary>Operations this service supports.</summary>
    new TmNotificationServiceCapabilities Capabilities { get; }

    /// <summary>Publishes a notification.</summary>
    /// <param name="notification">Notification to publish.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmNotification> PublishAsync(
        TmNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>Gets notifications for a recipient.</summary>
    /// <param name="query">Query and paging options.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(
        TmNotificationQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Gets unread notification count for a recipient.</summary>
    /// <param name="recipientUserId">Recipient user id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<int> GetUnreadCountAsync(
        string recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks one notification as read.</summary>
    /// <param name="notificationId">Notification id.</param>
    /// <param name="recipientUserId">Recipient user id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task MarkAsReadAsync(
        string notificationId,
        string recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks all notifications for a recipient as read.</summary>
    /// <param name="recipientUserId">Recipient user id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task MarkAllAsReadAsync(
        string recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a notification was delivered to the recipient's client (sets DeliveredAt),
    /// distinct from marking it read. Optional — services advertise support via
    /// <see cref="TmNotificationServiceCapabilities.DeliveryAck"/>. The default is a no-op so
    /// existing implementations keep working unchanged.
    /// </summary>
    /// <param name="notificationId">Notification id.</param>
    /// <param name="recipientUserId">Recipient user id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task MarkAsDeliveredAsync(
        string notificationId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
