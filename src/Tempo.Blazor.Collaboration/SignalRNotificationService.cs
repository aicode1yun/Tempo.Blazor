using Microsoft.AspNetCore.SignalR.Client;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Notifications;

/// <summary>
/// SignalR-backed <see cref="ITmNotificationService"/>. The client joins its per-user group and
/// receives <c>NotificationReceived</c> / <c>NotificationsChanged</c> pushes so a notification bell
/// updates in real time; reads and writes go through hub invocations. Mirrors the dual-mode design
/// of the collaboration providers: pass an in-process transport (tests / single process) or a hub
/// URL / <see cref="HubConnection"/> for real SignalR.
/// </summary>
public sealed class SignalRNotificationService : ITmNotificationService, IAsyncDisposable
{
    /// <summary>Hub method and client-event names shared by the client and the server hub.</summary>
    public static class HubMethods
    {
        /// <summary>Joins the caller's per-user notification group.</summary>
        public const string JoinUser = nameof(JoinUser);

        /// <summary>Leaves the caller's per-user notification group.</summary>
        public const string LeaveUser = nameof(LeaveUser);

        /// <summary>Publishes a notification.</summary>
        public const string Publish = nameof(Publish);

        /// <summary>Queries notifications for a recipient.</summary>
        public const string GetNotifications = nameof(GetNotifications);

        /// <summary>Gets the unread count for a recipient.</summary>
        public const string GetUnreadCount = nameof(GetUnreadCount);

        /// <summary>Marks one notification as read.</summary>
        public const string MarkAsRead = nameof(MarkAsRead);

        /// <summary>Marks all notifications as read.</summary>
        public const string MarkAllAsRead = nameof(MarkAllAsRead);

        /// <summary>Records a delivery acknowledgement.</summary>
        public const string MarkAsDelivered = nameof(MarkAsDelivered);

        /// <summary>Client event carrying a newly published notification.</summary>
        public const string NotificationReceived = nameof(NotificationReceived);

        /// <summary>Client event signalling that read/unread state changed.</summary>
        public const string NotificationsChanged = nameof(NotificationsChanged);
    }

    /// <summary>The SignalR group name for a given user's notifications.</summary>
    public static string GroupName(string userId) => $"notify:user:{userId}";

    private readonly ITmNotificationService? _transport;
    private readonly HubConnection? _hub;
    private readonly string _userId;
    private readonly bool _ackOnReceive;
    private bool _started;

    /// <inheritdoc />
    public event Action? OnChanged;

    /// <inheritdoc />
    public TmNotificationServiceCapabilities Capabilities
        => TmNotificationServiceCapabilities.Publish
        | TmNotificationServiceCapabilities.Read
        | TmNotificationServiceCapabilities.Query
        | TmNotificationServiceCapabilities.UnreadCount
        | TmNotificationServiceCapabilities.ReadState
        | TmNotificationServiceCapabilities.DeliveryAck
        | TmNotificationServiceCapabilities.RealtimePush;

    TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;

    /// <summary>Creates an in-process adapter around an existing service (tests / single process).</summary>
    public SignalRNotificationService(ITmNotificationService transport, string userId)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _userId = userId ?? throw new ArgumentNullException(nameof(userId));
        _transport.OnChanged += RaiseChanged;
    }

    /// <summary>Creates a SignalR service from a hub URL.</summary>
    public SignalRNotificationService(string hubUrl, string userId, bool ackOnReceive = true)
        : this(new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().Build(), userId, ackOnReceive)
    {
    }

    /// <summary>Creates a SignalR service from a prepared hub connection.</summary>
    public SignalRNotificationService(HubConnection hub, string userId, bool ackOnReceive = true)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _userId = userId ?? throw new ArgumentNullException(nameof(userId));
        _ackOnReceive = ackOnReceive;

        _hub.On<TmNotification>(HubMethods.NotificationReceived, notification =>
        {
            if (_ackOnReceive && !string.IsNullOrEmpty(notification.Id))
            {
                _ = _hub.InvokeAsync(HubMethods.MarkAsDelivered, notification.Id, _userId);
            }
            RaiseChanged();
            return Task.CompletedTask;
        });
        _hub.On(HubMethods.NotificationsChanged, RaiseChanged);

        _hub.Reconnected += async _ =>
        {
            await _hub.InvokeAsync(HubMethods.JoinUser, _userId).ConfigureAwait(false);
        };
    }

    /// <inheritdoc />
    public async Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
            return await _transport.PublishAsync(notification, cancellationToken).ConfigureAwait(false);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<TmNotification>(HubMethods.Publish, notification, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(TmNotificationQuery query, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
            return await _transport.GetNotificationsAsync(query, cancellationToken).ConfigureAwait(false);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<IReadOnlyList<TmNotification>>(HubMethods.GetNotifications, query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
            return await _transport.GetUnreadCountAsync(recipientUserId, cancellationToken).ConfigureAwait(false);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return await _hub!.InvokeAsync<int>(HubMethods.GetUnreadCount, recipientUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            await _transport.MarkAsReadAsync(notificationId, recipientUserId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub!.InvokeAsync(HubMethods.MarkAsRead, notificationId, recipientUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkAllAsReadAsync(string recipientUserId, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            await _transport.MarkAllAsReadAsync(recipientUserId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub!.InvokeAsync(HubMethods.MarkAllAsRead, recipientUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkAsDeliveredAsync(string notificationId, string recipientUserId, CancellationToken cancellationToken = default)
    {
        if (_transport is not null)
        {
            await _transport.MarkAsDeliveredAsync(notificationId, recipientUserId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub!.InvokeAsync(HubMethods.MarkAsDelivered, notificationId, recipientUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ensures the hub is connected and the user group is joined.</summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default) => EnsureConnectedAsync(cancellationToken);

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_hub is null || _started) return;

        _started = true;
        try
        {
            if (_hub.State == HubConnectionState.Disconnected)
            {
                await _hub.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            await _hub.InvokeAsync(HubMethods.JoinUser, _userId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Allow a later call to retry the connection instead of getting stuck "started".
            _started = false;
            throw;
        }
    }

    private void RaiseChanged() => OnChanged?.Invoke();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_transport is not null)
        {
            _transport.OnChanged -= RaiseChanged;
        }
        if (_hub is not null)
        {
            await _hub.DisposeAsync().ConfigureAwait(false);
        }
    }
}
