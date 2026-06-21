using Tempo.Blazor.Models;

namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Convenience base class for <see cref="ITmWorkItemProvider"/> implementations.
/// Read-only providers only need to override <see cref="SearchAsync"/> (and optionally
/// <see cref="GetByIdAsync"/>); all mutating members throw <see cref="NotSupportedException"/>
/// by default and should be overridden when the matching capability is declared.
/// </summary>
public abstract class TmWorkItemProviderBase : ITmWorkItemProvider
{
    /// <inheritdoc />
    public abstract string SourceKey { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public virtual TmWorkItemCapabilities Capabilities => TmWorkItemCapabilities.Read;

    /// <inheritdoc />
    public abstract Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task<TmWorkItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync(new TmWorkItemQuery { Ids = [id], Take = 1, IncludeCompleted = true }, cancellationToken)
            .ConfigureAwait(false);
        return result.Items.FirstOrDefault(i => i.Id == id);
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<TmWorkItemDependency>> GetDependenciesAsync(
        IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TmWorkItemDependency>>([]);

    /// <inheritdoc />
    public virtual Task<TmWorkItem> CreateAsync(TmWorkItem item, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Provider '{SourceKey}' does not support creating work items.");

    /// <inheritdoc />
    public virtual Task<TmWorkItem> UpdateAsync(TmWorkItem item, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Provider '{SourceKey}' does not support updating work items.");

    /// <inheritdoc />
    public virtual Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Provider '{SourceKey}' does not support deleting work items.");

    /// <inheritdoc />
    public virtual async Task SetCompletedAsync(string id, bool completed, CancellationToken cancellationToken = default)
    {
        var item = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Work item '{id}' was not found.");
        item.IsCompleted = completed;
        item.Status = completed ? TmWorkItemStatus.Done : TmWorkItemStatus.Open;
        if (completed && item.PercentComplete < 100) item.PercentComplete = 100;
        await UpdateAsync(item, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual Task<TmWorkItemDependency> AddDependencyAsync(TmWorkItemDependency dependency, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Provider '{SourceKey}' does not support dependencies.");

    /// <inheritdoc />
    public virtual Task RemoveDependencyAsync(string dependencyId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Provider '{SourceKey}' does not support dependencies.");
}
