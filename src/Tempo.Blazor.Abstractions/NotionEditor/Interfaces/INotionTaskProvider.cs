namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

/// <summary>Provides aggregated task items for the Notion editor.</summary>
public interface INotionTaskProvider
{
    /// <summary>Gets a paged set of tasks matching the query.</summary>
    Task<PagedResult<NotionTaskDto>> GetTasksAsync(NotionTaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>Sets the completed state for a task.</summary>
    Task SetCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default);
}
