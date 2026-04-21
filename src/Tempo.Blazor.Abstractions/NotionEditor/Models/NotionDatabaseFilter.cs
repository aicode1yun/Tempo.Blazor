namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public class NotionDatabaseFilter : INotionDatabaseFilter
{
    public FilterLogic Logic { get; set; }
    public IReadOnlyList<NotionFilterCondition> Conditions { get; set; } = new List<NotionFilterCondition>();
    public IReadOnlyList<INotionDatabaseFilter> NestedFilters { get; set; } = new List<INotionDatabaseFilter>();
}
