namespace Tempo.Blazor.NotionEditor.Models;

public sealed class IncludePageBlockContent : IIncludePageBlockContent
{
    public Guid? SourcePageId { get; set; }
}
