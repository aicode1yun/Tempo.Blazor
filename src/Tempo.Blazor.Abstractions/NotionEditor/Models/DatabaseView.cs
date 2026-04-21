namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class DatabaseView : IDatabaseView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseViewType Type { get; set; }
    public INotionDatabaseFilter? Filter { get; set; }
    public IReadOnlyList<NotionDatabaseSort> Sorts { get; set; } = new List<NotionDatabaseSort>();
    public NotionDatabaseGrouping? Grouping { get; set; }
    public IReadOnlyList<Guid> VisibleFieldIds { get; set; } = new List<Guid>();
    public IDatabaseViewConfig? Config { get; set; }
}
