using System.Collections.Concurrent;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionNotificationStore : ITmNotificationService
{
    private readonly ConcurrentDictionary<string, List<TmNotification>> _notifications = new(StringComparer.OrdinalIgnoreCase);

    public event Action? OnChanged;

    public TmNotificationServiceCapabilities Capabilities
        => TmNotificationServiceCapabilities.Publish
        | TmNotificationServiceCapabilities.Read
        | TmNotificationServiceCapabilities.Query
        | TmNotificationServiceCapabilities.UnreadCount
        | TmNotificationServiceCapabilities.ReadState;

    TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;

    public Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var normalized = Normalize(notification);

        var list = _notifications.GetOrAdd(normalized.EffectiveRecipientUserId, _ => []);
        lock (list)
        {
            list.Insert(0, Clone(normalized));
        }

        OnChanged?.Invoke();
        return Task.FromResult(Clone(normalized));
    }

    public Task MarkAsReadAsync(string notificationId, string userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_notifications.TryGetValue(userId, out var list))
        {
            lock (list)
            {
                var notification = list.FirstOrDefault(n => string.Equals(n.Id, notificationId, StringComparison.OrdinalIgnoreCase));
                if (notification is not null && !notification.IsRead)
                {
                    notification.ReadAt = DateTimeOffset.UtcNow;
                }
            }
        }

        OnChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (_notifications.TryGetValue(userId, out var list))
        {
            lock (list)
            {
                foreach (var notification in list.Where(n => !n.IsRead))
                {
                    notification.ReadAt = DateTimeOffset.UtcNow;
                }
            }
        }

        OnChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(TmNotificationQuery query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TmNotification>>(GetNotifications(query));
    }

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_notifications.TryGetValue(userId, out var list))
            return Task.FromResult(0);

        lock (list)
        {
            return Task.FromResult(list.Count(n => !n.IsRead));
        }
    }

    public IReadOnlyList<TmNotification> GetNotifications(TmNotificationQuery query)
    {
        if (!_notifications.TryGetValue(query.RecipientUserId, out var list))
            return [];

        lock (list)
        {
            IEnumerable<TmNotification> result = list;
            if (!query.IncludeRead)
                result = result.Where(notification => !notification.IsRead);
            if (query.EntityRef is { } entityRef)
                result = result.Where(notification => notification.EntityRef?.Equals(entityRef) == true);
            else
            {
                if (!string.IsNullOrWhiteSpace(query.EntityType))
                    result = result.Where(notification => string.Equals(notification.EntityRef?.EntityType, query.EntityType, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(query.EntityId))
                    result = result.Where(notification => string.Equals(notification.EntityRef?.EntityId, query.EntityId, StringComparison.Ordinal));
            }
            if (!string.IsNullOrWhiteSpace(query.Type))
                result = result.Where(notification => string.Equals(notification.Type, query.Type, StringComparison.OrdinalIgnoreCase));

            return result
                .Skip(Math.Max(0, query.Skip))
                .Take(Math.Clamp(query.Take, 1, 200))
                .Select(Clone)
                .ToList();
        }
    }

    public void Clear()
    {
        _notifications.Clear();
        OnChanged?.Invoke();
    }

    private static TmNotification Normalize(TmNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var recipientUserId = notification.EffectiveRecipientUserId.Trim();
        if (string.IsNullOrWhiteSpace(recipientUserId))
            throw new ArgumentException("Recipient user id is required.", nameof(notification));

        var normalized = Clone(notification);
        normalized.Id = string.IsNullOrWhiteSpace(notification.Id) ? Guid.NewGuid().ToString("N") : notification.Id.Trim();
        normalized.RecipientUserId = recipientUserId;
        normalized.CreatedAt = notification.CreatedAt == default ? DateTimeOffset.UtcNow : notification.CreatedAt.ToUniversalTime();
        normalized.ReadAt = notification.ReadAt?.ToUniversalTime();
        return normalized;
    }

    private static TmNotification Clone(TmNotification notification)
        => new()
        {
            Id = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            Recipient = notification.Recipient,
            Actor = notification.Actor,
            Type = notification.Type,
            Title = notification.Title,
            Body = notification.Body,
            Severity = notification.Severity,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            ActionUrl = notification.ActionUrl,
            EntityRef = notification.EntityRef?.Normalize(),
            CorrelationId = notification.CorrelationId,
            Metadata = notification.Metadata is null ? null : new Dictionary<string, object>(notification.Metadata, StringComparer.OrdinalIgnoreCase)
        };
}
