using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>
/// Toolbox panel showing available diagram stencils that can be dragged onto the canvas.
/// </summary>
public partial class TmDiagramToolbox : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised when a stencil drag starts.</summary>
    [Parameter] public EventCallback<string> StencilDragStart { get; set; }

    private bool _collapsed;
    private readonly HashSet<string> _collapsedCategories = new();
    private ElementReference _containerRef;
    private bool _initialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_initialized)
        {
            _initialized = true;
            await JS.InvokeVoidAsync("tmDiagramEditor.initToolbox", _containerRef);
        }
    }

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
    }

    private void ToggleCategory(string category)
    {
        if (_collapsedCategories.Contains(category))
            _collapsedCategories.Remove(category);
        else
            _collapsedCategories.Add(category);
    }

    private async Task OnDragStart(DragEventArgs e, string stencilId)
    {
        await StencilDragStart.InvokeAsync(stencilId);
    }
}
