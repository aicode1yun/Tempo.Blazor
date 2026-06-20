using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database;

public partial class TmNotionDbSortBar : ComponentBase
{
    internal sealed class SortModel
    {
        public Guid          Id        { get; } = Guid.NewGuid();
        public Guid?         FieldId   { get; set; }
        public SortDirection Direction { get; set; } = SortDirection.Ascending;
    }

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired] public IReadOnlyList<IDatabaseField>      Fields   { get; set; } = [];
    [Parameter]                 public IReadOnlyList<NotionDatabaseSort>?  Sorts    { get; set; }
    [Parameter]                 public bool                                ReadOnly { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<NotionDatabaseSort>> OnSortsChanged { get; set; }
    [Parameter] public EventCallback                                     OnClose        { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private List<SortModel> _sorts       = [];
    private bool            _initialized;
    private SortModel?      _dragging;
    private SortModel?      _dragOver;

    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _sorts       = Sorts is { Count: > 0 } ? FromSorts(Sorts) : [];
            _initialized = true;
        }
    }

    // ── Conversions ───────────────────────────────────────────────────────────

    private static List<SortModel> FromSorts(IReadOnlyList<NotionDatabaseSort> sorts) =>
        sorts
            .Select(s => new SortModel { FieldId = s.FieldId, Direction = s.Direction })
            .ToList();

    private static IReadOnlyList<NotionDatabaseSort> ToSorts(List<SortModel> sorts) =>
        sorts
            .Where(s => s.FieldId.HasValue)
            .Select(s => new NotionDatabaseSort(s.FieldId!.Value, s.Direction))
            .ToList();

    // ── Derived ───────────────────────────────────────────────────────────────

    internal bool IsEmpty         => _sorts.Count == 0;
    public   int  ActiveSortCount => _sorts.Count(s => s.FieldId.HasValue);

    // ── Mutations ─────────────────────────────────────────────────────────────

    private async Task AddSortAsync()
    {
        var usedIds = _sorts.Where(s => s.FieldId.HasValue).Select(s => s.FieldId!.Value).ToHashSet();
        var first   = Fields.FirstOrDefault(f => !usedIds.Contains(f.Id));
        _sorts.Add(new SortModel { FieldId = first?.Id });
        await EmitAsync();
    }

    private async Task RemoveSortAsync(SortModel sort)
    {
        _sorts.Remove(sort);
        await EmitAsync();
    }

    private async Task SetFieldAsync(SortModel sort, string? fieldIdStr)
    {
        sort.FieldId = Guid.TryParse(fieldIdStr, out var id) ? id : (Guid?)null;
        await EmitAsync();
    }

    private async Task SetDirectionAsync(SortModel sort, SortDirection direction)
    {
        sort.Direction = direction;
        await EmitAsync();
    }

    private async Task ClearAllAsync()
    {
        _sorts.Clear();
        await EmitAsync();
    }

    // ── Drag & drop reorder ───────────────────────────────────────────────────

    private void StartDrag(SortModel sort)
    {
        _dragging = sort;
        _dragOver = null;
    }

    private void SetDragOver(SortModel sort)
    {
        if (_dragging is null || _dragging == sort) return;
        _dragOver = sort;
    }

    private async Task DropOnAsync(SortModel target)
    {
        if (_dragging is null || _dragging == target) { EndDrag(); return; }
        var from = _sorts.IndexOf(_dragging);
        var to   = _sorts.IndexOf(target);
        if (from >= 0 && to >= 0)
        {
            _sorts.RemoveAt(from);
            _sorts.Insert(to, _dragging);
        }
        EndDrag();
        await EmitAsync();
    }

    private void EndDrag()
    {
        _dragging = null;
        _dragOver = null;
    }

    // ── Emit ──────────────────────────────────────────────────────────────────

    private async Task EmitAsync()
    {
        await OnSortsChanged.InvokeAsync(ToSorts(_sorts));
    }
}
