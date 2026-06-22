using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Single, unified contract for supplying and mutating <see cref="TmWorkItem"/>s.
/// One registration of this provider can feed every task-bearing component
/// (Gantt, Notion tasks, scheduler, work-item blocks) within an application,
/// so the same item stays consistent across them.
/// </summary>
/// <remarks>
/// Write operations are optional and gated by capability flags. A read-only
/// provider should declare only <see cref="TmWorkItemCapabilities.Read"/> and may throw
/// <see cref="NotSupportedException"/> from mutating methods.
/// </remarks>
public interface ITmWorkItemProvider : ITmCapabilityProvider<TmWorkItemCapabilities>
{
    /// <summary>Stable source key used by the registry and by queries.</summary>
    string SourceKey { get; }

    /// <summary>User-visible source name.</summary>
    string DisplayName { get; }

    /// <summary>Returns a paged set of work items matching the query.</summary>
    Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns a single work item by id, or null when not found.</summary>
    Task<TmWorkItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the dependencies relevant to the given items (or all when ids is empty).</summary>
    Task<IReadOnlyList<TmWorkItemDependency>> GetDependenciesAsync(
        IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default);

    /// <summary>Creates a new work item and returns the stored copy. Requires <see cref="TmWorkItemCapabilities.Create"/>.</summary>
    Task<TmWorkItem> CreateAsync(TmWorkItem item, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing work item and returns the stored copy. Requires <see cref="TmWorkItemCapabilities.Update"/>.</summary>
    Task<TmWorkItem> UpdateAsync(TmWorkItem item, CancellationToken cancellationToken = default);

    /// <summary>Deletes a work item by id. Requires <see cref="TmWorkItemCapabilities.Delete"/>.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Sets the completed state of a work item. Requires <see cref="TmWorkItemCapabilities.Update"/>.</summary>
    Task SetCompletedAsync(string id, bool completed, CancellationToken cancellationToken = default);

    /// <summary>Adds a dependency between two items. Requires <see cref="TmWorkItemCapabilities.Dependencies"/>.</summary>
    Task<TmWorkItemDependency> AddDependencyAsync(TmWorkItemDependency dependency, CancellationToken cancellationToken = default);

    /// <summary>Removes a dependency by id. Requires <see cref="TmWorkItemCapabilities.Dependencies"/>.</summary>
    Task RemoveDependencyAsync(string dependencyId, CancellationToken cancellationToken = default);
}
