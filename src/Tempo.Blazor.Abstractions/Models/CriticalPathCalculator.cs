using Tempo.Blazor.Abstractions.WorkItems;
namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Calculates the critical path of a Gantt task network using ES/EF/LS/LF algorithm.
/// Returns IDs of tasks with zero total float (slack).
/// </summary>
public static class CriticalPathCalculator
{
    /// <summary>
    /// Calculates which tasks lie on the critical path.
    /// </summary>
    public static IReadOnlySet<string> Calculate(
        IEnumerable<TmWorkItem> tasks,
        IEnumerable<GanttDependency> dependencies)
    {
        var taskList = tasks.ToList();
        var depList  = dependencies.ToList();

        if (taskList.Count == 0)
            return new HashSet<string>();

        var taskMap = taskList.ToDictionary(t => t.Id);

        // Build adjacency: fromId → toIds (successors)
        var successors   = taskList.ToDictionary(t => t.Id, _ => new List<string>());
        var predecessors = taskList.ToDictionary(t => t.Id, _ => new List<string>());
        var inDegree     = taskList.ToDictionary(t => t.Id, _ => 0);

        foreach (var dep in depList)
        {
            if (!successors.ContainsKey(dep.FromId) || !predecessors.ContainsKey(dep.ToId))
                continue;
            successors[dep.FromId].Add(dep.ToId);
            predecessors[dep.ToId].Add(dep.FromId);
            inDegree[dep.ToId]++;
        }

        // Topological order (Kahn's)
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();
        var tempDegree = new Dictionary<string, int>(inDegree);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var next in successors[id])
            {
                tempDegree[next]--;
                if (tempDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        // If we couldn't order all tasks (cycle), return empty
        if (order.Count != taskList.Count)
            return new HashSet<string>();

        // Duration in days for each task
        double Duration(TmWorkItem t) => Math.Max(1, t.ScheduledDuration().TotalDays);

        // Forward pass: ES (earliest start), EF (earliest finish)
        var es = new Dictionary<string, double>();
        var ef = new Dictionary<string, double>();

        foreach (var id in order)
        {
            var task = taskMap[id];
            var dur  = Duration(task);

            if (predecessors[id].Count == 0)
            {
                es[id] = 0;
            }
            else
            {
                var maxEf = predecessors[id]
                    .Where(p => ef.ContainsKey(p))
                    .Select(p => ef[p])
                    .DefaultIfEmpty(0)
                    .Max();
                es[id] = maxEf;
            }
            ef[id] = es[id] + dur;
        }

        double projectEnd = ef.Values.Max();

        // Backward pass: LF (latest finish), LS (latest start)
        var lf = new Dictionary<string, double>();
        var ls = new Dictionary<string, double>();

        foreach (var id in Enumerable.Reverse(order))
        {
            var task = taskMap[id];
            var dur  = Duration(task);

            if (successors[id].Count == 0)
            {
                lf[id] = projectEnd;
            }
            else
            {
                var minLs = successors[id]
                    .Where(s => ls.ContainsKey(s))
                    .Select(s => ls[s])
                    .DefaultIfEmpty(projectEnd)
                    .Min();
                lf[id] = minLs;
            }
            ls[id] = lf[id] - dur;
        }

        // Float = LS - ES; zero float = critical
        const double epsilon = 1e-9;
        return new HashSet<string>(
            order.Where(id => Math.Abs(ls[id] - es[id]) < epsilon));
    }
}
