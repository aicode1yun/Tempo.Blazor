using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotificationService
{
    Task NotifyAsync(INotificationEvent notificationEvent, CancellationToken ct = default);
    Task MarkAsReadAsync(string notificationId, string userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<INotification>> GetNotificationsAsync(string userId, int limit = 20, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
}
