using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>Tests for the unified <see cref="ITmWorkItemProvider"/> source on the scheduler.</summary>
public class TmSchedulerWorkItemSourceTests : LocalizationTestBase
{
    [Fact]
    public void WorkItemSource_ScheduledItems_RenderAsCalendarEvents()
    {
        var today = DateTime.Today;
        var provider = new FakeWorkItemSource(
        [
            new TmWorkItem
            {
                Id = "w1",
                SourceKey = "demo",
                Title = "Shared Gantt Task",
                Start = today.AddDays(1),
                End = today.AddDays(3),
                StatusColor = "#22c55e"
            }
        ]);

        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, today)
            .Add(c => c.WorkItemSource, provider));

        cut.WaitForAssertion(() =>
        {
            provider.SearchCalls.Should().BeGreaterThan(0);
            cut.Markup.Should().Contain("Shared Gantt Task");
        });
    }

    [Fact]
    public void WorkItemSource_ItemsOutsideVisibleRange_AreNotShown()
    {
        var today = DateTime.Today;
        var provider = new FakeWorkItemSource(
        [
            new TmWorkItem { Id = "w1", SourceKey = "demo", Title = "Far Future Task",
                Start = today.AddYears(1), End = today.AddYears(1).AddDays(2) }
        ]);

        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, today)
            .Add(c => c.WorkItemSource, provider));

        cut.Markup.Should().NotContain("Far Future Task");
    }

    private sealed class FakeWorkItemSource : TmWorkItemProviderBase
    {
        private readonly List<TmWorkItem> _items;

        public FakeWorkItemSource(IEnumerable<TmWorkItem> items) => _items = items.ToList();

        public int SearchCalls { get; private set; }

        public override string SourceKey => "demo";
        public override string DisplayName => "Demo source";
        public override TmWorkItemCapabilities Capabilities => TmWorkItemCapabilities.All;

        public override Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            IEnumerable<TmWorkItem> q = _items;
            if (query.RangeStart is { } start) q = q.Where(i => i.End > start);
            if (query.RangeEnd is { } end) q = q.Where(i => i.Start < end);
            var matches = q.ToArray();
            return Task.FromResult(new PagedResult<TmWorkItem>
            {
                Items = matches,
                TotalCount = matches.Length,
                Page = 1,
                PageSize = Math.Max(1, matches.Length)
            });
        }
    }
}
