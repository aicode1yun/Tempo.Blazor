using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Layout;

/// <summary>
/// A layout manager that arranges panes into dockable areas (top, left, center, right, bottom)
/// and floating overlays. Supports drag-and-drop docking, tab grouping, close and float actions.
/// </summary>
public partial class TmDockManager : ComponentBase
{
    private readonly List<TmDockPane> _panes = [];
    private string? _draggedPaneId;
    private bool _isDragging;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>Child <see cref="TmDockPane"/> definitions.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Optional data-bound pane list. When set, panes are rendered from this list instead of <see cref="ChildContent"/>.</summary>
    [Parameter] public List<DockPane>? Items { get; set; }

    /// <summary>Template used to render each <see cref="DockPane"/> when <see cref="Items"/> is set.</summary>
    [Parameter] public RenderFragment<DockPane>? PaneTemplate { get; set; }

    /// <summary>Fires when a pane is closed.</summary>
    [Parameter] public EventCallback<DockPane> OnPaneClose { get; set; }

    /// <summary>Fires when a pane is floated.</summary>
    [Parameter] public EventCallback<DockPane> OnPaneFloat { get; set; }

    /// <summary>Fires when a pane is docked back from floating.</summary>
    [Parameter] public EventCallback<DockPane> OnPaneDock { get; set; }

    /// <summary>Fires whenever the layout changes (dock, float, close, reorder).</summary>
    [Parameter] public EventCallback<DockLayout> OnLayoutChanged { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Pane registry ────────────────────────────────────────────

    internal IReadOnlyList<TmDockPane> Panes => _panes;

    internal void AddPane(TmDockPane pane)
    {
        if (!_panes.Contains(pane))
        {
            _panes.Add(pane);
            StateHasChanged();
        }
    }

    internal void RemovePane(TmDockPane pane)
    {
        _panes.Remove(pane);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && _panes.Count > 0)
        {
            StateHasChanged();
        }
    }

    // ── Query helpers ────────────────────────────────────────────

    private IReadOnlyList<TmDockPane> GetPanesForPosition(DockPosition position)
    {
        return _panes
            .Where(p => p.IsVisible && p.Position == position)
            .OrderBy(p => p.Order)
            .ToList();
    }

    private static string GetAreaSizeStyle(IReadOnlyList<TmDockPane> panes, bool width)
    {
        var size = panes.FirstOrDefault(p => width ? p.Width.HasValue : p.Height.HasValue);
        if (size is null) return "";
        var value = width ? size.Width!.Value : size.Height!.Value;
        return width ? $"width: {value}px;" : $"height: {value}px;";
    }

    // ── Actions ──────────────────────────────────────────────────

    private void ActivatePane(TmDockPane pane)
    {
        foreach (var p in _panes.Where(x => x.Position == pane.Position))
            p.IsActive = false;
        pane.IsActive = true;
        StateHasChanged();
    }

    private async Task ClosePaneAsync(TmDockPane pane)
    {
        pane.IsVisible = false;
        await OnPaneClose.InvokeAsync(pane.ToModel());
        await SaveLayoutAsync();
        StateHasChanged();
    }

    private async Task FloatPaneAsync(TmDockPane pane)
    {
        pane.Position = DockPosition.Floating;
        await OnPaneFloat.InvokeAsync(pane.ToModel());
        await SaveLayoutAsync();
        StateHasChanged();
    }

    private async Task DockPaneAsync(TmDockPane pane, DockPosition position)
    {
        pane.Position = position;
        pane.IsActive = true;
        await OnPaneDock.InvokeAsync(pane.ToModel());
        await SaveLayoutAsync();
        StateHasChanged();
    }

    // ── Drag & drop ──────────────────────────────────────────────

    private void HandleDragStart(TmDockPane pane)
    {
        _draggedPaneId = pane.Id;
        _isDragging = true;
        StateHasChanged();
    }

    private async Task HandleDropAsync(DockPosition position)
    {
        if (_draggedPaneId is null) return;
        var pane = _panes.FirstOrDefault(p => p.Id == _draggedPaneId);
        if (pane is not null)
        {
            pane.Position = position;
            pane.IsActive = true;
            EnsureSingleActivePerGroup();
            await SaveLayoutAsync();
        }
        _draggedPaneId = null;
        _isDragging = false;
        StateHasChanged();
    }

    private void EnsureSingleActivePerGroup()
    {
        var groups = _panes.Where(p => p.IsVisible).GroupBy(p => p.Position);
        foreach (var g in groups)
        {
            if (g.Count(p => p.IsActive) > 1)
            {
                foreach (var p in g) p.IsActive = false;
                g.First().IsActive = true;
            }
            else if (g.All(p => !p.IsActive) && g.Any())
            {
                g.First().IsActive = true;
            }
        }
    }

    // ── Layout persistence ───────────────────────────────────────

    private async Task SaveLayoutAsync()
    {
        var layout = new DockLayout
        {
            Panes = _panes.Select(p => new DockLayoutPane
            {
                Id = p.Id,
                Position = p.Position,
                IsVisible = p.IsVisible,
                IsActive = p.IsActive,
                Width = p.Width,
                Height = p.Height,
                Order = p.Order,
                FloatX = p.FloatX,
                FloatY = p.FloatY
            }).ToList()
        };
        await OnLayoutChanged.InvokeAsync(layout);
    }
}
