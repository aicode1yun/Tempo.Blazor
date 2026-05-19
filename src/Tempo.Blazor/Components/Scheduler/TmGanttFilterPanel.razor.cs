using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Scheduler;

public partial class TmGanttFilterPanel
{
    private sealed class EditableFilter
    {
        public string Field { get; set; } = "Title";
        public GanttFilterOperator Operator { get; set; } = GanttFilterOperator.Contains;
        public string? Value { get; set; }
    }

    private List<EditableFilter> _editFilters = [];

    [Parameter] public List<GanttFilter> Filters { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<GanttFilter>> OnFiltersChanged { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    protected override void OnParametersSet()
    {
        _editFilters = Filters
            .Select(f => new EditableFilter { Field = f.Field, Operator = f.Operator, Value = f.Value })
            .ToList();
    }

    private void AddFilter() =>
        _editFilters.Add(new EditableFilter());

    private void RemoveFilter(EditableFilter filter) =>
        _editFilters.Remove(filter);

    private void UpdateFilterField(EditableFilter filter, string field) =>
        filter.Field = field;

    private void UpdateFilterOp(EditableFilter filter, GanttFilterOperator op) =>
        filter.Operator = op;

    private void UpdateFilterValue(EditableFilter filter, string? value) =>
        filter.Value = value;

    private async Task ApplyAsync()
    {
        var filters = _editFilters
            .Select(f => new GanttFilter(f.Field, f.Operator, f.Value))
            .ToList();
        await OnFiltersChanged.InvokeAsync(filters);
    }

    private async Task ClearAll()
    {
        _editFilters.Clear();
        await OnFiltersChanged.InvokeAsync([]);
    }
}
