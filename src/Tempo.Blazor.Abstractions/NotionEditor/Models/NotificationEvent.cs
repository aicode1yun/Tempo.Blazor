using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

public class NotificationEvent : INotificationEvent
{
    public NotificationType Type { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public string? SenderUserId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderAvatarUrl { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? DeepLink { get; set; }
    public string? ThreadId { get; set; }
    public string? EntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
