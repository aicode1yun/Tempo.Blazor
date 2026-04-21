namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IDatabaseView
{
    Guid Id { get; }
    string Name { get; }
    DatabaseViewType Type { get; }
    INotionDatabaseFilter? Filter { get; }
    IReadOnlyList<NotionDatabaseSort> Sorts { get; }
    NotionDatabaseGrouping? Grouping { get; }
    IReadOnlyList<Guid> VisibleFieldIds { get; }
    IDatabaseViewConfig? Config { get; }
}
