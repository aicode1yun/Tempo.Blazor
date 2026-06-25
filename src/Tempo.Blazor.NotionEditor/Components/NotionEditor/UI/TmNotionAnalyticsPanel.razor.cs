using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.UI;

/// <summary>Analytics summary panel for the active Notion space and page.</summary>
public partial class TmNotionAnalyticsPanel : ComponentBase
{
    private const int SparklineWidth = 220;
    private const int SparklineHeight = 64;

    [CascadingParameter] private NotionEditorContext Context { get; set; } = default!;

    /// <summary>Analytics provider used to query page views and top pages.</summary>
    [Parameter, EditorRequired] public INotionAnalyticsProvider AnalyticsProvider { get; set; } = default!;

    /// <summary>Space whose top pages should be displayed.</summary>
    [Parameter, EditorRequired] public string SpaceId { get; set; } = string.Empty;

    /// <summary>Currently open page id used for the main analytics summary.</summary>
    [Parameter] public Guid? CurrentPageId { get; set; }

    /// <summary>Maximum number of top pages to show.</summary>
    [Parameter] public int TopPageCount { get; set; } = 5;

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised when the close button is clicked.</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    private readonly Dictionary<Guid, string> _pageTitles = [];
    private IReadOnlyList<PageAnalyticsDto> _topPages = [];
    private PageAnalyticsDto? _currentAnalytics;
    private bool _isLoading;
    private string? _loadError;
    private Guid? _loadedPageId;
    private string? _loadedSpaceId;

    private PageAnalyticsDto CurrentSummary => _currentAnalytics ?? new PageAnalyticsDto
    {
        PageId = CurrentPageId ?? Guid.Empty
    };

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedPageId == CurrentPageId && string.Equals(_loadedSpaceId, SpaceId, StringComparison.OrdinalIgnoreCase))
            return;

        _loadedPageId = CurrentPageId;
        _loadedSpaceId = SpaceId;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;
        StateHasChanged();

        try
        {
            _currentAnalytics = CurrentPageId is { } pageId
                ? await AnalyticsProvider.GetPageAnalyticsAsync(pageId)
                : null;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            _topPages = string.IsNullOrWhiteSpace(SpaceId)
                ? []
                : await AnalyticsProvider.GetTopPagesAsync(
                    SpaceId,
                    new NotionAnalyticsRange
                    {
                        From = today.AddDays(-13),
                        To = today,
                        Take = Math.Clamp(TopPageCount, 1, 20)
                    });

            await ResolveTopPageTitlesAsync();
        }
        catch
        {
            _currentAnalytics = null;
            _topPages = [];
            _pageTitles.Clear();
            _loadError = Loc["Notion_Analytics_LoadError"];
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task ResolveTopPageTitlesAsync()
    {
        _pageTitles.Clear();

        foreach (var analytics in _topPages)
        {
            try
            {
                var page = await Context.DataProvider.GetPageAsync(analytics.PageId.ToString("D"));
                _pageTitles[analytics.PageId] = string.IsNullOrWhiteSpace(page.Title)
                    ? Loc["TmNotionEditor_Untitled"]
                    : page.Title;
            }
            catch
            {
                _pageTitles[analytics.PageId] = Loc["Notion_Analytics_UnknownPage"];
            }
        }
    }

    private string GetPageTitle(Guid pageId)
        => _pageTitles.TryGetValue(pageId, out var title) ? title : Loc["Notion_Analytics_UnknownPage"];

    private static double GetTopPagePercent(PageAnalyticsDto analytics, IReadOnlyList<PageAnalyticsDto> pages)
    {
        var max = pages.Count == 0 ? 0 : pages.Max(page => page.Views);
        return max <= 0 ? 0 : Math.Clamp(analytics.Views / (double)max * 100, 0, 100);
    }

    private static string BuildSparklinePoints(IReadOnlyList<PageAnalyticsPointDto> points)
    {
        if (points.Count == 0)
            return $"0,{SparklineHeight} {SparklineWidth},{SparklineHeight}";

        if (points.Count == 1)
        {
            var y = points[0].Views <= 0 ? SparklineHeight : SparklineHeight / 2;
            return $"0,{y.ToString("0.##", CultureInfo.InvariantCulture)} {SparklineWidth},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        var max = Math.Max(1, points.Max(point => point.Views));
        return string.Join(" ", points.Select((point, index) =>
        {
            var x = index / (double)(points.Count - 1) * SparklineWidth;
            var y = SparklineHeight - point.Views / (double)max * (SparklineHeight - 6) - 3;
            return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
        }));
    }

    private static string FormatDate(DateTime? date)
        => date is null ? string.Empty : date.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
