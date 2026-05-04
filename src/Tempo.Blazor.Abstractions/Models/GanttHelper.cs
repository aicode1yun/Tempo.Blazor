namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Helper utilities for working with Gantt task hierarchies and timelines.
/// </summary>
public static class GanttHelper
{
    /// <summary>
    /// Builds a tree structure from flat tasks using <see cref="GanttTask.ParentId"/>.
    /// </summary>
    public static IReadOnlyList<GanttTaskNode> BuildTree(IReadOnlyList<GanttTask> tasks)
    {
        var lookup = tasks.ToDictionary(t => t.Id);
        var nodes = tasks.Select(t => new GanttTaskNode(t)).ToDictionary(n => n.Task.Id);

        var roots = new List<GanttTaskNode>();
        foreach (var node in nodes.Values)
        {
            if (string.IsNullOrEmpty(node.Task.ParentId) || !nodes.TryGetValue(node.Task.ParentId, out var parent))
            {
                roots.Add(node);
            }
            else
            {
                parent.Children.Add(node);
                node.Depth = parent.Depth + 1;
            }
        }

        return roots;
    }

    /// <summary>
    /// Flattens a tree back to a list respecting expand/collapse state.
    /// </summary>
    public static IReadOnlyList<GanttTaskNode> FlattenVisible(IReadOnlyList<GanttTaskNode> roots)
    {
        var result = new List<GanttTaskNode>();
        foreach (var root in roots)
            FlattenRecursive(root, result);
        return result;
    }

    private static void FlattenRecursive(GanttTaskNode node, List<GanttTaskNode> result)
    {
        result.Add(node);
        if (node.Task.IsExpanded)
        {
            foreach (var child in node.Children)
                FlattenRecursive(child, result);
        }
    }

    /// <summary>
    /// Computes the overall time range for a set of tasks.
    /// </summary>
    public static (DateTime Start, DateTime End) GetTimeRange(IReadOnlyList<GanttTask> tasks)
    {
        if (tasks.Count == 0) return (DateTime.Today, DateTime.Today.AddDays(7));
        var start = tasks.Min(t => t.Start);
        var end = tasks.Max(t => t.End);
        if (start == end) end = end.AddDays(1);
        return (start, end);
    }

    /// <summary>
    /// Computes the X offset and width for a task bar within a timeline.
    /// </summary>
    public static (double Left, double Width) CalculateBarPosition(
        DateTime taskStart, DateTime taskEnd, DateTime timelineStart, DateTime timelineEnd, double totalWidth)
    {
        var totalDuration = timelineEnd - timelineStart;
        if (totalDuration.TotalSeconds <= 0) return (0, 0);

        var left = (taskStart - timelineStart).TotalSeconds / totalDuration.TotalSeconds * totalWidth;
        var width = (taskEnd - taskStart).TotalSeconds / totalDuration.TotalSeconds * totalWidth;
        return (Math.Max(0, left), Math.Max(1, width));
    }
}

/// <summary>
/// A node in the Gantt task tree.
/// </summary>
public class GanttTaskNode
{
    /// <summary>The task data.</summary>
    public GanttTask Task { get; }

    /// <summary>Child nodes.</summary>
    public List<GanttTaskNode> Children { get; } = [];

    /// <summary>Depth in the tree (0 for roots).</summary>
    public int Depth { get; set; }

    public GanttTaskNode(GanttTask task) => Task = task;
}
