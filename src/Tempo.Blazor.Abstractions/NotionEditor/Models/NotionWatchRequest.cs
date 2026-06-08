namespace Tempo.Blazor.NotionEditor.Models;

public sealed class NotionWatchRequest
{
    public string UserId { get; set; } = string.Empty;
    public bool IncludeChildren { get; set; }
}
