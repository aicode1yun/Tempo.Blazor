using System.Net.Http.Json;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionNotificationService : ITmNotificationService
{
    private readonly HttpClient _http;
    private int _unreadCount;

    public DemoNotionNotificationService(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public int UnreadCount => _unreadCount;

    public event Action? OnChanged;

    public TmNotificationServiceCapabilities Capabilities
        => TmNotificationServiceCapabilities.Publish
        | TmNotificationServiceCapabilities.Read
        | TmNotificationServiceCapabilities.Query
        | TmNotificationServiceCapabilities.UnreadCount
        | TmNotificationServiceCapabilities.ReadState;

    TmNotificationServiceCapabilities ITmCapabilityProvider<TmNotificationServiceCapabilities>.Capabilities => Capabilities;

    public async Task<TmNotification> PublishAsync(TmNotification notification, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("/api/notion/notifications", notification, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TmNotification>(cancellationToken: ct) ?? notification;
        var recipientUserId = created.EffectiveRecipientUserId;
        if (!string.IsNullOrWhiteSpace(recipientUserId))
            UpdateUnreadCount(await GetUnreadCountAsync(recipientUserId, ct));
        else
            OnChanged?.Invoke();
        return created;
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

    public async Task<IReadOnlyList<TmNotification>> GetNotificationsAsync(TmNotificationQuery query, CancellationToken ct = default)
    {
        var notifications = await _http.GetFromJsonAsync<List<TmNotification>>(
            $"/api/notion/notifications/users/{Uri.EscapeDataString(query.RecipientUserId)}?skip={query.Skip}&take={query.Take}&includeRead={query.IncludeRead.ToString().ToLowerInvariant()}",
            ct);
        return notifications ?? [];
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

    public void Reset()
    {
        UpdateUnreadCount(0);
    }
}
