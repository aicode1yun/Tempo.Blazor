using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionIncludePageBlock : ComponentBase
{
    private const int MaxIncludeDepth = 8;

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    [CascadingParameter(Name = "IncludePageStack")]
    private IReadOnlySet<Guid>? IncludePageStack { get; set; }

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter]
    public IIncludePageBlockContent? Content { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public EventCallback<IPageBlock> OnUpdated { get; set; }

    [Parameter]
    public EventCallback OnFocused { get; set; }

    private readonly List<PageChoice> _pageChoices = [];
    private readonly Dictionary<Guid, IReadOnlyList<IPageBlock>> _childBlocksByBlockId = [];

    private List<IPageBlock> _sourceBlocks = [];
    private IReadOnlySet<Guid> _nextIncludeStack = new HashSet<Guid>();
    private INotionPage? _sourcePage;
    private Guid? _lastSourcePageId;
    private string? _lastStackKey;
    private bool _loadingPages;
    private bool _loadingContent;
    private bool _pageChoicesLoaded;
    private bool _notFound;
    private bool _cyclic;
    private bool _tooDeep;

    private string SourceSummary
        => _sourcePage is not null
            ? PageTitle(_sourcePage)
            : Content?.SourcePageId is not null && _notFound
                ? Loc["Notion_IncludePage_MissingSource"]
                : Loc["Notion_IncludePage_Select"];

    protected override async Task OnParametersSetAsync()
    {
        if (!_pageChoicesLoaded && !ReadOnly)
        {
            await LoadPageChoicesAsync();
        }

        var sourcePageId = Content?.SourcePageId;
        var stackKey = BuildStackKey(IncludePageStack);
        if (sourcePageId == _lastSourcePageId && stackKey == _lastStackKey)
        {
            return;
        }

        _lastSourcePageId = sourcePageId;
        _lastStackKey = stackKey;
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
            foreach (var page in roots.Where(page => !page.IsDeleted).OrderBy(PageTitle))
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
            children = await Context.DataProvider.GetChildPagesAsync(page.Id.ToString());
        }
        catch
        {
            return;
        }

        foreach (var child in children.Where(child => !child.IsDeleted).OrderBy(PageTitle))
        {
            await AddPageChoiceAsync(child, depth + 1, visited);
        }
    }

    private async Task LoadSourceAsync(Guid? sourcePageId)
    {
        _sourcePage = null;
        _sourceBlocks = [];
        _childBlocksByBlockId.Clear();
        _notFound = false;
        _cyclic = false;
        _tooDeep = false;
        _nextIncludeStack = BuildNextStack(sourcePageId);

        if (sourcePageId is null)
        {
            return;
        }

        if (IsCyclic(sourcePageId.Value))
        {
            _cyclic = true;
            return;
        }

        if (IsTooDeep())
        {
            _tooDeep = true;
            return;
        }

        _loadingContent = true;
        try
        {
            INotionPage? page;
            try
            {
                page = await Context.DataProvider.GetPageAsync(sourcePageId.Value.ToString());
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

            var blocks = await Context.BlockService.GetBlocksAsync(sourcePageId.Value.ToString());
            _sourceBlocks = [.. blocks.OrderBy(block => block.Order)];
            await LoadChildBlocksAsync(_sourceBlocks, []);
        }
        finally
        {
            _loadingContent = false;
        }
    }

    private async Task LoadChildBlocksAsync(IEnumerable<IPageBlock> blocks, HashSet<Guid> visited)
    {
        foreach (var block in blocks)
        {
            if (!visited.Add(block.Id))
            {
                continue;
            }

            IReadOnlyList<IPageBlock> children;
            try
            {
                var loaded = await Context.BlockService.GetChildBlocksAsync(block.Id.ToString());
                children = [.. loaded.OrderBy(child => child.Order)];
            }
            catch
            {
                children = [];
            }

            if (children.Count == 0)
            {
                continue;
            }

            _childBlocksByBlockId[block.Id] = children;
            await LoadChildBlocksAsync(children, visited);
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
            Type = BlockType.IncludePage,
            Order = Block.Order,
            Content = new IncludePageBlockContent { SourcePageId = sourcePageId },
            CreatedAt = Block.CreatedAt,
            LastEditedAt = DateTime.UtcNow
        };

        await Context.BlockService.UpdateBlockAsync(updated);
        await OnUpdated.InvokeAsync(updated);
        Block = updated;
        Content = (IIncludePageBlockContent)updated.Content;
        await LoadSourceAsync(sourcePageId);
    }

    private Task NavigateToSourceAsync()
        => _sourcePage is not null && Context.NavigateTo is not null
            ? Context.NavigateTo(_sourcePage.Id.ToString())
            : Task.CompletedTask;

    private Task NavigateToPageAsync(Guid pageId)
        => Context.NavigateTo is not null
            ? Context.NavigateTo(pageId.ToString())
            : Task.CompletedTask;

    private async Task OnFocusedAsync()
    {
        if (OnFocused.HasDelegate)
        {
            await OnFocused.InvokeAsync();
        }
    }

    private bool IsCyclic(Guid sourcePageId)
        => sourcePageId == Block.PageId || IncludePageStack?.Contains(sourcePageId) == true;

    private bool IsTooDeep()
        => IncludePageStack?.Count >= MaxIncludeDepth;

    private IReadOnlySet<Guid> BuildNextStack(Guid? sourcePageId)
    {
        HashSet<Guid> stack = IncludePageStack is null
            ? []
            : new HashSet<Guid>(IncludePageStack);
        stack.Add(Block.PageId);
        if (sourcePageId is Guid id)
        {
            stack.Add(id);
        }

        return stack;
    }

    private static string BuildStackKey(IReadOnlySet<Guid>? stack)
        => stack is null || stack.Count == 0
            ? string.Empty
            : string.Join('|', stack.Order());

    private string ChoiceLabel(PageChoice choice)
        => choice.Depth == 0
            ? choice.Title
            : $"{new string(' ', choice.Depth * 2)}{choice.Title}";

    private string PageTitle(INotionPage page)
        => string.IsNullOrWhiteSpace(page.Title) ? Loc["TmNotionEditor_Untitled"] : page.Title;

    private static string SanitizeInline(string? html)
        => NotionInlineHtmlSanitizer.SanitizeHtmlFragment(html);

    private int ListNumber(IPageBlock block)
    {
        var orderedBlocks = _sourceBlocks
            .OrderBy(candidate => candidate.Order)
            .ThenBy(candidate => candidate.Id)
            .ToArray();

        var index = Array.FindIndex(orderedBlocks, candidate => candidate.Id == block.Id);
        if (index < 0)
        {
            return 1;
        }

        var before = 1;
        for (var i = index - 1; i >= 0; i--)
        {
            if (orderedBlocks[i].Type != BlockType.NumberedList)
            {
                break;
            }

            before++;
        }

        return Math.Max(1, before);
    }

    private RenderFragment RenderTable(IPageBlock block) => builder =>
    {
        var rows = _childBlocksByBlockId.TryGetValue(block.Id, out var loadedRows)
            ? loadedRows.Where(row => row.Type == BlockType.TableRow).ToList()
            : [];
        if (rows.Count == 0)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "tm-include-page__state");
            builder.AddContent(2, Loc["Notion_IncludePage_Empty"]);
            builder.CloseElement();
            return;
        }

        var columnCount = TableColumnCount(block, rows);
        builder.OpenElement(3, "table");
        builder.AddAttribute(4, "class", "tm-include-page__table");
        builder.OpenElement(5, "tbody");

        var sequence = 6;
        foreach (var row in rows)
        {
            var rowContent = row.Content as ITableRowBlockContent;
            builder.OpenElement(sequence++, "tr");
            for (var index = 0; index < columnCount; index++)
            {
                var tag = IsHeaderCell(block, row, index) ? "th" : "td";
                builder.OpenElement(sequence++, tag);
                builder.AddMarkupContent(sequence++, SanitizeInline(CellHtml(rowContent, index)));
                builder.CloseElement();
            }
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    private RenderFragment RenderPageLink(IPageBlock block) => builder =>
    {
        var pageId = Guid.Empty;
        var title = Loc["TmNotionEditor_Untitled"].ToString();
        var icon = string.Empty;

        if (block.Content is IChildPageBlockContent childPage)
        {
            pageId = childPage.ChildPageId;
            title = string.IsNullOrWhiteSpace(childPage.Title) ? title : childPage.Title;
            icon = childPage.IconEmoji ?? string.Empty;
        }
        else if (block.Content is ILinkedPageBlockContent linkedPage)
        {
            pageId = linkedPage.LinkedPageId;
            title = string.IsNullOrWhiteSpace(linkedPage.Title) ? title : linkedPage.Title;
            icon = linkedPage.IconEmoji ?? string.Empty;
        }

        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "tm-include-page__page-link");
        builder.AddAttribute(2, "type", "button");
        builder.AddAttribute(3, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => NavigateToPageAsync(pageId)));
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "tm-include-page__page-icon");
        builder.AddAttribute(6, "aria-hidden", "true");
        builder.AddContent(7, string.IsNullOrWhiteSpace(icon) ? ">" : icon);
        builder.CloseElement();
        builder.OpenElement(8, "span");
        builder.AddContent(9, title);
        builder.CloseElement();
        builder.CloseElement();
    };

    private static int TableColumnCount(IPageBlock block, IReadOnlyList<IPageBlock> rows)
    {
        var declared = block.Content is ITableBlockContent table ? table.ColumnCount : 0;
        var actual = rows
            .Select(row => row.Content as ITableRowBlockContent)
            .Where(content => content is not null)
            .Select(content => content!.RichCells.Count)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(1, Math.Max(declared, actual));
    }

    private static bool IsHeaderCell(IPageBlock tableBlock, IPageBlock row, int columnIndex)
        => tableBlock.Content is ITableBlockContent table &&
           ((table.HasHeaderRow && row.Order == 0) || (table.HasHeaderColumn && columnIndex == 0));

    private static string CellHtml(ITableRowBlockContent? row, int index)
    {
        if (row is null)
        {
            return string.Empty;
        }

        return index < row.RichCells.Count
            ? row.RichCells[index].Html
            : string.Empty;
    }

    private sealed record PageChoice(Guid Id, string Title, int Depth);
}
