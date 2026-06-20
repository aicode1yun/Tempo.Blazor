namespace Tempo.Blazor.NotionEditor.Models;

public sealed class NotionWatchSubscriptionDto
{
    public string PageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IncludeChildren { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
