using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Special;

public partial class TmNotionChildrenDisplayBlock : ComponentBase, IDisposable
{
    private static readonly int[] DepthOptions = [1, 2, 3, 4, 5];
    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>Owning page block used to resolve the current page when no root page is configured.</summary>
    [Parameter] public IPageBlock Block { get; set; } = default!;

    /// <summary>Saved Children Display block configuration.</summary>
    [Parameter] public IChildrenDisplayBlockContent? Content { get; set; }

    /// <summary>Whether editing controls are hidden.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Raised after the block configuration changes.</summary>
    [Parameter] public EventCallback<ChildrenDisplayBlockContent> OnContentChanged { get; set; }

    /// <summary>Raised when the block receives focus.</summary>
    [Parameter] public EventCallback OnFocused { get; set; }

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    private IReadOnlyList<ChildrenDisplayNode> _nodes = [];
    private IReadOnlyList<PageChoice> _pageChoices = [];
    private INotionPage? _rootPage;
    private Guid? _rootPageId;
    private int _depth;
    private bool _showIcons = true;
    private bool _loading;
    private bool _loadedChoices;
    private bool _sourceDeleted;
    private int _loadVersion;
    private string? _loadedSignature;

    private string RootSelectValue => _rootPageId?.ToString("D") ?? string.Empty;

    private string SummaryText
    {
        get
        {
            var root = _rootPageId.HasValue && _rootPage is not null
                ? PageTitle(_rootPage)
                : Loc["Notion_Children_CurrentPage"];

            var depth = _depth == 0
                ? Loc["Notion_Children_DepthAll"]
                : Loc["Notion_Children_DepthLevel", _depth];

            return Loc["Notion_Children_Summary", root, depth];
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        _rootPageId = Content?.RootPageId;
        _depth = NormalizeDepth(Content?.Depth ?? 0);
        _showIcons = Content?.ShowIcons ?? true;

        if (!_loadedChoices && !ReadOnly)
        {
            await LoadPageChoicesAsync();
        }

        var signature = BuildSignature(Block.PageId, _rootPageId, _depth);
        if (!string.Equals(signature, _loadedSignature, StringComparison.Ordinal))
        {
            _loadedSignature = signature;
            await LoadTreeAsync();
        }
    }

    private async Task LoadPageChoicesAsync()
    {
        var choices = new List<PageChoice>();
        await AddPageChoicesAsync(null, 0, choices, new HashSet<Guid>());
        _pageChoices = choices;
        _loadedChoices = true;
    }

    private async Task AddPageChoicesAsync(Guid? parentId, int level, List<PageChoice> choices, HashSet<Guid> visited)
    {
        var pages = await Context.DataProvider.GetChildPagesAsync(parentId?.ToString("D"));
        foreach (var page in pages.Where(page => !page.IsDeleted).OrderBy(PageTitle, StringComparer.OrdinalIgnoreCase))
        {
            if (!visited.Add(page.Id))
            {
                continue;
            }

            choices.Add(new PageChoice(page.Id, page.Id.ToString("D"), $"{new string(' ', level * 2)}{PageTitle(page)}"));
            await AddPageChoicesAsync(page.Id, level + 1, choices, new HashSet<Guid>(visited));
        }
    }

    private async Task LoadTreeAsync()
    {
        var version = ++_loadVersion;
        _loading = true;
        try
        {
            _rootPage = null;
            _sourceDeleted = false;
            if (_rootPageId.HasValue)
            {
                _rootPage = await LoadPageOrNullAsync(_rootPageId.Value);
                if (_rootPage is null)
                {
                    if (version == _loadVersion)
                    {
                        _nodes = [];
                        _sourceDeleted = true;
                    }
                    return;
                }
            }

            var rootId = _rootPageId ?? Block.PageId;
            var nodes = await LoadChildrenAsync(rootId, 1, new HashSet<Guid> { rootId });
            if (version == _loadVersion)
            {
                _nodes = nodes;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _loading = false;
            }
        }
    }

    private async Task<INotionPage?> LoadPageOrNullAsync(Guid pageId)
    {
        try
        {
            var page = await Context.DataProvider.GetPageAsync(pageId.ToString("D"));
            return page.IsDeleted ? null : page;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<ChildrenDisplayNode>> LoadChildrenAsync(Guid parentId, int level, HashSet<Guid> ancestors)
    {
        var pages = await Context.DataProvider.GetChildPagesAsync(parentId.ToString("D"));
        var nodes = new List<ChildrenDisplayNode>();

        foreach (var page in pages.Where(page => !page.IsDeleted).OrderBy(PageTitle, StringComparer.OrdinalIgnoreCase))
        {
            if (ancestors.Contains(page.Id))
            {
                continue;
            }

            var descendants = Array.Empty<ChildrenDisplayNode>();
            if (_depth == 0 || level < _depth)
            {
                descendants = (await LoadChildrenAsync(page.Id, level + 1, new HashSet<Guid>(ancestors) { page.Id })).ToArray();
            }

            nodes.Add(new ChildrenDisplayNode(page, descendants));
        }

        return nodes;
    }

    private Task OnFocusedAsync(MouseEventArgs _)
        => OnFocused.InvokeAsync();

    private async Task HandleRootChangedAsync(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        _rootPageId = Guid.TryParse(value, out var id) ? id : null;
        await SaveContentAsync();
    }

    private async Task HandleDepthChangedAsync(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var depth))
        {
            _depth = NormalizeDepth(depth);
            await SaveContentAsync();
        }
    }

    private async Task HandleShowIconsChangedAsync(ChangeEventArgs args)
    {
        _showIcons = args.Value is bool value && value;
        await SaveContentAsync();
    }

    private async Task SaveContentAsync()
    {
        var content = new ChildrenDisplayBlockContent
        {
            RootPageId = _rootPageId,
            Depth = _depth,
            ShowIcons = _showIcons
        };

        _loadedSignature = BuildSignature(Block.PageId, _rootPageId, _depth);
        await OnContentChanged.InvokeAsync(content);
        await LoadTreeAsync();
    }

    private async Task NavigateToPageAsync(INotionPage page)
    {
        await OnFocused.InvokeAsync();
        if (Context.NavigateTo is not null)
        {
            await Context.NavigateTo(page.Id.ToString("D"));
        }
    }

    private RenderFragment RenderNode(ChildrenDisplayNode node, int level) => builder =>
    {
        var page = node.Page;
        var seq = 0;

        builder.OpenElement(seq++, "li");
        builder.AddAttribute(seq++, "class", "tm-children__item");
        builder.AddAttribute(seq++, "role", "none");

        builder.OpenElement(seq++, "button");
        builder.AddAttribute(seq++, "class", _showIcons ? "tm-children__page" : "tm-children__page tm-children__page--no-icon");
        builder.AddAttribute(seq++, "type", "button");
        builder.AddAttribute(seq++, "role", "treeitem");
        builder.AddAttribute(seq++, "aria-level", level);
        if (node.Children.Count > 0)
        {
            builder.AddAttribute(seq++, "aria-expanded", "true");
        }
        builder.AddAttribute(seq++, "title", PageTitle(page));
        builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => NavigateToPageAsync(page)));

