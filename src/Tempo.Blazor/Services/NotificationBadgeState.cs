using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Services;

/// <summary>In-memory badge state that tracks unread notification count.</summary>
public class NotificationBadgeState : INotificationBadgeState
{
    private int _unreadCount;

    public int UnreadCount => _unreadCount;

    public event Action? OnChanged;

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
}
