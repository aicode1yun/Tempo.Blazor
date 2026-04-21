namespace Tempo.Blazor.NotionEditor.Models;

public class LinkedDatabaseBlockContent : ILinkedDatabaseBlockContent
{
    public Guid SourceDatabaseId { get; set; }
    public Guid SourcePageId { get; set; }
    public Guid ActiveViewId { get; set; }
    public INotionDatabaseFilter? OverrideFilter { get; set; }
    public IReadOnlyList<NotionDatabaseSort>? OverrideSorts { get; set; }
}
