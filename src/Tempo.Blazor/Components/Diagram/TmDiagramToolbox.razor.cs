using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Diagram.Models;

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

    private string _searchQuery = string.Empty;
    private List<string> _filteredCategories = new();
    private Dictionary<string, List<DiagramStencil>> _filteredStencilsByCategory = new();

    protected override void OnInitialized()
    {
        RefreshFilter();
    }

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

    private void OnSearchChanged(string value)
    {
        _searchQuery = value;
        RefreshFilter();
    }

    private void RefreshFilter()
    {
        var query = _searchQuery.Trim();
        var allCategories = StencilRegistry.GetCategories();
        var categories = new List<string>();
        var stencilsByCategory = new Dictionary<string, List<DiagramStencil>>();

        foreach (var cat in allCategories)
        {
            var stencils = StencilRegistry.GetByCategory(cat)
                .Where(s => string.IsNullOrEmpty(query)
                    || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (stencils.Count > 0)
            {
                categories.Add(cat);
                stencilsByCategory[cat] = stencils;
            }
        }

        _filteredCategories = categories;
        _filteredStencilsByCategory = stencilsByCategory;
    }
}
