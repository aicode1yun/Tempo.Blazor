using Bunit;
using FluentAssertions;
using System.Reflection;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Components.NotionEditor.Blocks.Embed;
using Tempo.Blazor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionWorkItemBlockTests : LocalizationTestBase
{
    public TmNotionWorkItemBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["Tm_Search"] = "Search",
            ["Tm_NoResults"] = "No results",
            ["Tm_Select"] = "Select",
            ["Notion_WorkItem_Insert"] = "Insert work item",
            ["Notion_WorkItem_Search"] = "Search work items",
            ["Notion_WorkItem_Provider"] = "Provider",
            ["Notion_WorkItem_LoadError"] = "Work item could not be refreshed.",
            ["Notion_WorkItem_Status"] = "Status",
            ["Notion_WorkItem_Refresh"] = "Refresh",
            ["Notion_WorkItem_Open"] = "Open work item",
            ["Notion_WorkItem_Mode_Card"] = "Card",
            ["Notion_WorkItem_Mode_List"] = "List",
            ["Notion_WorkItem_Mode_Inline"] = "Inline"
        });
    }

    [Fact]
    public void CardMode_RendersStatusColorTypeIconTitleAndLink()
    {
        var item = SampleItem();
        var cut = RenderWorkItem(new WorkItemBlockContent
        {
            SourceKey = "demo",
            ExternalId = "DEMO-101",
            DisplayMode = WorkItemDisplayMode.Card,
            CachedSnapshot = item
        }, [new StaticWorkItemProvider("demo", "Demo tracker", [item])]);

        cut.WaitForAssertion(() =>
        {
            cut.Find(".tm-work-item.tm-work-item--card").Should().NotBeNull();
            cut.Find(".tm-work-item__status").GetAttribute("style").Should().Contain("#22c55e");
            cut.Find(".tm-work-item__type-icon").GetAttribute("src").Should().Be(item.TypeIconUrl);
            cut.Find(".tm-work-item__title").TextContent.Should().Contain(item.Title);
            cut.Find(".tm-work-item__link").GetAttribute("href").Should().Be(item.Url);
        });
    }

    [Fact]
    public void InlineMode_RendersCompactChip()
    {
        var item = SampleItem();
        var cut = RenderWorkItem(new WorkItemBlockContent
        {
            SourceKey = "demo",
            ExternalId = "DEMO-101",
            DisplayMode = WorkItemDisplayMode.Inline,
            CachedSnapshot = item
        }, [new StaticWorkItemProvider("demo", "Demo tracker", [item])]);

        cut.WaitForAssertion(() =>
        {
            cut.Find(".tm-work-item.tm-work-item--inline").Should().NotBeNull();
            cut.Find(".tm-work-item__chip-id").TextContent.Should().Be("DEMO-101");
            cut.Find(".tm-work-item__chip-title").TextContent.Should().Contain(item.Title);
        });
    }

    [Fact]
    public async Task PickerSearch_SendsFreeTextWithoutIds()
    {
        var provider = new CapturingWorkItemProvider("demo", "Demo tracker", [SampleItem()]);
        var cut = RenderWorkItem(new WorkItemBlockContent(), [provider]);

        SetPrivateField(cut.Instance, "_searchText", "release");
        SetPrivateField(cut.Instance, "_selectedSourceKey", "demo");

        var search = typeof(TmNotionWorkItemBlock).GetMethod("SearchAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        search.Should().NotBeNull();
        await cut.InvokeAsync(async () => await (Task)search!.Invoke(cut.Instance, null)!);

        cut.WaitForAssertion(() =>
        {
            provider.LastQuery.Should().NotBeNull();
            provider.LastQuery!.FreeText.Should().Be("release");
            provider.LastQuery.Ids.Should().BeEmpty();
        });
    }

    private static void SetPrivateField<TValue>(TmNotionWorkItemBlock instance, string fieldName, TValue value)
    {
        var field = typeof(TmNotionWorkItemBlock).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(instance, value);
    }

    private IRenderedComponent<TmNotionWorkItemBlock> RenderWorkItem(
        WorkItemBlockContent content,
        IReadOnlyList<ITmWorkItemProvider> providers)
        => Render<TmNotionWorkItemBlock>(parameters => parameters
            .Add(component => component.Content, content)
            .Add(component => component.WorkItemProviders, new TmWorkItemProviderRegistry(providers))
            .Add(component => component.ReadOnly, false));

    private static TmWorkItem SampleItem() => new()
    {
        Id = "DEMO-101",
        SourceKey = "demo",
        ExternalId = "DEMO-101",
        Url = "https://tracker.example/work/DEMO-101",
        Title = "Prepare release checklist",
        StatusLabel = "Done",
        StatusColor = "#22c55e",
        TypeLabel = "Story",
        TypeIconUrl = "https://tracker.example/icons/story.svg",
        Assignees = [new TmWorkItemAssignee { Id = "ada", Name = "Ada Lovelace" }],
        PriorityLabel = "High",
        UpdatedAt = new DateTime(2026, 6, 1, 10, 15, 0, DateTimeKind.Utc)
    };

    private sealed class StaticWorkItemProvider : TmWorkItemProviderBase
    {
        private readonly IReadOnlyList<TmWorkItem> _items;

        public StaticWorkItemProvider(string sourceKey, string displayName, IReadOnlyList<TmWorkItem> items)
        {
            SourceKey = sourceKey;
            DisplayName = displayName;
            _items = items;
        }

        public override string SourceKey { get; }
        public override string DisplayName { get; }

        public override Task<TmWorkItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item =>
                string.Equals(item.ExternalId ?? item.Id, id, StringComparison.OrdinalIgnoreCase)));

        public override Task<Tempo.Blazor.Models.PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new Tempo.Blazor.Models.PagedResult<TmWorkItem>
            {
                Items = _items,
                TotalCount = _items.Count,
                Page = 1,
                PageSize = _items.Count
            });
    }

    private sealed class CapturingWorkItemProvider : TmWorkItemProviderBase
    {
        private readonly IReadOnlyList<TmWorkItem> _items;

        public CapturingWorkItemProvider(string sourceKey, string displayName, IReadOnlyList<TmWorkItem> items)
        {
            SourceKey = sourceKey;
            DisplayName = displayName;
            _items = items;
        }

        public override string SourceKey { get; }
        public override string DisplayName { get; }
        public TmWorkItemQuery? LastQuery { get; private set; }

        public override Task<TmWorkItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item =>
                string.Equals(item.ExternalId ?? item.Id, id, StringComparison.OrdinalIgnoreCase)));

        public override Task<Tempo.Blazor.Models.PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            var term = query.FreeText?.Trim() ?? string.Empty;
            var items = _items
                .Where(item => item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               (item.ExternalId ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return Task.FromResult(new Tempo.Blazor.Models.PagedResult<TmWorkItem>
            {
                Items = items,
                TotalCount = items.Length,
                Page = 1,
                PageSize = Math.Max(1, query.Take)
            });
        }
    }
}
