namespace Tempo.Blazor.NotionEditor.Models;

public sealed class NotificationDto : INotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public NotificationEvent Event { get; set; } = new();
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    INotificationEvent INotification.Event => Event;
}
