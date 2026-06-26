using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>
/// Collapsible component palette for the wireframe editor.
/// Items are draggable onto <see cref="TmWireframeDesignerCanvas"/> via HTML5 drag-and-drop.
/// </summary>
public partial class TmWireframeToolbox : ComponentBase
{
    // ── DI ────────────────────────────────────────────────────────────────────

    [Inject] private WireframeComponentRegistry _registry { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Additional CSS class on the toolbox wrapper.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>
    /// Raised when the user activates an item via keyboard (Enter/Space).
    /// Carries the component type string so the parent can add it at a default position.
    /// </summary>
    [Parameter] public EventCallback<string> OnComponentActivated { get; set; }

    /// <summary>Optional application scope used to resolve custom wireframe components.</summary>
    [Parameter] public WireframeComponentScope? ComponentScope { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private ElementReference _containerRef;
    private bool _toolboxJsInit;

    private string _search = "";
    private string _filterMode = FilterAll;

    private const string FilterAll     = "All";
    private const string FilterBuiltIn = "Built-in";
    private const string FilterCustom  = "Custom";

    private static readonly string[] _filters = [FilterAll, FilterBuiltIn, FilterCustom];

    // Cached registry data
    private WireframeComponentDef[] _allDefs     = [];
    private string[]                _allCategories = [];
    private bool                    _hasCustomComponents;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        _allDefs          = _registry.GetAll(ComponentScope).ToArray();
        _allCategories    = _registry.GetCategories(ComponentScope);
        _hasCustomComponents = _allDefs.Any(d => !d.IsBuiltIn);
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _toolboxJsInit) return;
        _toolboxJsInit = true;
        await JS.InvokeVoidAsync("tmWireframeDesigner.initToolbox", _containerRef);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    private IEnumerable<WireframeComponentDef> FilteredDefs
    {
        get
        {
            var defs = _allDefs.AsEnumerable();

            // Filter by built-in / custom
            defs = _filterMode switch
            {
                FilterBuiltIn => defs.Where(d => d.IsBuiltIn),
                FilterCustom  => defs.Where(d => !d.IsBuiltIn),
                _             => defs
            };

            // Filter by search
            if (!string.IsNullOrWhiteSpace(_search))
            {
                var term = _search.Trim();
                defs = defs.Where(d =>
                    d.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    d.Type.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    d.Category.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return defs;
        }
    }

    private IEnumerable<string> _visibleCategories => FilteredDefs
        .Select(d => d.Category)
        .Distinct()
        .OrderBy(c => c);

    private IEnumerable<WireframeComponentDef> GetVisibleItems(string category)
        => FilteredDefs
            .Where(d => d.Category == category)
            .OrderBy(d => d.DisplayName);

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnSearchInput(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? "";
    }

    private void ClearSearch()
    {
        _search = "";
    }

    private void SetFilter(string mode)
    {
        _filterMode = mode;
    }

    private string GetFilterLabel(string filter) => filter switch
    {
        FilterBuiltIn => Loc["TmWireframeToolbox_FilterBuiltIn"],
        FilterCustom  => Loc["TmWireframeToolbox_FilterCustom"],
        _             => Loc["TmWireframeToolbox_FilterAll"],
    };

    /// <summary>
    /// Keyboard activation (Enter or Space on a toolbox item).
    /// Fires <see cref="OnComponentActivated"/> so the parent can add the element
    /// at a default position without requiring drag-and-drop.
    /// </summary>
    private async Task OnItemKeyDown(KeyboardEventArgs e, string componentType)
    {
        if (e.Key is "Enter" or " ")
            await OnComponentActivated.InvokeAsync(componentType);
    }
}
