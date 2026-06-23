using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Sidebar;

public partial class TmNotionSidebarTrash : ComponentBase
{
    // ── DI ────────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ──────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    [Parameter] public EventCallback OnClosed { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private IReadOnlyList<INotionPage> _allItems    = [];
    private string                     _filterQuery = string.Empty;
    private bool                       _isLoading   = true;
    private string?                    _loadError;
    private ElementReference           _searchInputRef;

    private HashSet<Guid> _pendingDelete = [];

    // ── Computed ──────────────────────────────────────────────────────────────

    private IReadOnlyList<INotionPage> FilteredItems =>
        string.IsNullOrWhiteSpace(_filterQuery)
            ? _allItems
            : _allItems
                .Where(p => (p.Title ?? string.Empty)
                    .Contains(_filterQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

    private string GetTitle(INotionPage page) =>
        string.IsNullOrWhiteSpace(page.Title)
            ? Loc["TmNotionSidebarTrash_Untitled"]
            : page.Title;

    private string GetIcon(INotionPage page) =>
        string.IsNullOrEmpty(page.IconEmoji) ? "📄" : page.IconEmoji;

    private string GetDeletedAt(INotionPage page)
    {
        if (page.DeletedAt is null) return string.Empty;

        var diff = DateTime.UtcNow - page.DeletedAt.Value;

        if (diff.TotalHours < 24)
            return Loc["TmNotionSidebarTrash_DeletedToday"];

        if (diff.TotalDays < 30)
            return string.Format(Loc["TmNotionSidebarTrash_DeletedDaysAgo"], (int)diff.TotalDays);

        return string.Format(
            Loc["TmNotionSidebarTrash_DeletedOn"],
            page.DeletedAt.Value.ToLocalTime().ToString("MMM d, yyyy"));
    }

    private bool IsConfirming(Guid id) => _pendingDelete.Contains(id);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            await Task.Delay(30);
            await JS.InvokeVoidAsync("tmNotionEditor.focus", _searchInputRef);
        }
        catch { }
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    internal async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;
        StateHasChanged();

        try
        {
            var result = await Context.DataProvider.GetTrashAsync();
            _allItems  = result
                .OrderByDescending(p => p.DeletedAt ?? DateTime.MinValue)
                .ToList();
        }
        catch
        {
            _loadError = Loc["TmNotionSidebarTrash_LoadError"];
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void OnFilterInput(ChangeEventArgs e)
    {
        _filterQuery = e.Value?.ToString() ?? string.Empty;
    }

    private void OnFilterKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            _filterQuery = string.Empty;
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    private async Task RestoreAsync(INotionPage page)
    {
        try
        {
            await Context.DataProvider.RestorePageAsync(page.Id.ToString());
            await LoadAsync();
        }
        catch { }
    }

    // ── Permanent delete ──────────────────────────────────────────────────────

    private void BeginDeleteConfirm(Guid id)   => _pendingDelete.Add(id);
    private void CancelDeleteConfirm(Guid id)  => _pendingDelete.Remove(id);

    private async Task ConfirmPermanentDeleteAsync(INotionPage page)
    {
        _pendingDelete.Remove(page.Id);

        try
        {
            await Context.DataProvider.PermanentlyDeletePageAsync(page.Id.ToString());
            await LoadAsync();
        }
        catch { }
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    private async Task CloseAsync()
    {
        await OnClosed.InvokeAsync();
    }
}
