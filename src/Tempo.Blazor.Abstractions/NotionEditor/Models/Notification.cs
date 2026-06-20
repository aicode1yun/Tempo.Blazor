namespace Tempo.Blazor.NotionEditor.Models;

public class Notification : INotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public INotificationEvent Event { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
