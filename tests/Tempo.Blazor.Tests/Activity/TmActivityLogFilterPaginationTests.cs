using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Activity;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Activity;

/// <summary>
/// K10 – filtering + pagination pass-through from TmActivityLog to its timeline tab
/// (TmActivityTimeline). Additive: existing consumers pass no new params and behave unchanged.
/// </summary>
public class TmActivityLogFilterPaginationTests : LocalizationTestBase
{
    private const string LoadMore = "[data-testid=\"timeline-load-more\"]";

    private static IReadOnlyList<ITimelineEntry> Many(int n) =>
        Enumerable.Range(1, n)
            .Select(i => (ITimelineEntry)new AlEntry(
                $"e{i}",
                i % 2 == 0 ? "status_change" : "comment",
                $"User{i}",
                null,
                DateTimeOffset.Now.AddMinutes(-i),
                null,
                $"Entry number {i}"))
            .ToList();

    private static IReadOnlyList<ITimelineEntry> Mixed() =>
    [
        new AlEntry("1", "comment",       "Alice", null, DateTimeOffset.Now.AddHours(-3), null, "Fixed the bug"),
        new AlEntry("2", "status_change", "Bob",   null, DateTimeOffset.Now.AddHours(-2), null, "Changed to Active"),
        new AlEntry("3", "comment",       "Carol", null, DateTimeOffset.Now.AddHours(-1), null, "Another note"),
    ];

    [Fact]
    public void ActivityLog_NoNewParams_RendersAllTimelineItems()
    {
        var cut = RenderComponent<TmActivityLog>(p => p
            .Add(c => c.TimelineEntries, Many(4)));

        cut.FindAll(".tm-timeline-item").Count.Should().Be(4);
        cut.FindAll(LoadMore).Should().BeEmpty();
    }

    [Fact]
    public void ActivityLog_TimelineFilter_Narrows_Timeline()
    {
        var cut = RenderComponent<TmActivityLog>(p => p
            .Add(c => c.TimelineEntries, Mixed())
            .Add(c => c.TimelineFilter, "bug"));

        cut.FindAll(".tm-timeline-item").Count.Should().Be(1);
    }

    [Fact]
    public void ActivityLog_TimelinePageSize_Limits_And_LoadMore_Grows()
    {
        var cut = RenderComponent<TmActivityLog>(p => p
            .Add(c => c.TimelineEntries, Many(5))
            .Add(c => c.TimelinePageSize, 2));

        cut.FindAll(".tm-timeline-item").Count.Should().Be(2);

        cut.Find(LoadMore).Click();
        cut.FindAll(".tm-timeline-item").Count.Should().Be(4);
    }

    private sealed record AlEntry(
        string Id,
        string EntryType,
        string AuthorName,
        string? AuthorAvatarUrl,
        DateTimeOffset CreatedAt,
        string? HtmlContent,
        string? PlainContent,
        bool IsInternal = false,
        IReadOnlyDictionary<string, string>? Metadata = null) : ITimelineEntry;
}
