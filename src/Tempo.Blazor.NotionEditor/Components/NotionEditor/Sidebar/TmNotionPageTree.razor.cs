using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Sidebar;

public partial class TmNotionPageTree : ComponentBase
{
    internal enum BulkAction
    {
        Move,
        Copy
    }

    // ── Cascaded context ──────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    [Parameter, EditorRequired] public IReadOnlyList<INotionPage> RootPages    { get; set; } = [];
    [Parameter]                 public string?                    ActivePageId { get; set; }
    [Parameter]                 public EventCallback              OnTreeChanged { get; set; }

    // ── Bulk state ────────────────────────────────────────────────────────────

    private readonly HashSet<string> _selectedPageIds = new(StringComparer.OrdinalIgnoreCase);
    private BulkAction               _bulkAction       = BulkAction.Move;
    private bool                     _bulkPickerOpen;
    private bool                     _bulkDeleteConfirmOpen;
    private string                   _bulkTargetQuery = string.Empty;
    private IReadOnlyList<INotionPage> _bulkTargetResults = [];
    private bool                       _bulkTargetLoading;
    private string                     _bulkError = string.Empty;

    internal int SelectedCount => _selectedPageIds.Count;

    internal bool IsSelected(string pageId) => _selectedPageIds.Contains(pageId);

    internal Task SetSelectedAsync(string pageId, bool selected)
    {
        if (selected)
            _selectedPageIds.Add(pageId);
        else
            _selectedPageIds.Remove(pageId);

        if (_selectedPageIds.Count == 0)
            CloseBulkPanels();

        return InvokeAsync(StateHasChanged);
    }

    // ── Drag state (accessed by TmNotionPageTreeItem) ─────────────────────────

    internal INotionPage? DraggingPage { get; private set; }

    internal void BeginDrag(INotionPage page) => DraggingPage = page;
    internal void EndDrag()                   => DraggingPage = null;

    // ── Provider access (accessed by TmNotionPageTreeItem) ────────────────────

    internal INotionDataProvider    DataProvider   => Context.DataProvider;
    internal INotionSearchProvider? SearchProvider => Context.SearchProvider;

    // ── Mutations ─────────────────────────────────────────────────────────────

    internal async Task NavigateToAsync(string pageId)
    {
        if (Context.NavigateTo is not null)
            await Context.NavigateTo(pageId);
    }

    internal async Task RenameAsync(INotionPage page, string newTitle)
    {
        var updated = new NotionPage
        {
            Id                  = page.Id,
            ParentId            = page.ParentId,
            Title               = newTitle,
            Description         = page.Description,
            IconEmoji           = page.IconEmoji,
            IconImageUrl        = page.IconImageUrl,
            CoverImageUrl       = page.CoverImageUrl,
            CoverImagePositionY = page.CoverImagePositionY,
            IsFullWidth         = page.IsFullWidth,
            IsSmallText         = page.IsSmallText,
            IsLocked            = page.IsLocked,
            CreatedAt           = page.CreatedAt,
            CreatedByUserId     = page.CreatedByUserId,
            LastEditedAt        = page.LastEditedAt,
            LastEditedByUserId  = page.LastEditedByUserId,
            IsDeleted           = page.IsDeleted,
            DeletedAt           = page.DeletedAt,
            IsFavorite          = page.IsFavorite,
        };

        await Context.DataProvider.UpdatePageAsync(updated);
        await OnTreeChanged.InvokeAsync();
    }

    internal async Task DuplicateAsync(string pageId)
    {
        await Context.DataProvider.DuplicatePageAsync(pageId);
        await OnTreeChanged.InvokeAsync();
    }

    internal async Task DeleteAsync(string pageId)
    {
        await Context.DataProvider.DeletePageAsync(pageId);
        await OnTreeChanged.InvokeAsync();
    }

    internal async Task ToggleFavoriteAsync(string pageId, bool makeFavorite)
    {
        await Context.DataProvider.ToggleFavoriteAsync(pageId, makeFavorite);
        await OnTreeChanged.InvokeAsync();
    }

    internal async Task MoveAsync(string pageId, string? newParentId)
    {
        await Context.DataProvider.MovePageAsync(pageId, newParentId);
        await OnTreeChanged.InvokeAsync();
    }

    internal async Task AddChildAsync(string parentId)
    {
        var page = await Context.DataProvider.CreatePageAsync(parentId, string.Empty);
        await OnTreeChanged.InvokeAsync();

        if (Context.NavigateTo is not null)
            await Context.NavigateTo(page.Id.ToString());
    }

    private void OpenBulkTargetPicker(BulkAction action)
    {
        _bulkAction            = action;
        _bulkPickerOpen        = true;
        _bulkDeleteConfirmOpen = false;
        _bulkTargetQuery       = string.Empty;
        _bulkTargetResults     = [];
        _bulkError             = string.Empty;
    }

    private void OpenBulkDeleteConfirm()
    {
        _bulkDeleteConfirmOpen = true;
        _bulkPickerOpen        = false;
        _bulkError             = string.Empty;
    }

    private void CloseBulkPanels()
    {
        _bulkPickerOpen        = false;
        _bulkDeleteConfirmOpen = false;
        _bulkTargetQuery       = string.Empty;
        _bulkTargetResults     = [];
        _bulkTargetLoading     = false;
    }

    private async Task ClearSelectionAsync()
    {
        _selectedPageIds.Clear();
        CloseBulkPanels();
        _bulkError = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnBulkTargetSearchAsync(ChangeEventArgs e)
    {
        _bulkTargetQuery = e.Value?.ToString() ?? string.Empty;

        if (SearchProvider is null || string.IsNullOrWhiteSpace(_bulkTargetQuery))
        {
            _bulkTargetResults = [];
            return;
        }

        _bulkTargetLoading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var selected = _selectedPageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var results = await SearchProvider.SearchPagesAsync(_bulkTargetQuery, null);
            _bulkTargetResults = results
                .Where(page => !selected.Contains(page.Id.ToString("D")))
                .ToList();
        }
        catch
        {
            _bulkTargetResults = [];
        }
        finally
        {
            _bulkTargetLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ExecuteBulkTargetAsync(INotionPage? targetPage)
    {
        var selected = _selectedPageIds.ToArray();
        if (selected.Length == 0)
            return;

        var targetPageId = targetPage?.Id.ToString("D");

        try
        {
            if (_bulkAction == BulkAction.Move)
                await DataProvider.MovePagesAsync(selected, targetPageId);
            else
            {
                foreach (var pageId in selected)
                    await DataProvider.CopyPageTreeAsync(pageId, targetPageId);
            }

            _selectedPageIds.Clear();
            _bulkError = string.Empty;
            CloseBulkPanels();
            await OnTreeChanged.InvokeAsync();
        }
        catch (Exception) when (_bulkAction == BulkAction.Move)
        {
            _bulkError = Loc["TmNotionPageTree_BulkMoveDescendantError"];
            CloseBulkPanels();
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            _bulkError = Loc["TmNotionPageTree_BulkOperationError"];
            CloseBulkPanels();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ExecuteBulkDeleteAsync()
    {
        var selected = _selectedPageIds.ToArray();
        if (selected.Length == 0)
            return;

        try
        {
            await DataProvider.DeletePagesAsync(selected);
            _selectedPageIds.Clear();
            _bulkError = string.Empty;
            CloseBulkPanels();
            await OnTreeChanged.InvokeAsync();
        }
        catch
        {
            _bulkError = Loc["TmNotionPageTree_BulkOperationError"];
            CloseBulkPanels();
            await InvokeAsync(StateHasChanged);
        }
    }
}
