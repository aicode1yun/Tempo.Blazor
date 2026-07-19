using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>Timezone + DST behavior for the DateTimeOffset scheduler migration.</summary>
public class TmSchedulerTimezoneTests : LocalizationTestBase
{
    private static TimeZoneInfo Prague()
    {
        foreach (var id in new[] { "Europe/Prague", "Central Europe Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
            {
                return tz;
            }
        }

        throw new InvalidOperationException("Prague timezone not available on this machine.");
    }

    [Fact]
    public void ResolveTimeZone_NullId_ReturnsLocal()
    {
        var cut = Render<TmScheduler>();
        cut.Instance.ResolveTimeZone().Should().Be(TimeZoneInfo.Local);
    }

    [Fact]
    public void ResolveTimeZone_KnownId_ReturnsThatZone()
    {
        var cut = Render<TmScheduler>(p => p.Add(c => c.TimeZoneId, "Europe/Prague"));
        cut.Instance.ResolveTimeZone().BaseUtcOffset.Should().Be(TimeSpan.FromHours(1)); // CET base
    }

    [Fact]
    public void ResolveTimeZone_InvalidId_FallsBackToLocal()
    {
        var cut = Render<TmScheduler>(p => p.Add(c => c.TimeZoneId, "Not/AReal_Zone"));
        cut.Instance.ResolveTimeZone().Should().Be(TimeZoneInfo.Local);
    }

    [Fact]
    public void Recurrence_DailyAcrossSpringDst_KeepsLocalTime_ShiftsOffset()
    {
        var prague = Prague();
        // Spring-forward 2025 in Prague: Sun 30 Mar, 02:00 CET (+1) -> 03:00 CEST (+2).
        var source = new TmScheduleEvent
        {
            Title = "Standup",
            Start = new DateTimeOffset(2025, 3, 28, 9, 0, 0, TimeSpan.FromHours(1)), // 28 Mar 09:00 CET
            End = new DateTimeOffset(2025, 3, 28, 9, 30, 0, TimeSpan.FromHours(1)),
            RecurrenceRule = "FREQ=DAILY;COUNT=6"
        };

        var occ = RecurrenceEngine.ExpandRecurrence(source, new DateTime(2025, 3, 28), new DateTime(2025, 4, 3), prague);

        occ.Should().HaveCount(6);
        occ.Should().OnlyContain(o => o.StartLocal.Hour == 9 && o.StartLocal.Minute == 0); // wall-clock stays 09:00

        var before = occ.First(o => o.StartLocal.Date == new DateTime(2025, 3, 28));
        var after = occ.First(o => o.StartLocal.Date == new DateTime(2025, 3, 31));
        before.Start.Offset.Should().Be(TimeSpan.FromHours(1)); // CET
        after.Start.Offset.Should().Be(TimeSpan.FromHours(2));  // CEST (DST)
    }

    [Fact]
    public void EventsPath_ExpandsRecurrence_ForPlainEvents()
    {
        // Regression: the plain Events path previously ignored RecurrenceRule (only the provider expanded).
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, new DateTime(2025, 6, 15))
            .Add(c => c.Events, new List<TmScheduleEvent>
            {
                new()
                {
                    Title = "Daily",
                    Start = new DateTime(2025, 6, 2, 9, 0, 0),
                    End = new DateTime(2025, 6, 2, 10, 0, 0),
                    RecurrenceRule = "FREQ=DAILY;COUNT=5"
                }
            }));

        // 5 daily occurrences on 5 distinct June days → 5 rendered month events.
        cut.FindAll(".tm-scheduler-month-event").Count.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void NoTimeZoneId_NonRecurring_PassesEventThroughUnchanged()
    {
        // Backward compatibility: without TimeZoneId, non-recurring events are not copied/converted.
        var evt = new TmScheduleEvent
        {
            Title = "Meeting",
            Start = new DateTime(2025, 6, 10, 9, 0, 0),
            End = new DateTime(2025, 6, 10, 10, 0, 0)
        };
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, new DateTime(2025, 6, 15))
            .Add(c => c.Events, new List<TmScheduleEvent> { evt }));

        cut.FindAll(".tm-scheduler-month-event").Count.Should().Be(1);
    }
}
