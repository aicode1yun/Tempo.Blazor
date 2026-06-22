using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Services;

/// <summary>Default no-op implementation of <see cref="ITmNotificationService"/>. Does nothing.</summary>
public class NoOpNotificationService : ITmNotificationService
{
    /// <inheritdoc />
    public event Action? OnChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public TmNotificationServiceCapabilities Capabilities => TmNotificationServiceCapabilities.None;

    TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;

    /// <inheritdoc />
    public Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken cancellationToken = default)
        => Task.FromResult(notification);

    /// <inheritdoc />
    public Task MarkAsReadAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task MarkAllAsReadAsync(string recipientUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(TmNotificationQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TmNotification>>(Array.Empty<TmNotification>());

    /// <inheritdoc />
    public Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
