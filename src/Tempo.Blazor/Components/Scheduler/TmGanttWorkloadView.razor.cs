using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Scheduler;

public partial class TmGanttWorkloadView
{
    [Parameter] public IReadOnlyList<GanttTask> Tasks { get; set; } = [];
    [Parameter] public WorkingSchedule WorkingSchedule { get; set; } = new();
    [Parameter] public WorkloadDisplayMode DisplayMode { get; set; } = WorkloadDisplayMode.Hours;
}
