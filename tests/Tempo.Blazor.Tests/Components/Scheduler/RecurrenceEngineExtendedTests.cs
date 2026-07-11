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
