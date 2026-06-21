namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>Describes a dependency relationship between two <see cref="TmWorkItem"/>s.</summary>
public sealed class TmWorkItemDependency
{
    /// <summary>Unique identifier of the dependency.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Source (predecessor) work item identifier.</summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>Target (successor) work item identifier.</summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>Type of dependency.</summary>
    public TmWorkItemDependencyType Type { get; set; } = TmWorkItemDependencyType.FinishToStart;

    /// <summary>Lag (positive) or lead (negative) in calendar days.</summary>
    public int LagDays { get; set; }
}
