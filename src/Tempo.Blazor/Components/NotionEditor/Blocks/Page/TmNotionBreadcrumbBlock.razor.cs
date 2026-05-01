using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Page;

public partial class TmNotionBreadcrumbBlock : ComponentBase
{
    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock               Block    { get; set; } = default!;

    [Parameter]
    public IBreadcrumbBlockContent? Content  { get; set; }

    [Parameter] public bool         ReadOnly  { get; set; }
    [Parameter] public EventCallback OnFocused { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private record BreadcrumbCrumb(Guid PageId, string Title, string? Icon);

    private List<BreadcrumbCrumb> _crumbs    = [];
    private bool                  _isLoading = true;
    private Guid?                 _lastPageId;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (Block.PageId == _lastPageId) return;
        _lastPageId = Block.PageId;
        await LoadCrumbsAsync();
    }

    // ── Crumb loading ─────────────────────────────────────────────────────────

    private async Task LoadCrumbsAsync()
    {
        _isLoading = true;
        _crumbs    = [];
        StateHasChanged();

        try
        {
            var stack  = new Stack<BreadcrumbCrumb>();
            Guid? pageId = Block.PageId;

            while (pageId.HasValue)
            {
                INotionPage page;
                try
                {
                    page = await Context.DataProvider.GetPageAsync(pageId.Value.ToString());
                }
                catch
                {
                    break;
                }

                stack.Push(new BreadcrumbCrumb(page.Id, page.Title, page.IconEmoji));
                pageId = page.ParentId;
            }

            _crumbs = [.. stack];
        }
        catch { _crumbs = []; }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async Task HandleCrumbClickAsync(Guid pageId)
    {
        await OnFocused.InvokeAsync();
        if (Context.NavigateTo is not null)
            await Context.NavigateTo(pageId.ToString());
    }
}
