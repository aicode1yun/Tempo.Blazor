namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface INotionDatabaseFilter
{
    FilterLogic Logic { get; }
    IReadOnlyList<NotionFilterCondition> Conditions { get; }
    IReadOnlyList<INotionDatabaseFilter> NestedFilters { get; }
}
