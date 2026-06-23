using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Page;

public partial class TmNotionPageInfoPanel : ComponentBase
{
    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    /// <summary>The page whose metadata should be displayed.</summary>
    [Parameter, EditorRequired]
    public INotionPage Page { get; set; } = default!;

    /// <summary>Currently loaded page blocks. When absent, the component loads them from the block provider.</summary>
    [Parameter]
    public IReadOnlyList<IPageBlock>? Blocks { get; set; }

    /// <summary>Shows the panel when true.</summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>Raised when the visibility changes.</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    private PageStats _stats = PageStats.Empty;
    private PageAnalyticsDto? _analytics;
    private IReadOnlyDictionary<string, string> _userDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private Guid _loadedPageId;
    private IReadOnlyList<IPageBlock>? _loadedBlocksReference;
    private bool _wasVisible;

    protected override async Task OnParametersSetAsync()
    {
        if (!Visible)
        {
            _wasVisible = false;
            return;
        }

        if (_wasVisible && _loadedPageId == Page.Id && ReferenceEquals(_loadedBlocksReference, Blocks))
            return;

        _wasVisible = true;
        _loadedPageId = Page.Id;
        _loadedBlocksReference = Blocks;
        await LoadPanelDataAsync();
    }

    private async Task LoadPanelDataAsync()
    {
        var blocks = Blocks ?? (await Context.BlockProvider.GetBlocksAsync(Page.Id.ToString("D"))).ToArray();
        var fragments = new List<string?>();
        foreach (var block in blocks.OrderBy(block => block.Order))
        {
            await CollectTextFragmentsAsync(block, fragments);
        }

        _stats = PageStats.Calculate(fragments);
        _analytics = null;
        _userDisplayNames = await ResolveUserDisplayNamesAsync(Page.CreatedByUserId, Page.LastEditedByUserId);

        if (Context.AnalyticsProvider is null)
            return;

        try
        {
            _analytics = await Context.AnalyticsProvider.GetPageAnalyticsAsync(Page.Id);
        }
        catch
        {
            _analytics = null;
        }
    }

    private async Task CollectTextFragmentsAsync(IPageBlock block, List<string?> fragments)
    {
        fragments.AddRange(ExtractTextFragments(block.Content));

        try
        {
            var children = await Context.BlockProvider.GetChildBlocksAsync(block.Id.ToString("D"));
            foreach (var child in children.OrderBy(item => item.Order))
                await CollectTextFragmentsAsync(child, fragments);
        }
        catch
        {
        }
    }

    private static IEnumerable<string?> ExtractTextFragments(IBlockContent? content)
    {
        return content switch
        {
            ITextBlockContent text => [text.Html],
            ITableRowBlockContent row when row.RichCells.Count > 0 => row.RichCells.Select(cell => cell.Html),
            ITableRowBlockContent row => row.Cells,
            ICodeBlockContent code => [code.Code, code.Caption],
            IChildPageBlockContent child => [child.Title],
            ILinkedPageBlockContent linked => [linked.Title],
            IBookmarkBlockContent bookmark => [bookmark.Title],
            IInlineDatabaseBlockContent database => [database.Title],
            IExcerptBlockContent excerpt => [excerpt.Html],
            IPagePropertiesBlockContent properties => properties.Rows.SelectMany(row => new[] { row.Key, row.ValueHtml }),
            IPagePropertiesReportBlockContent report => report.Labels.Concat(report.Columns),
            IWorkItemBlockContent workItem => [workItem.CachedSnapshot?.Title],
            IImageBlockContent image => [image.Caption, image.AltText],
            IFileBlockContent file => [file.FileName, file.Caption],
            IMediaBlockContent media => [media.Caption],
            _ => []
        };
    }

    private string FormatUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Loc["Notion_PageInfo_UnknownUser"];

        return _userDisplayNames.TryGetValue(userId.Trim(), out var displayName)
            ? displayName
            : Loc["Notion_PageInfo_UnknownUser"];
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveUserDisplayNamesAsync(params string?[] userIds)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Context.MentionProvider is null)
            return result;

        foreach (var userId in userIds.Select(id => id?.Trim()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var match = await Context.MentionProvider.GetByIdAsync(userId!);
                if (match is null)
                {
                    var users = await Context.MentionProvider.SearchAsync(new TmPeopleQuery { SearchText = userId, Take = 8 });
                    match = users.FirstOrDefault(user =>
                        string.Equals(user.Id, userId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(user.UserName, userId, StringComparison.OrdinalIgnoreCase));
                }

                if (match is not null && !string.IsNullOrWhiteSpace(match.DisplayName))
                {
                    result[userId!] = match.DisplayName;
                }
            }
            catch
            {
            }
        }

        return result;
    }

    private static string FormatDate(DateTime date)
        => date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private async Task CloseAsync()
    {
        if (!Visible)
            return;

        Visible = false;
        await VisibleChanged.InvokeAsync(false);
    }
}
