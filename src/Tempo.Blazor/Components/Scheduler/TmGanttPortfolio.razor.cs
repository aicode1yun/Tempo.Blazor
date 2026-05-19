using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Scheduler;

public partial class TmGanttPortfolio
{
    [Parameter] public IReadOnlyList<GanttPortfolioProject> Projects { get; set; } = [];
}
