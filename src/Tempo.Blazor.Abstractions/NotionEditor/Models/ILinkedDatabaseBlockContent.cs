namespace Tempo.Blazor.NotionEditor.Models;

public interface ILinkedDatabaseBlockContent : IBlockContent
{
    Guid SourceDatabaseId { get; }
    Guid SourcePageId { get; }
    Guid ActiveViewId { get; }
    INotionDatabaseFilter? OverrideFilter { get; }
    IReadOnlyList<NotionDatabaseSort>? OverrideSorts { get; }
}
