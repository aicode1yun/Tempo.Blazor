using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionExcerptIncludeBlock : ComponentBase
{
    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    /// <summary>Owning page block used for persistence and focus context.</summary>
    [Parameter] public IPageBlock Block { get; set; } = default!;

    /// <summary>Saved Excerpt Include configuration.</summary>
    [Parameter] public IExcerptIncludeBlockContent? Content { get; set; }

    /// <summary>Whether editing controls are hidden.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised after source page selection changes.</summary>
    [Parameter] public EventCallback<IPageBlock> OnUpdated { get; set; }

    /// <summary>Raised when the block receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    private readonly List<PageChoice> _pageChoices = [];
    private INotionPage? _sourcePage;
    private Guid? _lastSourcePageId;
    private string? _excerptHtml;
    private bool _loadingPages;
    private bool _loadingContent;
    private bool _pageChoicesLoaded;
    private bool _notFound;

    private string SourceSummary
        => _sourcePage is not null ? PageTitle(_sourcePage) : Loc["Notion_ExcerptInclude_Select"];

    protected override async Task OnParametersSetAsync()
    {
        if (!_pageChoicesLoaded && !ReadOnly)
        {
            await LoadPageChoicesAsync();
        }

        var sourcePageId = Content?.SourcePageId;
        if (sourcePageId == _lastSourcePageId)
        {
            return;
        }

        _lastSourcePageId = sourcePageId;
        await LoadSourceAsync(sourcePageId);
    }

    private async Task LoadPageChoicesAsync()
    {
        _loadingPages = true;
        try
        {
            _pageChoices.Clear();
            var visited = new HashSet<Guid>();
            var roots = await Context.DataProvider.GetChildPagesAsync(null);
            foreach (var page in roots.Where(page => !page.IsDeleted).OrderBy(PageTitle, StringComparer.OrdinalIgnoreCase))
            {
                await AddPageChoiceAsync(page, 0, visited);
            }
        }
        finally
        {
            _pageChoicesLoaded = true;
            _loadingPages = false;
        }
    }

    private async Task AddPageChoiceAsync(INotionPage page, int depth, HashSet<Guid> visited)
    {
        if (!visited.Add(page.Id) || page.IsDeleted)
        {
            return;
        }

        _pageChoices.Add(new PageChoice(page.Id, PageTitle(page), Math.Min(depth, 6)));

        IEnumerable<INotionPage> children;
        try
        {
            children = await Context.DataProvider.GetChildPagesAsync(page.Id.ToString("D"));
        }
        catch
        {
            return;
        }

        foreach (var child in children.Where(child => !child.IsDeleted).OrderBy(PageTitle, StringComparer.OrdinalIgnoreCase))
        {
            await AddPageChoiceAsync(child, depth + 1, visited);
        }
    }

    private async Task LoadSourceAsync(Guid? sourcePageId)
    {
        _sourcePage = null;
        _excerptHtml = null;
        _notFound = false;

        if (sourcePageId is null)
        {
            return;
        }

        _loadingContent = true;
        try
        {
            INotionPage? page;
            try
            {
                page = await Context.DataProvider.GetPageAsync(sourcePageId.Value.ToString("D"));
            }
            catch
            {
                page = null;
            }

            if (page is null || page.IsDeleted)
            {
                _notFound = true;
                return;
            }

            _sourcePage = page;
            var blocks = await Context.BlockProvider.GetBlocksAsync(sourcePageId.Value.ToString("D"));
            var excerpt = blocks
                .Where(block => block.Type == BlockType.Excerpt)
                .OrderBy(block => block.Order)
                .Select(block => block.Content as IExcerptBlockContent)
                .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content?.Html));

            _excerptHtml = Sanitize(excerpt?.Html);
        }
        finally
        {
            _loadingContent = false;
        }
    }

    private async Task HandleSourceChangedAsync(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        var sourcePageId = Guid.TryParse(value, out var parsed) ? parsed : (Guid?)null;
        var updated = new PageBlock
        {
            Id = Block.Id,
            PageId = Block.PageId,
            ParentBlockId = Block.ParentBlockId,
            Type = BlockType.ExcerptInclude,
            Order = Block.Order,
            Content = new ExcerptIncludeBlockContent { SourcePageId = sourcePageId },
            CreatedAt = Block.CreatedAt,
            LastEditedAt = DateTime.UtcNow
        };

        await Context.BlockProvider.UpdateBlockAsync(updated);
        await OnUpdated.InvokeAsync(updated);
        Block = updated;
        Content = (IExcerptIncludeBlockContent)updated.Content;
        _lastSourcePageId = null;
        await LoadSourceAsync(sourcePageId);
    }

    private Task NavigateToSourceAsync()
        => _sourcePage is not null && Context.NavigateTo is not null
            ? Context.NavigateTo(_sourcePage.Id.ToString("D"))
            : Task.CompletedTask;

    private Task HandleFocusedAsync(MouseEventArgs _)
        => OnFocused.InvokeAsync();

    private string ChoiceLabel(PageChoice choice)
        => choice.Depth == 0
            ? choice.Title
            : $"{new string(' ', choice.Depth * 2)}{choice.Title}";

    private string PageTitle(INotionPage page)
        => string.IsNullOrWhiteSpace(page.Title) ? Loc["TmNotionEditor_Untitled"] : page.Title;

    private static string Sanitize(string? html)
        => NotionInlineHtmlSanitizer.SanitizeHtmlFragment(html);

    private sealed record PageChoice(Guid Id, string Title, int Depth);
}
