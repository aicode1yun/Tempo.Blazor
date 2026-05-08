using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.NotionEditor.Models;

public interface INotificationEvent
{
    NotificationType Type { get; }
    string RecipientUserId { get; }
    string? SenderUserId { get; }
    string? SenderName { get; }
    string? SenderAvatarUrl { get; }
    string Message { get; }
    string? DeepLink { get; }
    string? ThreadId { get; }
    string? EntryId { get; }
    DateTime CreatedAt { get; }
}
