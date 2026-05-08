using System.Collections.Concurrent;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Services;

/// <summary>
/// In-memory implementation of <see cref="INotificationService"/> and
/// <see cref="INotificationBadgeState"/> for demos and tests.
/// </summary>
public class InMemoryNotificationStore : INotificationService, INotificationBadgeState
{
    private readonly ConcurrentDictionary<string, List<Notification>> _store = new();
    private int _unreadCount;

    public int UnreadCount => _unreadCount;

    public event Action? OnChanged;

    public Task NotifyAsync(INotificationEvent notificationEvent, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid().ToString(),
            Event = notificationEvent,
            IsRead = false
        };

        var list = _store.GetOrAdd(notificationEvent.RecipientUserId, _ => new List<Notification>());
        lock (list)
        {
            list.Insert(0, notification);
        }

        Interlocked.Increment(ref _unreadCount);
        OnChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task MarkAsReadAsync(string notificationId, string userId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(userId, out var list))
        {
            lock (list)
            {
                var n = list.FirstOrDefault(x => x.Id == notificationId);
                if (n is not null && !n.IsRead)
                {
                    n.IsRead = true;
                    n.ReadAt = DateTime.UtcNow;
                    Interlocked.Decrement(ref _unreadCount);
                    OnChanged?.Invoke();
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(userId, out var list))
        {
            int previouslyUnread;
            lock (list)
            {
                previouslyUnread = list.Count(x => !x.IsRead);
                foreach (var n in list.Where(x => !x.IsRead))
                {
                    n.IsRead = true;
                    n.ReadAt = DateTime.UtcNow;
                }
            }
            if (previouslyUnread > 0)
            {
                Interlocked.Add(ref _unreadCount, -previouslyUnread);
                OnChanged?.Invoke();
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<INotification>> GetNotificationsAsync(string userId, int limit = 20, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(userId, out var list))
            return Task.FromResult<IReadOnlyList<INotification>>(Array.Empty<INotification>());

        lock (list)
        {
            var result = list.Take(limit).Cast<INotification>().ToList();
            return Task.FromResult<IReadOnlyList<INotification>>(result);
        }
    }

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(userId, out var list))
            return Task.FromResult(0);

        lock (list)
        {
            return Task.FromResult(list.Count(x => !x.IsRead));
        }
    }

    public void Increment()
    {
        Interlocked.Increment(ref _unreadCount);
        OnChanged?.Invoke();
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _unreadCount, 0);
        OnChanged?.Invoke();
    }

    public void ClearAll()
    {
        _store.Clear();
        Interlocked.Exchange(ref _unreadCount, 0);
        OnChanged?.Invoke();
    }
}
