using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Sidebar;

public partial class TmNotionSidebar : ComponentBase, IAsyncDisposable
{
    // ── DI ────────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ──────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>ID of the currently active (open) page — highlights it in the tree.</summary>
    [Parameter] public string? ActivePageId { get; set; }

    /// <summary>Raised when the user requests to open the full trash view.</summary>
    [Parameter] public EventCallback OnTrashRequested { get; set; }

    // ── State — sections ──────────────────────────────────────────────────────

    private bool _pagesExpanded = true;
    private bool _showTrash     = false;

    // ── State — data ──────────────────────────────────────────────────────────

    private IReadOnlyList<INotionPage> _favorites  = [];
    private IReadOnlyList<INotionPage> _recent     = [];
    private IReadOnlyList<INotionPage> _rootPages  = [];
    private int                        _trashCount = 0;

    private bool    _isLoading = true;
    private string? _loadError;

    // ── State — search panel ──────────────────────────────────────────────────

    private bool                       _searchOpen    = false;
    private string                     _searchQuery   = string.Empty;
    private IReadOnlyList<INotionPage> _searchResults = [];
    private bool                       _searchLoading = false;
    private string?                    _searchError;
    private ElementReference           _searchInputRef;

    // ── State — new page ──────────────────────────────────────────────────────

    private bool _isCreatingPage = false;

    // ── Resize ───────────────────────────────────────────────────────────────

    private ElementReference _resizeHandleRef;
    private DotNetObjectReference<TmNotionSidebar>? _selfRef;

    // ── Computed ──────────────────────────────────────────────────────────────

    private bool HasSearchProvider => Context.SearchProvider is not null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadAllAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _selfRef = DotNetObjectReference.Create(this);

        try
        {
            await JS.InvokeVoidAsync(
                "tmNotionEditor.initSidebarResize",
                _resizeHandleRef,
                _selfRef,
                180, 520);
        }
        catch { /* JS unavailable in SSR / test */ }
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task LoadAllAsync()
    {
        _isLoading = true;
        _loadError = null;
        StateHasChanged();

        try
        {
            var tFav    = Context.DataProvider.GetFavoritesAsync();
            var tRecent = Context.DataProvider.GetRecentPagesAsync(10);
            var tPages  = Context.DataProvider.GetChildPagesAsync(null);
            var tTrash  = Context.DataProvider.GetTrashAsync();

            await Task.WhenAll(tFav, tRecent, tPages, tTrash);

            _favorites  = (await tFav).ToList();
            _recent     = (await tRecent).ToList();
            _rootPages  = (await tPages).ToList();
            _trashCount = (await tTrash).Count();
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async Task NavigateToAsync(INotionPage page)
    {
        if (Context.NavigateTo is null) return;
        await Context.NavigateTo(page.Id.ToString());
    }

    // ── New page ──────────────────────────────────────────────────────────────

    private async Task CreateNewPageAsync()
    {
        if (_isCreatingPage) return;
        _isCreatingPage = true;
        StateHasChanged();

        try
        {
            var page = await Context.DataProvider.CreatePageAsync(null, string.Empty);
            _rootPages = _rootPages.Append(page).ToList();
            StateHasChanged();

            if (Context.NavigateTo is not null)
                await Context.NavigateTo(page.Id.ToString());
        }
        catch { }
        finally
        {
            _isCreatingPage = false;
            StateHasChanged();
        }
    }

    // ── Search panel ──────────────────────────────────────────────────────────

    private async Task OpenSearchAsync()
    {
        _searchOpen    = true;
        _searchQuery   = string.Empty;
        _searchResults = [];
        _searchError   = null;
        StateHasChanged();

        try
        {
            await Task.Delay(50); // let Blazor render the input first
            await JS.InvokeVoidAsync("tmNotionEditor.focus", _searchInputRef);
        }
        catch { }
    }

    private void CloseSearch()
    {
        _searchOpen    = false;
        _searchQuery   = string.Empty;
        _searchResults = [];
        _searchError   = null;
    }

    private async Task OnSearchInputAsync(ChangeEventArgs e)
    {
        _searchQuery = e.Value?.ToString() ?? string.Empty;
        _searchError = null;

        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults = [];
            return;
        }

        if (Context.SearchProvider is null) return;

        _searchLoading = true;
        StateHasChanged();

        try
        {
            var results = await Context.SearchProvider.SearchPagesAsync(_searchQuery, null);
            _searchResults = results.ToList();
        }
        catch (Exception ex)
        {
            _searchError   = ex.Message;
            _searchResults = [];
        }
        finally
        {
            _searchLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnSearchKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") CloseSearch();
        else if (e.Key == "Enter" && _searchResults.Count == 1)
        {
            CloseSearch();
            await NavigateToAsync(_searchResults[0]);
        }
    }

    // ── Tree / Favorites changed ──────────────────────────────────────────────

    private async Task OnTreeChangedAsync()     => await LoadAllAsync();
    private async Task OnFavoritesChangedAsync() => await LoadAllAsync();

    // ── Trash ─────────────────────────────────────────────────────────────────

    private async Task OpenTrashAsync()
    {
        _showTrash = true;
        await OnTrashRequested.InvokeAsync();
    }

    private async Task CloseTrashAsync()
    {
        _showTrash = false;
        await LoadAllAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetPageIcon(INotionPage page) =>
        string.IsNullOrEmpty(page.IconEmoji) ? "📄" : page.IconEmoji;

    private string GetPageTitle(INotionPage page) =>
        string.IsNullOrWhiteSpace(page.Title)
            ? Loc["TmNotionSidebar_Untitled"]
            : page.Title;

    private bool IsActivePage(INotionPage page) =>
        ActivePageId is not null &&
        page.Id.ToString().Equals(ActivePageId, StringComparison.OrdinalIgnoreCase);

    // ── JS interop callback ───────────────────────────────────────────────────

    [JSInvokable]
    public void OnSidebarResized(int width) { /* width persisted by JS in localStorage */ }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.destroySidebarResize", _resizeHandleRef);
        }
        catch { }

        _selfRef?.Dispose();
    }
}
