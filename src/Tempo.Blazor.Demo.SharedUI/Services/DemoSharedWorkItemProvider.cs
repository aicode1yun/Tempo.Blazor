using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Single in-memory <see cref="ITmWorkItemProvider"/> shared by multiple components in the demo
/// (Gantt, Notion "My Tasks", Scheduler). Proves the unified-provider goal: an item created or
/// edited through one component is visible to the others because they all read/write this one store.
/// Registered scoped so a circuit/app shares one instance.
/// </summary>
public sealed class DemoSharedWorkItemProvider : TmWorkItemProviderBase
{
    private readonly List<TmWorkItem> _items;
    private readonly List<TmWorkItemDependency> _dependencies;

    public DemoSharedWorkItemProvider()
    {
        var today = DateTime.Today;
        TmWorkItemAssignee Ada = new() { Id = "ada", Name = "Ada Lovelace" };
        TmWorkItemAssignee Grace = new() { Id = "grace", Name = "Grace Hopper" };

        _items =
        [
            new() { Id = "p1", SourceKey = "project", Title = "Project Phoenix", Start = today.AddDays(-2), End = today.AddDays(20),
                    Status = TmWorkItemStatus.InProgress, Priority = TmWorkItemPriority.Highest, PercentComplete = 30, Color = "#6366f1" },
            new() { Id = "p2", SourceKey = "project", Title = "Design sign-off", ParentId = "p1", Start = today.AddDays(-2), End = today.AddDays(3),
                    DueDate = today.AddDays(3), Status = TmWorkItemStatus.InProgress, Priority = TmWorkItemPriority.High,
                    PercentComplete = 60, Assignees = [Ada] },
            new() { Id = "p3", SourceKey = "project", Title = "Backend API", ParentId = "p1", Start = today.AddDays(3), End = today.AddDays(12),
                    DueDate = today.AddDays(12), Status = TmWorkItemStatus.Open, Priority = TmWorkItemPriority.High, Assignees = [Grace] },
            new() { Id = "p4", SourceKey = "project", Title = "Write release notes", ParentId = "p1", Start = today.AddDays(12), End = today.AddDays(16),
                    DueDate = today.AddDays(-1), Status = TmWorkItemStatus.Open, Priority = TmWorkItemPriority.Medium, Assignees = [Ada] },
            new() { Id = "p5", SourceKey = "project", Title = "Kickoff complete", ParentId = "p1", Start = today.AddDays(-2), End = today.AddDays(-2),
                    IsMilestone = true, Status = TmWorkItemStatus.Done, IsCompleted = true, PercentComplete = 100 },
        ];

        _dependencies =
        [
            new() { Id = "d1", FromId = "p2", ToId = "p3", Type = TmWorkItemDependencyType.FinishToStart },
            new() { Id = "d2", FromId = "p3", ToId = "p4", Type = TmWorkItemDependencyType.FinishToStart },
        ];
    }

    public override string SourceKey => "project";
    public override string DisplayName => "Project tasks";
    public override TmWorkItemCapabilities Capabilities => TmWorkItemCapabilities.All;

    public override Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<TmWorkItem> q = _items;

        if (query.Ids.Count > 0)
            q = q.Where(i => query.Ids.Contains(i.Id));
        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
            q = q.Where(i => i.Assignees.Any(a => string.Equals(a.Id, query.AssigneeId, StringComparison.OrdinalIgnoreCase)));
        if (!query.IncludeCompleted)
            q = q.Where(i => !i.IsCompleted);
        if (query.DueBefore is { } before)
            q = q.Where(i => i.DueDate is not null && i.DueDate.Value.Date <= before.Date);
        if (query.DueAfter is { } after)
            q = q.Where(i => i.DueDate is not null && i.DueDate.Value.Date >= after.Date);
        if (query.RangeStart is { } rs)
            q = q.Where(i => i.End > rs);
        if (query.RangeEnd is { } re)
            q = q.Where(i => i.Start < re);
        if (!string.IsNullOrWhiteSpace(query.FreeText))
            q = q.Where(i => i.Title.Contains(query.FreeText, StringComparison.OrdinalIgnoreCase));

        var matches = q.ToArray();
        return Task.FromResult(new PagedResult<TmWorkItem>
        {
            Items = matches,
            TotalCount = matches.Length,
            Page = 1,
            PageSize = Math.Max(1, matches.Length)
        });
    }

    public override Task<IReadOnlyList<TmWorkItemDependency>> GetDependenciesAsync(
        IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TmWorkItemDependency>>(_dependencies.ToList());

    public override Task<TmWorkItem> CreateAsync(TmWorkItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString();
        item.SourceKey = SourceKey;
        _items.Add(item);
        return Task.FromResult(item);
    }

    public override Task<TmWorkItem> UpdateAsync(TmWorkItem item, CancellationToken cancellationToken = default)
    {
        var idx = _items.FindIndex(i => i.Id == item.Id);
        if (idx >= 0) _items[idx] = item;
        else _items.Add(item);
        return Task.FromResult(item);
    }

    public override Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(i => i.Id == id);
        _dependencies.RemoveAll(d => d.FromId == id || d.ToId == id);
        return Task.CompletedTask;
    }

    public override Task SetCompletedAsync(string id, bool completed, CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is not null)
        {
            item.IsCompleted = completed;
            item.Status = completed ? TmWorkItemStatus.Done : TmWorkItemStatus.Open;
            item.PercentComplete = completed ? 100 : item.PercentComplete;
        }
        return Task.CompletedTask;
    }

    public override Task<TmWorkItemDependency> AddDependencyAsync(TmWorkItemDependency dependency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dependency.Id)) dependency.Id = Guid.NewGuid().ToString();
        _dependencies.Add(dependency);
        return Task.FromResult(dependency);
    }

    public override Task RemoveDependencyAsync(string dependencyId, CancellationToken cancellationToken = default)
    {
        _dependencies.RemoveAll(d => d.Id == dependencyId);
        return Task.CompletedTask;
    }
}
