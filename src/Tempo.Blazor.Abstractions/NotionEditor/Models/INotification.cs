namespace Tempo.Blazor.NotionEditor.Models;

public interface INotification
{
    string Id { get; }
    INotificationEvent Event { get; }
    bool IsRead { get; }
    DateTime? ReadAt { get; }
}
