using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Services;

/// <summary>Default no-op implementation of <see cref="INotificationService"/>. Does nothing.</summary>
public class NoOpNotificationService : INotificationService
{
    public Task NotifyAsync(INotificationEvent notificationEvent, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task MarkAsReadAsync(string notificationId, string userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<INotification>> GetNotificationsAsync(string userId, int limit = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<INotification>>(Array.Empty<INotification>());

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(0);
}
