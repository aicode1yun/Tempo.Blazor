using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database;

public partial class TmNotionDbGroupBar : ComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired] public IReadOnlyList<IDatabaseField> Fields   { get; set; } = [];
    [Parameter]                 public NotionDatabaseGrouping?        Grouping { get; set; }
    [Parameter]                 public bool                           ReadOnly { get; set; }

    [Parameter] public EventCallback<NotionDatabaseGrouping?> OnGroupingChanged { get; set; }
    [Parameter] public EventCallback                          OnClose           { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private Guid?         _selectedFieldId;
    private bool          _hideEmptyGroups;
    private SortDirection _sortDirection = SortDirection.Ascending;
    private bool          _initialized;

    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _selectedFieldId = Grouping?.FieldId;
            _hideEmptyGroups = Grouping?.HideEmptyGroups ?? false;
            _sortDirection   = Grouping?.SortDirection   ?? SortDirection.Ascending;
            _initialized     = true;
        }
    }

    // ── Groupable field types ─────────────────────────────────────────────────

    internal static readonly HashSet<DatabaseFieldType> GroupableTypes =
    [
        DatabaseFieldType.Select,
        DatabaseFieldType.MultiSelect,
        DatabaseFieldType.Status,
        DatabaseFieldType.Person,
        DatabaseFieldType.Checkbox,
        DatabaseFieldType.Date,
        DatabaseFieldType.DateRange,
        DatabaseFieldType.CreatedTime,
        DatabaseFieldType.LastEditedTime,
        DatabaseFieldType.CreatedBy,
        DatabaseFieldType.LastEditedBy
    ];

    internal IEnumerable<IDatabaseField> GroupableFields =>
        Fields.Where(f => GroupableTypes.Contains(f.Type));

    // ── Mutations ─────────────────────────────────────────────────────────────

    private async Task SelectFieldAsync(Guid? fieldId)
    {
        _selectedFieldId = fieldId;
        await EmitAsync();
    }

    private async Task ToggleHideEmptyAsync(bool value)
    {
        _hideEmptyGroups = value;
        await EmitAsync();
    }

    private async Task SetSortDirectionAsync(SortDirection dir)
    {
        _sortDirection = dir;
        await EmitAsync();
    }

    // ── Emit ──────────────────────────────────────────────────────────────────

    private async Task EmitAsync()
    {
        var grouping = _selectedFieldId.HasValue
            ? new NotionDatabaseGrouping(_selectedFieldId.Value, _hideEmptyGroups, _sortDirection)
            : (NotionDatabaseGrouping?)null;
        await OnGroupingChanged.InvokeAsync(grouping);
    }

    // ── Field type icon ───────────────────────────────────────────────────────

    internal static Microsoft.AspNetCore.Components.MarkupString FieldTypeIcon(DatabaseFieldType type) =>
        (Microsoft.AspNetCore.Components.MarkupString)(type switch
        {
            DatabaseFieldType.Select =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><circle cx='12' cy='12' r='9' stroke='currentColor' stroke-width='1.5'/><circle cx='12' cy='12' r='4' fill='currentColor'/></svg>",
            DatabaseFieldType.MultiSelect =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><rect x='2' y='5' width='9' height='6' rx='1' fill='currentColor'/><rect x='13' y='5' width='9' height='6' rx='1' fill='currentColor'/><rect x='2' y='14' width='9' height='6' rx='1' fill='currentColor'/></svg>",
            DatabaseFieldType.Status =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><circle cx='12' cy='12' r='9' stroke='currentColor' stroke-width='1.5'/><path d='M8 12l3 3 5-5' stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round'/></svg>",
            DatabaseFieldType.Person or DatabaseFieldType.CreatedBy or DatabaseFieldType.LastEditedBy =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><circle cx='12' cy='8' r='4' stroke='currentColor' stroke-width='1.5'/><path d='M4 20c0-4 3.6-7 8-7s8 3 8 7' stroke='currentColor' stroke-width='1.5' stroke-linecap='round'/></svg>",
            DatabaseFieldType.Checkbox =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><rect x='3' y='3' width='18' height='18' rx='2' stroke='currentColor' stroke-width='1.5'/><path d='M7 12l4 4 6-6' stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round'/></svg>",
            DatabaseFieldType.Date or DatabaseFieldType.DateRange or
            DatabaseFieldType.CreatedTime or DatabaseFieldType.LastEditedTime =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><rect x='3' y='4' width='18' height='17' rx='2' stroke='currentColor' stroke-width='1.5'/><path d='M16 2v4M8 2v4M3 10h18' stroke='currentColor' stroke-width='1.5' stroke-linecap='round'/></svg>",
            _ =>
                "<svg width='14' height='14' viewBox='0 0 24 24' fill='none' aria-hidden='true'><path d='M4 7h16M4 12h16M4 17h10' stroke='currentColor' stroke-width='1.5' stroke-linecap='round'/></svg>"
        });
}
