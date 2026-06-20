using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.Layout;

/// <summary>A pane within a splitter component.</summary>
public partial class TmSplitterPane : ComponentBase, IDisposable
{
    /// <summary>The content to render inside the pane.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The initial size of the pane (in pixels or percentage).</summary>
    [Parameter] public string? Size { get; set; }

    /// <summary>The minimum size of the pane.</summary>
    [Parameter] public string? MinSize { get; set; }

    /// <summary>The maximum size of the pane.</summary>
    [Parameter] public string? MaxSize { get; set; }

    /// <summary>Whether the pane can be collapsed.</summary>
    [Parameter] public bool Collapsible { get; set; }

    /// <summary>Whether the pane is collapsed.</summary>
    [Parameter] public bool Collapsed { get; set; }

    /// <summary>Event fired when collapsed state changes.</summary>
    [Parameter] public EventCallback<bool> CollapsedChanged { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    /// <summary>Parent splitter.</summary>
    [CascadingParameter] public TmSplitter? Parent { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ElementReference _paneRef;
    private ElementReference _resizerRef;
    private bool _isDragging;
    private double _dragStartPos;
    private double _paneStartSize;
    private string? _dragSize;

    internal string GetCssClass()
    {
        var classes = new System.Text.StringBuilder();
        classes.Append("tm-splitter__pane");
        if (Collapsed) classes.Append(" tm-splitter__pane--collapsed");
        if (Collapsible) classes.Append(" tm-splitter__pane--collapsible");
        if (!string.IsNullOrEmpty(AdditionalCssClass)) classes.Append(' ').Append(AdditionalCssClass);
        return classes.ToString();
    }

    internal string GetStyle()
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_dragSize) && !Collapsed)
        {
            // During/after drag lock the pane to an exact pixel size
            sb.Append($"flex: 0 0 {_dragSize}; ");
        }
        else if (!string.IsNullOrEmpty(Size) && !Collapsed)
        {
            sb.Append($"flex-basis: {Size}; ");
        }
        if (!string.IsNullOrEmpty(MinSize))
        {
            sb.Append($"min-width: {MinSize}; min-height: {MinSize}; ");
        }
        if (!string.IsNullOrEmpty(MaxSize))
        {
            sb.Append($"max-width: {MaxSize}; max-height: {MaxSize}; ");
        }
        if (Collapsed)
        {
            sb.Append("flex: 0 0 auto; overflow: hidden;");
        }
        return sb.ToString();
    }

    protected override void OnInitialized()
    {
        Parent?.AddPane(this);
        base.OnInitialized();
    }

    private async Task HandleResizerPointerDown(PointerEventArgs e)
    {
        if (e.Button != 0) return;

        _isDragging = true;
        _dragStartPos = Parent?.Orientation == SplitterOrientation.Horizontal ? e.ClientX : e.ClientY;

        try
        {
            // Define global helper once, then call it with real arguments.
            // eval() only receives a single string argument; extra args are ignored.
            await JSRuntime.InvokeVoidAsync("eval", @"
                if (!window.TempoSplitter) {
                    window.TempoSplitter = {
                        startDrag: function(pane, resizer, pointerId) {
                            var rect = pane.getBoundingClientRect();
                            resizer.setPointerCapture(pointerId);
                            return { width: rect.width, height: rect.height };
                        }
                    };
                }
            ");
            var rect = await JSRuntime.InvokeAsync<DomRect>("TempoSplitter.startDrag", _paneRef, _resizerRef, e.PointerId);
            if (rect != null)
            {
                _paneStartSize = Parent?.Orientation == SplitterOrientation.Horizontal ? rect.Width : rect.Height;
            }
            else if (!string.IsNullOrEmpty(Size) && Size.EndsWith("px", StringComparison.OrdinalIgnoreCase) && double.TryParse(Size[..^2], NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedSize))
            {
                _paneStartSize = parsedSize;
            }
        }
        catch
        {
            // JS interop may fail in prerendering or tests
        }

        Parent?.HandleResizerPointerDown(e, this);
    }

    private void HandleResizerPointerMove(PointerEventArgs e)
    {
        if (!_isDragging || Parent is null) return;

        var currentPos = Parent.Orientation == SplitterOrientation.Horizontal ? e.ClientX : e.ClientY;
        var delta = currentPos - _dragStartPos;
        var newSize = Math.Max(0, _paneStartSize + delta);

        _dragSize = $"{newSize.ToString("F0", CultureInfo.InvariantCulture)}px";
        Parent.RequestStateUpdate();
    }

    private void HandleResizerPointerUp(PointerEventArgs e)
    {
        _isDragging = false;
    }

    private void ToggleCollapse()
    {
        Collapsed = !Collapsed;
        _ = CollapsedChanged.InvokeAsync(Collapsed);
        Parent?.RequestStateUpdate();
    }

    public void Dispose()
    {
        Parent?.RemovePane(this);
    }

    private class DomRect
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
