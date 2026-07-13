using FluentAssertions;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>Reference test set for the extended RRULE support (positional BYDAY, BYSETPOS, UNTIL formats).</summary>
public class RecurrenceEngineExtendedTests
{
    private static List<DateTime> ExpandDates(string rrule, DateTime start, DateTime rangeStart, DateTime rangeEnd)
    {
        var src = new TmScheduleEvent { Title = "x", Start = start, End = start.AddHours(1), RecurrenceRule = rrule };
        return RecurrenceEngine.ExpandRecurrence(src, rangeStart, rangeEnd)
            .Select(o => o.StartLocal.Date)
            .ToList();
    }

    private static List<DateTime> ExpandDatesInZone(
        string rrule, DateTimeOffset start, DateTime rangeStart, DateTime rangeEnd, TimeZoneInfo zone)
    {
        var src = new TmScheduleEvent { Title = "x", Start = start, End = start.AddHours(1), RecurrenceRule = rrule };
        return RecurrenceEngine.ExpandRecurrence(src, rangeStart, rangeEnd, zone)
            .Select(o => o.StartLocal.Date)
            .ToList();
    }

    private static TimeZoneInfo NewYork()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
            {
                return tz;
            }
        }

        throw new InvalidOperationException("New York timezone not available on this machine.");
    }

    // ── WKST (RFC 5545 week-start) ──

    [Fact]
    public void Parse_Wkst_SetsWeekStart()
    {
        RecurrenceEngine.Parse("FREQ=WEEKLY;BYDAY=TU,SU;WKST=SU")!.WeekStart.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public void Parse_NoWkst_DefaultsToMonday()
    {
        RecurrenceEngine.Parse("FREQ=WEEKLY;BYDAY=TU,SU")!.WeekStart.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Serialize_NonDefaultWkst_IsEmitted()
    {
        var rule = new TmRecurrenceRule
        {
            Frequency = TmRecurrenceFrequency.Weekly,
            Interval = 2,
            ByDay = [DayOfWeek.Tuesday, DayOfWeek.Sunday],
            WeekStart = DayOfWeek.Sunday
        };

        RecurrenceEngine.Serialize(rule).Should().Contain("WKST=SU");
    }

    [Fact]
    public void Serialize_DefaultWkst_IsOmitted()
    {
        var rule = new TmRecurrenceRule
        {
            Frequency = TmRecurrenceFrequency.Weekly,
            ByDay = [DayOfWeek.Tuesday]
        };

        RecurrenceEngine.Serialize(rule).Should().NotContain("WKST");
    }

    [Fact]
    public void ParseSerialize_RoundTrip_PreservesWkst()
    {
        var serialized = RecurrenceEngine.Serialize(
            RecurrenceEngine.Parse("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,SU;WKST=SU")!);

        serialized.Should().Contain("WKST=SU");
        RecurrenceEngine.Parse(serialized)!.WeekStart.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public void Weekly_Interval2_ByDay_DefaultWkst_IsMonday_MatchesRfcExample()
    {
        // RFC 5545 example: FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST=MO (5 Aug 1997 = Tue)
        // ==> Aug 5, 10, 19, 24. WKST=MO is the default, so omitting it must match.
        var dates = ExpandDates("FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU",
            new DateTime(1997, 8, 5, 9, 0, 0), new DateTime(1997, 8, 1), new DateTime(1997, 9, 30));

        dates.Should().Equal(
            new DateTime(1997, 8, 5), new DateTime(1997, 8, 10),
            new DateTime(1997, 8, 19), new DateTime(1997, 8, 24));
    }

    [Fact]
    public void Weekly_Interval2_ByDay_WkstMonday_MatchesRfcExample()
    {
        // RFC 5545 example (explicit WKST=MO) ==> Aug 5, 10, 19, 24.
        var dates = ExpandDates("FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST=MO",
            new DateTime(1997, 8, 5, 9, 0, 0), new DateTime(1997, 8, 1), new DateTime(1997, 9, 30));

        dates.Should().Equal(
            new DateTime(1997, 8, 5), new DateTime(1997, 8, 10),
            new DateTime(1997, 8, 19), new DateTime(1997, 8, 24));
    }

    [Fact]
    public void Weekly_Interval2_ByDay_WkstSunday_MatchesRfcExample()
    {
        // RFC 5545 example: same rule but WKST=SU regroups the weeks ==> Aug 5, 17, 19, 31.
        var dates = ExpandDates("FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST=SU",
            new DateTime(1997, 8, 5, 9, 0, 0), new DateTime(1997, 8, 1), new DateTime(1997, 9, 30));

        dates.Should().Equal(
            new DateTime(1997, 8, 5), new DateTime(1997, 8, 17),
            new DateTime(1997, 8, 19), new DateTime(1997, 8, 31));
    }

    // ── UNTIL boundary normalized in the target timezone (DST safety) ──

    [Fact]
    public void Until_Utc_AcrossFallDst_NoDayDrift()
    {
        // America/New_York DST ends 2 Nov 2025 (EDT -4 → EST -5). A 19:30 local occurrence maps to
        // 23:30 UTC while EDT (same UTC day) but to 00:30 UTC the *next* day once EST. With
        // UNTIL=3 Nov 00:00 UTC, the last occurrence whose UTC date is ≤ 3 Nov is 2 Nov local; the
        // 3 Nov local occurrence (→ 4 Nov UTC) must be excluded — no ±1 day drift.
        var dates = ExpandDatesInZone(
            "FREQ=DAILY;UNTIL=20251103T000000Z",
            new DateTimeOffset(2025, 10, 31, 19, 30, 0, TimeSpan.FromHours(-4)),
            new DateTime(2025, 10, 31), new DateTime(2025, 11, 10), NewYork());

        dates.Should().Equal(
            new DateTime(2025, 10, 31),
            new DateTime(2025, 11, 1),
            new DateTime(2025, 11, 2));
    }

    [Fact]
    public void Until_DateOnly_Floating_AcrossFallDst_IsInclusiveWallClock()
    {
        // A floating date-only UNTIL is compared in the event's wall clock and is unaffected by the
        // DST offset shift: occurrences up to and including 3 Nov are kept.
        var dates = ExpandDatesInZone(
            "FREQ=DAILY;UNTIL=20251103",
            new DateTimeOffset(2025, 10, 31, 9, 0, 0, TimeSpan.FromHours(-4)),
            new DateTime(2025, 10, 31), new DateTime(2025, 11, 10), NewYork());

        dates.Should().Equal(
            new DateTime(2025, 10, 31),
            new DateTime(2025, 11, 1),
            new DateTime(2025, 11, 2),
            new DateTime(2025, 11, 3));
    }

    [Fact]
    public void Monthly_ThirdThursday()
    {
        var dates = ExpandDates("FREQ=MONTHLY;BYDAY=3TH;COUNT=3",
            new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 6, 1));

        dates.Should().Equal(new DateTime(2025, 1, 16), new DateTime(2025, 2, 20), new DateTime(2025, 3, 20));
    }

    [Fact]
    public void Monthly_LastFriday()
    {
        var dates = ExpandDates("FREQ=MONTHLY;BYDAY=-1FR;COUNT=2",
            new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 4, 1));

        dates.Should().Equal(new DateTime(2025, 1, 31), new DateTime(2025, 2, 28));
    }

    [Fact]
    public void Yearly_FourthThursdayOfNovember()
    {
        var dates = ExpandDates("FREQ=YEARLY;BYMONTH=11;BYDAY=4TH;COUNT=1",
            new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2026, 1, 1));

        dates.Should().Equal(new DateTime(2025, 11, 27));
    }

    [Fact]
    public void Monthly_LastWeekday_ViaBySetPos()
    {
        var dates = ExpandDates("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1;COUNT=3",
            new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 5, 1));

        // Jan 31 (Fri), Feb 28 (Fri), Mar 31 (Mon)
        dates.Should().Equal(new DateTime(2025, 1, 31), new DateTime(2025, 2, 28), new DateTime(2025, 3, 31));
    }

    [Fact]
    public void Until_DateOnlyFormat()
    {
        var dates = ExpandDates("FREQ=DAILY;UNTIL=20250105",
            new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 2, 1));

        dates.Should().HaveCount(5); // Jan 1..5
    }

    [Fact]
    public void Until_UtcFormat()
    {
        var dates = ExpandDates("FREQ=DAILY;UNTIL=20250103T235959Z",
            new DateTime(2025, 1, 1, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 2, 1));

        dates.Should().HaveCount(3); // Jan 1..3
    }

    [Fact]
    public void Weekly_EveryOtherMonday_Interval()
    {
        var dates = ExpandDates("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO;COUNT=3",
            new DateTime(2025, 1, 6, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 3, 1));

        // Jan 6, Jan 20, Feb 3
        dates.Should().Equal(new DateTime(2025, 1, 6), new DateTime(2025, 1, 20), new DateTime(2025, 2, 3));
    }

    [Fact]
    public void Monthly_ByMonthDay_DoesNotEmitBeforeDtStart()
    {
        // DTSTART Jan 15; BYMONTHDAY=10 → first occurrence is Feb 10, not Jan 10 (before the series).
        var dates = ExpandDates("FREQ=MONTHLY;BYMONTHDAY=10;COUNT=2",
            new DateTime(2025, 1, 15, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 6, 1));

        dates.Should().Equal(new DateTime(2025, 2, 10), new DateTime(2025, 3, 10));
    }

    [Fact]
    public void Weekly_MultiDay_IncludesEarlierWeekdaysInLaterWeeks()
    {
        // DTSTART Wed 8 Jan 2025; BYDAY=MO,WE,FR — Mondays must appear from the 2nd week on.
        var dates = ExpandDates("FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=5",
            new DateTime(2025, 1, 8, 9, 0, 0), new DateTime(2025, 1, 1), new DateTime(2025, 3, 1));

        dates.Should().Equal(
            new DateTime(2025, 1, 8),   // Wed (start)
            new DateTime(2025, 1, 10),  // Fri
            new DateTime(2025, 1, 13),  // Mon (next week)
            new DateTime(2025, 1, 15),  // Wed
            new DateTime(2025, 1, 17)); // Fri
    }

    [Fact]
    public void ParseSerialize_RoundTrip_PositionalByDayAndBySetPos()
    {
        var rule = RecurrenceEngine.Parse("FREQ=MONTHLY;BYDAY=3TH;BYSETPOS=-1");
        rule.Should().NotBeNull();
        rule!.ByDayPositional.Should().ContainSingle().Which.Should().Be((3, DayOfWeek.Thursday));
        rule.BySetPos.Should().Equal(-1);

        var serialized = RecurrenceEngine.Serialize(rule);
        serialized.Should().Contain("BYDAY=3TH");
        serialized.Should().Contain("BYSETPOS=-1");
    }
}
