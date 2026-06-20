using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.Layout;

/// <summary>Renders a resizable two-pane splitter layout.</summary>
public partial class TmSplitter : ComponentBase
{
    private ElementReference _splitterRef;
    private readonly List<TmSplitterPane> _panes = new();
    private TmSplitterPane? _activePane;
    private double _dragStartX;
    private double _dragStartY;

    /// <summary>The child content (TmSplitterPane elements).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Splitter orientation.</summary>
    [Parameter] public SplitterOrientation Orientation { get; set; } = SplitterOrientation.Horizontal;

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    internal IReadOnlyList<TmSplitterPane> Panes => _panes;

    internal bool IsNotLastPane(TmSplitterPane pane) => _panes.IndexOf(pane) < _panes.Count - 1;

    internal void AddPane(TmSplitterPane pane)
    {
        if (!_panes.Contains(pane))
        {
            _panes.Add(pane);
        }
    }

    internal void RemovePane(TmSplitterPane pane)
    {
        _panes.Remove(pane);
    }

    internal void HandleResizerPointerDown(PointerEventArgs e, TmSplitterPane leftPane)
    {
        _activePane = leftPane;
        _dragStartX = e.ClientX;
        _dragStartY = e.ClientY;
    }

    internal void RequestStateUpdate() => StateHasChanged();
}
