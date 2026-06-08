using System.Collections.Concurrent;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public sealed class DemoNotionNotificationStore : INotificationService
{
    private readonly ConcurrentDictionary<string, List<NotificationDto>> _notifications = new(StringComparer.OrdinalIgnoreCase);

    public Task NotifyAsync(INotificationEvent notificationEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var dto = new NotificationDto
        {
            Id = Guid.NewGuid().ToString("D"),
            Event = ToConcreteEvent(notificationEvent),
            IsRead = false
        };

        var list = _notifications.GetOrAdd(dto.Event.RecipientUserId, _ => []);
        lock (list)
        {
            list.Insert(0, dto);
        }

        return Task.CompletedTask;
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
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }
            }
        }

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
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<INotification>> GetNotificationsAsync(string userId, int limit = 20, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<INotification>>(GetNotificationDtos(userId, limit));
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

    public IReadOnlyList<NotificationDto> GetNotificationDtos(string userId, int limit)
    {
        if (!_notifications.TryGetValue(userId, out var list))
            return [];

        lock (list)
        {
            return list.Take(Math.Max(1, limit)).Select(Clone).ToList();
        }
    }

    public void Clear()
    {
        _notifications.Clear();
    }

    private static NotificationEvent ToConcreteEvent(INotificationEvent notificationEvent)
        => notificationEvent is NotificationEvent concrete
            ? Clone(concrete)
            : new NotificationEvent
            {
                Type = notificationEvent.Type,
                RecipientUserId = notificationEvent.RecipientUserId,
                SenderUserId = notificationEvent.SenderUserId,
                SenderName = notificationEvent.SenderName,
                SenderAvatarUrl = notificationEvent.SenderAvatarUrl,
                Message = notificationEvent.Message,
                DeepLink = notificationEvent.DeepLink,
                ThreadId = notificationEvent.ThreadId,
                EntryId = notificationEvent.EntryId,
                CreatedAt = notificationEvent.CreatedAt
            };

    private static NotificationDto Clone(NotificationDto notification)
        => new()
        {
            Id = notification.Id,
            Event = Clone(notification.Event),
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt
        };

    private static NotificationEvent Clone(NotificationEvent notificationEvent)
        => new()
        {
            Type = notificationEvent.Type,
            RecipientUserId = notificationEvent.RecipientUserId,
            SenderUserId = notificationEvent.SenderUserId,
            SenderName = notificationEvent.SenderName,
            SenderAvatarUrl = notificationEvent.SenderAvatarUrl,
            Message = notificationEvent.Message,
            DeepLink = notificationEvent.DeepLink,
            ThreadId = notificationEvent.ThreadId,
            EntryId = notificationEvent.EntryId,
            CreatedAt = notificationEvent.CreatedAt
        };
}