        if (_showIcons)
        {
            RenderIcon(builder, ref seq, page);
        }

        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-children__page-title");
        builder.AddContent(seq++, PageTitle(page));
        builder.CloseElement();

        builder.OpenElement(seq++, "svg");
        builder.AddAttribute(seq++, "class", "tm-children__arrow");
        builder.AddAttribute(seq++, "width", "14");
        builder.AddAttribute(seq++, "height", "14");
        builder.AddAttribute(seq++, "viewBox", "0 0 14 14");
        builder.AddAttribute(seq++, "fill", "none");
        builder.AddAttribute(seq++, "aria-hidden", "true");
        builder.OpenElement(seq++, "path");
        builder.AddAttribute(seq++, "d", "M5 3.5 8.5 7 5 10.5");
        builder.AddAttribute(seq++, "stroke", "currentColor");
        builder.AddAttribute(seq++, "stroke-width", "1.5");
        builder.AddAttribute(seq++, "stroke-linecap", "round");
        builder.AddAttribute(seq++, "stroke-linejoin", "round");
        builder.CloseElement();
        builder.CloseElement();

        builder.CloseElement();

        if (node.Children.Count > 0)
        {
            builder.OpenElement(seq++, "ul");
            builder.AddAttribute(seq++, "class", "tm-children__group");
            builder.AddAttribute(seq++, "role", "group");
            foreach (var child in node.Children)
            {
                builder.AddContent(seq++, RenderNode(child, level + 1));
            }
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private void RenderIcon(RenderTreeBuilder builder, ref int seq, INotionPage page)
    {
        if (!string.IsNullOrWhiteSpace(page.IconImageUrl))
        {
            builder.OpenElement(seq++, "img");
            builder.AddAttribute(seq++, "class", "tm-children__page-icon tm-children__page-icon--image");
            builder.AddAttribute(seq++, "src", page.IconImageUrl);
            builder.AddAttribute(seq++, "alt", string.Empty);
            builder.CloseElement();
            return;
        }

        builder.OpenElement(seq++, "span");
        builder.AddAttribute(seq++, "class", "tm-children__page-icon");
        builder.AddAttribute(seq++, "aria-hidden", "true");
        builder.AddContent(seq++, page.IconEmoji ?? string.Empty);
        builder.CloseElement();
    }

    private static string PageTitle(INotionPage page)
        => string.IsNullOrWhiteSpace(page.Title) ? page.Id.ToString("D") : page.Title;

    private static int NormalizeDepth(int depth)
        => depth is >= 0 and <= 10 ? depth : 0;

    private static string BuildSignature(Guid currentPageId, Guid? rootPageId, int depth)
        => $"{currentPageId:D}|{rootPageId?.ToString("D") ?? string.Empty}|{depth}";

    public void Dispose()
        => _disposeCts.Dispose();

    private sealed record ChildrenDisplayNode(INotionPage Page, IReadOnlyList<ChildrenDisplayNode> Children);

    private sealed record PageChoice(Guid Id, string Value, string Label);
}
