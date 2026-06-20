using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Scheduler;

public partial class TmGanttReportsPanel
{
    private bool _showNewForm;

    [Parameter] public IReadOnlyList<GanttReport> Reports { get; set; } = [];

    [Parameter] public EventCallback<GanttReport> OnReportRun { get; set; }

    private async Task RunReportAsync(GanttReport report)
        => await OnReportRun.InvokeAsync(report);
}
