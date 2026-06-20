using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>
/// A combo box that displays a multi-column grid in its dropdown for rich item selection.
/// </summary>
public partial class TmMultiColumnComboBox<TItem, TValue>
{
    private bool _isOpen;
    private string _filterText = string.Empty;
    private readonly List<MultiColumnComboBoxColumn<TItem>> _columns = [];
    private IReadOnlyList<TItem> _filteredItems = [];

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>Selected value.</summary>
    [Parameter] public TValue? Value { get; set; }

    /// <summary>Fires when the selection changes.</summary>
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    /// <summary>Data source for the dropdown grid.</summary>
    [Parameter] public IReadOnlyList<TItem> Data { get; set; } = [];

    /// <summary>Expression that returns the value for a given item. Required for selection matching.</summary>
    [Parameter] public Func<TItem, TValue> ValueField { get; set; } = default!;

    /// <summary>Expression that returns the display text for the trigger when an item is selected.</summary>
    [Parameter] public Func<TItem, string> TextField { get; set; } = default!;

    /// <summary>Placeholder shown when no value is selected.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Whether the user can filter the dropdown list. Default is <c>true</c>.</summary>
    [Parameter] public bool Filterable { get; set; } = true;

    /// <summary>Placeholder for the filter input.</summary>
    [Parameter] public string? FilterPlaceholder { get; set; }

    /// <summary>Whether to show a clear button. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowClearButton { get; set; } = true;

    /// <summary>Disables the component.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Column definitions for the dropdown grid.</summary>
    [Parameter] public IReadOnlyList<MultiColumnComboBoxColumn<TItem>> Columns { get; set; } = [];

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _columns.Clear();
        if (Columns is not null)
            _columns.AddRange(Columns);
        _filteredItems = ApplyFilter(Data, _filterText);
    }

    // ── Interaction ──────────────────────────────────────────────

    private async Task ToggleAsync()
    {
        if (Disabled) return;
        _isOpen = !_isOpen;
        if (_isOpen)
            _filteredItems = ApplyFilter(Data, _filterText);
    }

    private async Task SelectItemAsync(TItem item)
    {
        if (Disabled || ValueField is null) return;

        var value = ValueField(item);
        await ValueChanged.InvokeAsync(value);
        _isOpen = false;
    }

    private async Task ClearValueAsync()
    {
        if (Disabled) return;
        await ValueChanged.InvokeAsync(default);
    }

    private async Task OnFilterChangedAsync(string text)
    {
        _filterText = text;
        _filteredItems = ApplyFilter(Data, _filterText);
        await InvokeAsync(StateHasChanged);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private IReadOnlyList<TItem> ApplyFilter(IReadOnlyList<TItem> data, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || _columns.Count == 0)
            return data;

        var term = filter.Trim();
        return data.Where(item => _columns.Any(col =>
        {
            var val = col.Field(item)?.ToString();
            return val is not null && val.Contains(term, StringComparison.OrdinalIgnoreCase);
        })).ToList();
    }

    private string GetDisplayText(TValue? value)
    {
        if (value is null || TextField is null || Data is null) return string.Empty;

        var item = Data.FirstOrDefault(i =>
        {
            if (ValueField is null) return false;
            var itemValue = ValueField(i);
            return EqualityComparer<TValue>.Default.Equals(itemValue, value);
        });

        return item is not null ? TextField(item) : value.ToString() ?? string.Empty;
    }

    private bool IsEmptyValue(TValue? value) =>
        EqualityComparer<TValue?>.Default.Equals(value, default);

    private bool IsItemSelected(TItem item)
    {
        if (ValueField is null || Value is null) return false;
        var itemValue = ValueField(item);
        return EqualityComparer<TValue>.Default.Equals(itemValue, Value);
    }

    private string GetItemKey(TItem item)
    {
        if (ValueField is not null)
        {
            var val = ValueField(item);
            return val?.ToString() ?? item?.GetHashCode().ToString() ?? Guid.NewGuid().ToString();
        }
        return item?.GetHashCode().ToString() ?? Guid.NewGuid().ToString();
    }
}
