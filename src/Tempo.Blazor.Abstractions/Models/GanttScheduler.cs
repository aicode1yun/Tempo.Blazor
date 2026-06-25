using Tempo.Blazor.Abstractions.WorkItems;
namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Schedules Gantt tasks respecting all 4 dependency types and lag/lead.
/// Uses Kahn's topological sort to detect cycles and process in dependency order.
/// </summary>
public static class GanttScheduler
{
    /// <summary>
    /// Adjusts task start/end dates so all dependency constraints are satisfied.
    /// Modifies <paramref name="tasks"/> in place.
    /// </summary>
    /// <exception cref="GanttCircularDependencyException">Thrown when a cycle is detected.</exception>
    public static void Schedule(IList<TmWorkItem> tasks, IEnumerable<GanttDependency> dependencies)
    {
        var taskMap = tasks.ToDictionary(t => t.Id);
        var deps = dependencies.ToList();

        // Build adjacency for topological sort: fromId → list of toIds
        var adjacency = tasks.ToDictionary(t => t.Id, _ => new List<string>());
        var inDegree  = tasks.ToDictionary(t => t.Id, _ => 0);

        foreach (var dep in deps)
        {
            if (!adjacency.ContainsKey(dep.FromId) || !adjacency.ContainsKey(dep.ToId))
                continue;
            adjacency[dep.FromId].Add(dep.ToId);
            inDegree[dep.ToId]++;
        }

        // Kahn's algorithm
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);
            foreach (var next in adjacency[current])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        if (order.Count != tasks.Count)
            throw new GanttCircularDependencyException();

        // Build a lookup: toId → list of deps that constrain it
        var constraints = tasks.ToDictionary(t => t.Id, _ => new List<GanttDependency>());
        foreach (var dep in deps)
        {
            if (constraints.ContainsKey(dep.ToId))
                constraints[dep.ToId].Add(dep);
        }

        // Forward pass in topological order
        foreach (var id in order)
        {
            if (!taskMap.TryGetValue(id, out var task)) continue;

            foreach (var dep in constraints[id])
            {
                if (!taskMap.TryGetValue(dep.FromId, out var pred)) continue;

                var duration = task.ScheduledDuration();

                switch (dep.DepType)
                {
                    case GanttDependencyType.FinishToStart:
                    {
                        var minStart = pred.ScheduledEnd().AddDays(dep.LagDays);
                        if (task.ScheduledStart() < minStart)
                        {
                            task.Start = minStart;
                            task.End   = minStart + duration;
                        }
                        break;
                    }
                    case GanttDependencyType.StartToStart:
                    {
                        var minStart = pred.ScheduledStart().AddDays(dep.LagDays);
                        if (task.ScheduledStart() < minStart)
                        {
                            task.Start = minStart;
                            task.End   = minStart + duration;
                        }
                        break;
                    }
                    case GanttDependencyType.FinishToFinish:
                    {
                        var minEnd = pred.ScheduledEnd().AddDays(dep.LagDays);
                        if (task.ScheduledEnd() < minEnd)
                        {
                            task.End   = minEnd;
                            task.Start = minEnd - duration;
                        }
                        break;
                    }
                    case GanttDependencyType.StartToFinish:
                    {
                        var minEnd = pred.ScheduledStart().AddDays(dep.LagDays);
                        if (task.ScheduledEnd() < minEnd)
                        {
                            task.End   = minEnd;
                            task.Start = minEnd - duration;
                        }
                        break;
                    }
                }
            }
        }
    }
}
