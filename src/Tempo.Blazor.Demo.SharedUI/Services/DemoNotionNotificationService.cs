using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionNotificationService : INotificationService, INotificationBadgeState
{
    private readonly HttpClient _http;
    private int _unreadCount;

    public DemoNotionNotificationService(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public int UnreadCount => _unreadCount;

    public event Action? OnChanged;

    public void Increment()
    {
        Interlocked.Increment(ref _unreadCount);
        OnChanged?.Invoke();
    }

    public void Reset()
    {
        UpdateUnreadCount(0);
    }

    public async Task NotifyAsync(INotificationEvent notificationEvent, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("/api/notion/notifications", ToConcreteEvent(notificationEvent), ct);
        response.EnsureSuccessStatusCode();
        Increment();
    }

    public async Task MarkAsReadAsync(string notificationId, string userId, CancellationToken ct = default)
    {
        var before = await GetUnreadCountAsync(userId, ct);
        using var response = await _http.PostAsync(
            $"/api/notion/notifications/users/{Uri.EscapeDataString(userId)}/{Uri.EscapeDataString(notificationId)}/read",
            null,
            ct);
        response.EnsureSuccessStatusCode();
        var after = await GetUnreadCountAsync(userId, ct);
        UpdateUnreadCount(_unreadCount + after - before);
    }

    public async Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        var before = await GetUnreadCountAsync(userId, ct);
        using var response = await _http.PostAsync(
            $"/api/notion/notifications/users/{Uri.EscapeDataString(userId)}/read-all",
            null,
            ct);
        response.EnsureSuccessStatusCode();
        var after = await GetUnreadCountAsync(userId, ct);
        UpdateUnreadCount(_unreadCount + after - before);
    }

    public async Task<IReadOnlyList<INotification>> GetNotificationsAsync(string userId, int limit = 20, CancellationToken ct = default)
    {
        var notifications = await _http.GetFromJsonAsync<List<NotificationDto>>(
            $"/api/notion/notifications/users/{Uri.EscapeDataString(userId)}?limit={limit}",
            ct);
        return notifications?.Cast<INotification>().ToList() ?? [];
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        var count = await _http.GetFromJsonAsync<int>(
            $"/api/notion/notifications/users/{Uri.EscapeDataString(userId)}/unread-count",
            ct);
        return count;
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync("/api/notion/notifications", ct);
        response.EnsureSuccessStatusCode();
        UpdateUnreadCount(0);
    }

    private void UpdateUnreadCount(int count)
    {
        count = Math.Max(0, count);
        if (Interlocked.Exchange(ref _unreadCount, count) != count)
            OnChanged?.Invoke();
    }

    private static NotificationEvent ToConcreteEvent(INotificationEvent notificationEvent)
        => notificationEvent is NotificationEvent concrete
            ? concrete
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
}
