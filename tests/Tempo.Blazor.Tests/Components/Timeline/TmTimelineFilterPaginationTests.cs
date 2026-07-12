using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Timeline;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Timeline;

file record TlEntry(
    string Id,
    string EntryType,
    string AuthorName,
    string? AuthorAvatarUrl,
    DateTimeOffset CreatedAt,
    string? HtmlContent,
    string? PlainContent,
    bool IsInternal = false,
    IReadOnlyDictionary<string, string>? Metadata = null) : ITimelineEntry;

/// <summary>
/// K10 – TDD tests for additive filtering + incremental pagination on TmTimeline,
/// covering both the client-side (Entries) path and the optional async data provider path.
/// </summary>
public class TmTimelineFilterPaginationTests : LocalizationTestBase
{
    private const string LoadMore = "[data-testid=\"timeline-load-more\"]";
    private const string SearchBox = "[data-testid=\"timeline-search\"]";
    private const string TypeFilter = "[data-testid=\"timeline-type-filter\"]";

    private static List<ITimelineEntry> Many(int n) =>
        Enumerable.Range(1, n)
            .Select(i => (ITimelineEntry)new TlEntry(
                $"e{i}",
                i % 2 == 0 ? "status_change" : "comment",
                $"User{i}",
                null,
                DateTimeOffset.Now.AddMinutes(-i),
                null,
                $"Entry number {i}"))
            .ToList();

    private static List<ITimelineEntry> Mixed() =>
    [
        new TlEntry("1", "comment",       "Alice", null, DateTimeOffset.Now.AddHours(-3), null, "Fixed the bug"),
        new TlEntry("2", "status_change", "Bob",   null, DateTimeOffset.Now.AddHours(-2), null, "Changed to Active"),
        new TlEntry("3", "comment",       "Carol", null, DateTimeOffset.Now.AddHours(-1), null, "Another note"),
    ];

    // ── Backward compatibility ─────────────────────────────────────────────

    [Fact]
    public void No_PageSize_Renders_All_And_No_LoadMore()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Many(5)));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(5);
        cut.FindAll(LoadMore).Should().BeEmpty();
    }

    // ── Filtering (host-controlled params) ─────────────────────────────────

    [Fact]
    public void Filter_Param_Narrows_Entries_By_Text()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Mixed())
            .Add(c => c.Filter, "bug"));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(1);
        cut.Find(".tm-timeline-content").TextContent.Should().Contain("Fixed the bug");
    }

    [Fact]
    public void EntryTypeFilter_Param_Narrows_Entries_By_Type()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Many(6)) // 3 comment (odd), 3 status_change (even)
            .Add(c => c.EntryTypeFilter, "comment"));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(3);
    }

    // ── Pagination (client-side) ───────────────────────────────────────────

    [Fact]
    public void PageSize_Limits_Rendered_Count()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Many(5))
            .Add(c => c.PageSize, 2));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(2);
        cut.FindAll(LoadMore).Should().NotBeEmpty();
    }

    [Fact]
    public void LoadMore_Grows_Rendered_Count_Then_Disappears()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Many(5))
            .Add(c => c.PageSize, 2));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(2);

        cut.Find(LoadMore).Click();
        cut.FindAll(".tm-timeline-entry").Count.Should().Be(4);

        cut.Find(LoadMore).Click();
        cut.FindAll(".tm-timeline-entry").Count.Should().Be(5);

        cut.FindAll(LoadMore).Should().BeEmpty();
    }

    // ── Filter bar (interactive) ───────────────────────────────────────────

    [Fact]
    public void FilterBar_Search_Input_Narrows_Entries()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Mixed())
            .Add(c => c.ShowFilterBar, true));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(3);

        cut.Find(SearchBox).Input("bug");

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(1);
    }

    [Fact]
    public void FilterBar_Renders_Type_Options_From_Entries()
    {
        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.Entries, Many(4))
            .Add(c => c.ShowFilterBar, true));

        // "All types" + comment + status_change = 3 options
        cut.Find(TypeFilter).QuerySelectorAll("option").Length.Should().Be(3);
    }

    // ── Provider path ──────────────────────────────────────────────────────

    [Fact]
    public void Provider_Path_Renders_Provider_Items_And_Is_Called()
    {
        var provider = new FakeProvider(Many(10));

        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, 3));

        provider.Calls.Should().BeGreaterThan(0);
        cut.FindAll(".tm-timeline-entry").Count.Should().Be(3);
        cut.FindAll(LoadMore).Should().NotBeEmpty();
    }

    [Fact]
    public void Provider_LoadMore_Requests_Larger_Window()
    {
        var provider = new FakeProvider(Many(10));

        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.PageSize, 3));

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(3);

        cut.Find(LoadMore).Click();

        cut.FindAll(".tm-timeline-entry").Count.Should().Be(6);
        provider.LastTake.Should().Be(6);
    }

    [Fact]
    public void Provider_Path_Applies_Filter_Through_Query()
    {
        var provider = new FakeProvider(Mixed());

        var cut = RenderComponent<TmTimeline>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.Filter, "bug"));

        provider.LastSearch.Should().Be("bug");
        cut.FindAll(".tm-timeline-entry").Count.Should().Be(1);
    }

    private sealed class FakeProvider : ITimelineDataProvider
    {
        private readonly IReadOnlyList<ITimelineEntry> _all;
        public int Calls;
        public int LastTake;
        public string? LastSearch;

        public FakeProvider(IReadOnlyList<ITimelineEntry> all) => _all = all;

        public Task<TimelinePage> GetEntriesAsync(TimelineQuery query, CancellationToken ct = default)
        {
            Calls++;
            LastTake = query.Take;
            LastSearch = query.SearchText;

            IEnumerable<ITimelineEntry> q = _all.Where(e => query.IncludeInternal || !e.IsInternal);
            if (!string.IsNullOrWhiteSpace(query.EntryType))
                q = q.Where(e => string.Equals(e.EntryType, query.EntryType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.SearchText))
                q = q.Where(e => (e.PlainContent ?? string.Empty)
                    .Contains(query.SearchText!, StringComparison.OrdinalIgnoreCase));

            var list = q.ToList();
            var items = list.Skip(query.Skip).Take(query.Take).ToList();
            return Task.FromResult(new TimelinePage { Items = items, TotalCount = list.Count });
        }
    }
}
