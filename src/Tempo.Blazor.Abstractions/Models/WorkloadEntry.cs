namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Represents the workload allocation for one assignee on one day.</summary>
public record WorkloadEntry(
    string AssigneeId,
    DateTime Date,
    double AllocatedHours,
    double CapacityHours);
