using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Sidebar;

public partial class TmNotionPageTree : ComponentBase
{
    // ── Cascaded context ──────────────────────────────────────────────────────

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ────────────────────────────────────────────────────────────

    [Parameter, EditorRequired] public IReadOnlyList<INotionPage> RootPages    { get; set; } = [];
    [Parameter]                 public string?                    ActivePageId { get; set; }
    [Parameter]                 public EventCallback              OnTreeChanged { get; set; }

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
}
