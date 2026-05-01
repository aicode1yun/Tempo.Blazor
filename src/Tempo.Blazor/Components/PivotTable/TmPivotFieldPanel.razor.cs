using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.PivotTable;

namespace Tempo.Blazor.Components.PivotTable;

/// <summary>
/// Configuration panel for TmPivotTable that allows drag-and-drop field arrangement
/// into Row, Column, Value, and Filter areas.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public partial class TmPivotFieldPanel<TItem>
{
    // ── Draft state (working copy) ───────────────────────────────
    private readonly List<string> _draftRowFields = [];
    private readonly List<string> _draftColumnFields = [];
    private readonly List<PivotValueFieldConfiguration> _draftValueFields = [];
    private readonly Dictionary<string, List<object?>> _draftFilterFields = [];

    // ── Drag state ───────────────────────────────────────────────
    private string? _draggedFieldKey;
    private PivotArea? _draggedFromArea;

    // ── Editing state ────────────────────────────────────────────
    private int? _editingValueFieldIndex;
    private string? _expandedFilterFieldKey;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>All available field definitions.</summary>
    [Parameter] public List<PivotField<TItem>> Fields { get; set; } = [];

    /// <summary>Source data items used to compute distinct filter values.</summary>
    [Parameter] public IEnumerable<TItem>? Items { get; set; }

    /// <summary>Keys of fields currently in the row area.</summary>
    [Parameter] public List<string> RowFieldKeys { get; set; } = [];

    /// <summary>Keys of fields currently in the column area.</summary>
    [Parameter] public List<string> ColumnFieldKeys { get; set; } = [];

    /// <summary>Value field configurations in the data area.</summary>
    [Parameter] public List<PivotValueFieldConfiguration> ValueFields { get; set; } = [];

    /// <summary>Filter field configurations.</summary>
    [Parameter] public Dictionary<string, List<object?>> FilterFields { get; set; } = [];

    /// <summary>When true, enables drag-and-drop. Default: true.</summary>
    [Parameter] public bool AllowDragDrop { get; set; } = true;

    /// <summary>Fires when the user applies a new configuration.</summary>
    [Parameter] public EventCallback<PivotTableConfiguration> OnConfigurationChanged { get; set; }

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>Syncs draft state from parameters when they change.</summary>
    protected override void OnParametersSet()
    {
        SyncDraftState();
    }

    private void SyncDraftState()
    {
        _draftRowFields.Clear();
        _draftRowFields.AddRange(RowFieldKeys);

        _draftColumnFields.Clear();
        _draftColumnFields.AddRange(ColumnFieldKeys);

        _draftValueFields.Clear();
        _draftValueFields.AddRange(ValueFields.Select(v => new PivotValueFieldConfiguration
        {
            FieldKey = v.FieldKey,
            Aggregation = v.Aggregation,
            DisplayName = v.DisplayName,
            Format = v.Format
        }));

        _draftFilterFields.Clear();
        foreach (var kv in FilterFields)
            _draftFilterFields[kv.Key] = [.. kv.Value];
    }

    // ── Drag & Drop ──────────────────────────────────────────────

    private void OnChipDragStart(string fieldKey, PivotArea fromArea)
    {
        _draggedFieldKey = fieldKey;
        _draggedFromArea = fromArea;
    }

    private void OnChipDragEnd()
    {
        _draggedFieldKey = null;
        _draggedFromArea = null;
    }

    private void OnZoneDragOver()
    {
        // preventDefault is handled in the razor template
    }

    private void OnZoneDrop(PivotArea targetArea)
    {
        if (_draggedFieldKey is null) return;

        MoveFieldToArea(_draggedFieldKey, _draggedFromArea ?? PivotArea.Unused, targetArea);

        _draggedFieldKey = null;
        _draggedFromArea = null;
    }

    // ── Field Movement ───────────────────────────────────────────

    private void MoveFieldToArea(string fieldKey, PivotArea fromArea, PivotArea toArea)
    {
        if (fromArea == toArea) return;

        // Remove from source
        RemoveFieldFromArea(fieldKey, fromArea);

        // Add to target
        switch (toArea)
        {
            case PivotArea.Row:
                if (!_draftRowFields.Contains(fieldKey))
                    _draftRowFields.Add(fieldKey);
                break;
            case PivotArea.Column:
                if (!_draftColumnFields.Contains(fieldKey))
                    _draftColumnFields.Add(fieldKey);
                break;
            case PivotArea.Data:
                if (!_draftValueFields.Any(v => v.FieldKey == fieldKey))
                    _draftValueFields.Add(new PivotValueFieldConfiguration { FieldKey = fieldKey, Aggregation = "Sum" });
                break;
            case PivotArea.Filter:
                if (!_draftFilterFields.ContainsKey(fieldKey))
                    _draftFilterFields[fieldKey] = [];
                break;
        }

        StateHasChanged();
    }

    private void RemoveFieldFromArea(string fieldKey, PivotArea area)
    {
        switch (area)
        {
            case PivotArea.Row:
                _draftRowFields.Remove(fieldKey);
                break;
            case PivotArea.Column:
                _draftColumnFields.Remove(fieldKey);
                break;
            case PivotArea.Data:
                _draftValueFields.RemoveAll(v => v.FieldKey == fieldKey);
                if (_editingValueFieldIndex.HasValue)
                    _editingValueFieldIndex = null;
                break;
            case PivotArea.Filter:
                _draftFilterFields.Remove(fieldKey);
                break;
        }
    }

    private void RemoveField(string fieldKey, PivotArea area)
    {
        RemoveFieldFromArea(fieldKey, area);
        StateHasChanged();
    }

    // ── Value Field Settings ─────────────────────────────────────

    private void ToggleValueFieldSettings(int index)
    {
        _editingValueFieldIndex = _editingValueFieldIndex == index ? null : index;
    }

    private void HandleAggregationChange(int index, ChangeEventArgs e)
    {
        var aggregation = e.Value?.ToString() ?? "Sum";
        UpdateValueFieldAggregation(index, aggregation);
    }

    private void UpdateValueFieldAggregation(int index, string aggregation)
    {
        if (index < 0 || index >= _draftValueFields.Count) return;
        _draftValueFields[index] = new PivotValueFieldConfiguration
        {
            FieldKey = _draftValueFields[index].FieldKey,
            Aggregation = aggregation,
            DisplayName = _draftValueFields[index].DisplayName,
            Format = _draftValueFields[index].Format
        };
    }

    private void UpdateValueFieldDisplayName(int index, string displayName)
    {
        if (index < 0 || index >= _draftValueFields.Count) return;
        _draftValueFields[index] = new PivotValueFieldConfiguration
        {
            FieldKey = _draftValueFields[index].FieldKey,
            Aggregation = _draftValueFields[index].Aggregation,
            DisplayName = displayName,
            Format = _draftValueFields[index].Format
        };
    }

    // ── Filter Management ────────────────────────────────────────

    private IReadOnlyList<object?> GetDistinctFieldValues(string fieldKey)
    {
        var field = Fields.FirstOrDefault(f => f.Key == fieldKey);
        if (field is null || Items is null) return [];

        return Items
            .Select(item => field.Accessor(item))
            .Where(v => v is not null)
            .Distinct()
            .OrderBy(v => v?.ToString())
            .ToList();
    }

    private bool IsFilterValueSelected(string fieldKey, object? value)
    {
        if (!_draftFilterFields.TryGetValue(fieldKey, out var selected))
            return false;
        return selected.Any(v => Equals(v, value));
    }

    private void ToggleFilterValue(string fieldKey, object? value)
    {
        if (!_draftFilterFields.TryGetValue(fieldKey, out var selected))
        {
            _draftFilterFields[fieldKey] = [value];
            StateHasChanged();
            return;
        }

        var existing = selected.FirstOrDefault(v => Equals(v, value));
        if (existing is not null)
        {
            selected.Remove(existing);
            if (selected.Count == 0)
                _draftFilterFields.Remove(fieldKey);
        }
        else
        {
            selected.Add(value);
        }

        StateHasChanged();
    }

    private void SelectAllFilterValues(string fieldKey)
    {
        var values = GetDistinctFieldValues(fieldKey);
        _draftFilterFields[fieldKey] = values.ToList();
        StateHasChanged();
    }

    private void ClearFilterValues(string fieldKey)
    {
        _draftFilterFields.Remove(fieldKey);
        StateHasChanged();
    }

    private void ToggleFilterEditor(string fieldKey)
    {
        _expandedFilterFieldKey = _expandedFilterFieldKey == fieldKey ? null : fieldKey;
        StateHasChanged();
    }

    // ── Actions ──────────────────────────────────────────────────

    private async Task ApplyAsync()
    {
        var config = new PivotTableConfiguration
        {
            RowFieldKeys = _draftRowFields.ToList(),
            ColumnFieldKeys = _draftColumnFields.ToList(),
            ValueFields = _draftValueFields.ToList(),
            FilterFields = new Dictionary<string, List<object?>>(_draftFilterFields)
        };

        _editingValueFieldIndex = null;
        _expandedFilterFieldKey = null;
        await OnConfigurationChanged.InvokeAsync(config);
    }

    private void Reset()
    {
        SyncDraftState();
        _editingValueFieldIndex = null;
        _expandedFilterFieldKey = null;
        StateHasChanged();
    }

    private void ClearAll()
    {
        _draftRowFields.Clear();
        _draftColumnFields.Clear();
        _draftValueFields.Clear();
        _draftFilterFields.Clear();
        _editingValueFieldIndex = null;
        _expandedFilterFieldKey = null;
        StateHasChanged();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private List<PivotField<TItem>> GetFieldsInArea(PivotArea area) => area switch
    {
        PivotArea.Row => _draftRowFields.Select(k => Fields.FirstOrDefault(f => f.Key == k)).Where(f => f is not null).ToList()!,
        PivotArea.Column => _draftColumnFields.Select(k => Fields.FirstOrDefault(f => f.Key == k)).Where(f => f is not null).ToList()!,
        PivotArea.Data => _draftValueFields.Select(v => Fields.FirstOrDefault(f => f.Key == v.FieldKey)).Where(f => f is not null).ToList()!,
        PivotArea.Filter => _draftFilterFields.Keys.Select(k => Fields.FirstOrDefault(f => f.Key == k)).Where(f => f is not null).ToList()!,
        _ => Fields.Where(f => !IsFieldUsed(f.Key)).ToList()
    };

    private bool IsFieldUsed(string fieldKey) =>
        _draftRowFields.Contains(fieldKey) ||
        _draftColumnFields.Contains(fieldKey) ||
        _draftValueFields.Any(v => v.FieldKey == fieldKey) ||
        _draftFilterFields.ContainsKey(fieldKey);

    private static string GetZoneTitle(PivotArea area) => area switch
    {
        PivotArea.Row => "TmPivotTable_RowFields",
        PivotArea.Column => "TmPivotTable_ColumnFields",
        PivotArea.Data => "TmPivotTable_ValueFields",
        PivotArea.Filter => "TmPivotTable_FilterFields",
        _ => "TmPivotTable_AvailableFields"
    };

    private static string GetZoneClass(PivotArea area) => area switch
    {
        PivotArea.Row => "tm-pivot-zone--row",
        PivotArea.Column => "tm-pivot-zone--column",
        PivotArea.Data => "tm-pivot-zone--value",
        PivotArea.Filter => "tm-pivot-zone--filter",
        _ => "tm-pivot-zone--unused"
    };
}
