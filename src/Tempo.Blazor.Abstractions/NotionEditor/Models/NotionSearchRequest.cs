namespace Tempo.Blazor.NotionEditor.Models;

public sealed class NotionSearchRequest
{
    public string? Query { get; set; }
    public NotionSearchFilter? Filter { get; set; }
    public int MaxResults { get; set; } = 20;
}
