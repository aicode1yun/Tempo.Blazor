using Bunit.Rendering;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionAnalyticsPanelTests : LocalizationTestBase
{
    private static readonly Guid PageA = Guid.Parse("cf310000-0000-0000-0000-000000000001");
    private static readonly Guid PageB = Guid.Parse("cf310000-0000-0000-0000-000000000002");

    public TmNotionAnalyticsPanelTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Close"] = "Close",
            ["Tm_Loading"] = "Loading",
            ["Tm_Retry"] = "Retry",
            ["TmNotionEditor_Untitled"] = "Untitled",
            ["Notion_Analytics_Title"] = "Analytics",
            ["Notion_Analytics_Subtitle"] = "Page traffic and top content",
            ["Notion_Analytics_Views"] = "Views",
            ["Notion_Analytics_Unique"] = "Unique visitors",
            ["Notion_Analytics_TopPages"] = "Top pages",
            ["Notion_Analytics_LastViewed"] = "Last viewed",
            ["Notion_Analytics_Empty"] = "No analytics data yet.",
            ["Notion_Analytics_ViewsByDay"] = "Views by day",
            ["Notion_Analytics_NeverViewed"] = "Never viewed",
            ["Notion_Analytics_LoadError"] = "Analytics could not be loaded.",
            ["Notion_Analytics_UnknownPage"] = "Unknown page"
        });
    }

    [Fact]
    public void PanelRendersViewsDailyChartAndTopPages()
    {
        var provider = new FakeAnalyticsProvider([
            Analytics(PageA, 42, 7, [12, 30]),
            Analytics(PageB, 18, 4, [5, 13])
        ]);

        var cut = RenderPanel(provider);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='notion-analytics-views']").TextContent.Should().Contain("42");
            cut.Find("[data-testid='notion-analytics-unique']").TextContent.Should().Contain("7");
            cut.Find(".tm-notion-analytics__sparkline-line").Should().NotBeNull();
            cut.Find("[data-testid='notion-analytics-top-pages']").TextContent.Should().Contain("Analytics Overview");
            cut.Find("[data-testid='notion-analytics-top-pages']").TextContent.Should().Contain("Adoption Report");
        });
    }

    [Fact]
    public void PanelRendersEmptyAnalyticsState()
    {
        var cut = RenderPanel(new FakeAnalyticsProvider([]));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='notion-analytics-views']").TextContent.Should().Contain("0");
            cut.FindAll("[data-testid='notion-analytics-empty']").Should().ContainSingle();
            cut.Markup.Should().Contain("No analytics data yet.");
        });
    }

    private IRenderedComponent<ContainerFragment> RenderPanel(INotionAnalyticsProvider provider)
    {
        var context = new NotionEditorContext
        {
            DataProvider = new FakeDataProvider(),
            BlockService = new FakeBlockService()
        };

        return Render(builder =>
        {
            builder.OpenComponent<CascadingValue<NotionEditorContext>>(0);
            builder.AddAttribute(1, "Value", context);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(child =>
            {
                child.OpenComponent<TmNotionAnalyticsPanel>(3);
                child.AddAttribute(4, nameof(TmNotionAnalyticsPanel.AnalyticsProvider), provider);
                child.AddAttribute(5, nameof(TmNotionAnalyticsPanel.SpaceId), "team");
                child.AddAttribute(6, nameof(TmNotionAnalyticsPanel.CurrentPageId), PageA);
                child.CloseComponent();
            }));
            builder.CloseComponent();
        });
    }

    private static PageAnalyticsDto Analytics(Guid pageId, int views, int uniqueVisitors, IReadOnlyList<int> byDay)
        => new()
        {
            PageId = pageId,
            Views = views,
            UniqueVisitors = uniqueVisitors,
            LastViewedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            ViewsByDay = byDay.Select((count, index) => new PageAnalyticsPointDto
            {
                Date = new DateOnly(2026, 1, 14).AddDays(index),
                Views = count
            }).ToArray()
        };

    private sealed class FakeAnalyticsProvider(IReadOnlyList<PageAnalyticsDto> analytics) : INotionAnalyticsProvider
    {
        public Task RecordViewAsync(Guid pageId, string? userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PageAnalyticsDto?> GetPageAnalyticsAsync(Guid pageId, CancellationToken cancellationToken = default)
            => Task.FromResult(analytics.FirstOrDefault(item => item.PageId == pageId));

        public Task<IReadOnlyList<PageAnalyticsDto>> GetTopPagesAsync(string spaceId, NotionAnalyticsRange range, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PageAnalyticsDto>>(analytics
                .OrderByDescending(item => item.Views)
                .Take(range.Take)
                .ToArray());
    }

    private sealed class FakeDataProvider : INotionDataProvider
    {
        public Task<INotionPage> GetPageAsync(string pageId)
        {
            var id = Guid.Parse(pageId);
            return Task.FromResult<INotionPage>(new NotionPage
            {
                Id = id,
                SpaceId = "team",
                Title = id == PageA ? "Analytics Overview" : "Adoption Report"
            });
        }

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId) => throw new NotSupportedException();
        public Task<IEnumerable<INotionPage>> GetFavoritesAsync() => throw new NotSupportedException();
        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count) => throw new NotSupportedException();
        public Task<IEnumerable<INotionPage>> GetTrashAsync() => throw new NotSupportedException();
        public Task<INotionPage> CreatePageAsync(string? parentId, string title) => throw new NotSupportedException();
        public Task UpdatePageAsync(INotionPage page) => throw new NotSupportedException();
        public Task DeletePageAsync(string pageId) => throw new NotSupportedException();
        public Task RestorePageAsync(string pageId) => throw new NotSupportedException();
        public Task PermanentlyDeletePageAsync(string pageId) => throw new NotSupportedException();
        public Task ToggleFavoriteAsync(string pageId, bool isFavorite) => throw new NotSupportedException();
        public Task MovePageAsync(string pageId, string? newParentId) => throw new NotSupportedException();
        public Task<INotionPage> DuplicatePageAsync(string pageId) => throw new NotSupportedException();
        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeBlockService : INotionEditorBlockService
    {
        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId) => Task.FromResult<IEnumerable<IPageBlock>>([]);
        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId) => Task.FromResult<IEnumerable<IPageBlock>>([]);
        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId) => throw new NotSupportedException();
        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId) => throw new NotSupportedException();
        public Task UpdateBlockAsync(IPageBlock block) => throw new NotSupportedException();
        public Task DeleteBlockAsync(string blockId) => throw new NotSupportedException();
        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds) => throw new NotSupportedException();
        public Task MoveBlockAsync(MoveNotionBlockRequest request) => throw new NotSupportedException();
        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId) => throw new NotSupportedException();
        public Task<IPageBlock> DuplicateBlockAsync(string blockId) => throw new NotSupportedException();
        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType) => throw new NotSupportedException();
        public Task<string> GetBlockLinkAsync(string blockId) => throw new NotSupportedException();
    }
}
