using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.Notifications;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Server-side <see cref="ITmNotificationService"/> that decorates an inner store and, on publish
/// or read-state change, pushes to the recipient's <see cref="TmNotificationHub"/> group so open
/// clients update live. Reads delegate straight to the inner store.
/// </summary>
public sealed class SignalRNotificationBroadcaster : ITmNotificationService
{
    private readonly ITmNotificationService _inner;
    private readonly IHubContext<TmNotificationHub> _hub;

    public SignalRNotificationBroadcaster(ITmNotificationService inner, IHubContext<TmNotificationHub> hub)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    public event Action? OnChanged
    {
        add => _inner.OnChanged += value;
        remove => _inner.OnChanged -= value;
    }

    public TmNotificationServiceCapabilities Capabilities
        => _inner.Capabilities | TmNotificationServiceCapabilities.RealtimePush;

    TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;

    public async Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken cancellationToken = default)
    {
        var saved = await _inner.PublishAsync(notification, cancellationToken).ConfigureAwait(false);
        await _hub.Clients
            .Group(SignalRNotificationService.GroupName(saved.EffectiveRecipientUserId))
            .SendAsync(SignalRNotificationService.HubMethods.NotificationReceived, saved, cancellationToken)
            .ConfigureAwait(false);
        return saved;
    }

    public Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(TmNotificationQuery query, CancellationToken cancellationToken = default)
        => _inner.GetNotificationsAsync(query, cancellationToken);

    public Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken cancellationToken = default)
        => _inner.GetUnreadCountAsync(recipientUserId, cancellationToken);

    public async Task MarkAsReadAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
    {
        await _inner.MarkAsReadAsync(notificationId, recipientUserId, cancellationToken).ConfigureAwait(false);
        await PushChangedAsync(recipientUserId, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkAllAsReadAsync(string recipientUserId, CancellationToken cancellationToken = default)
    {
        await _inner.MarkAllAsReadAsync(recipientUserId, cancellationToken).ConfigureAwait(false);
        await PushChangedAsync(recipientUserId, cancellationToken).ConfigureAwait(false);
    }

    public Task MarkAsDeliveredAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
        => _inner.MarkAsDeliveredAsync(notificationId, recipientUserId, cancellationToken);

    private Task PushChangedAsync(string recipientUserId, CancellationToken cancellationToken)
        => _hub.Clients
            .Group(SignalRNotificationService.GroupName(recipientUserId))
            .SendAsync(SignalRNotificationService.HubMethods.NotificationsChanged, cancellationToken);
}
