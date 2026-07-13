using FluentAssertions;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>ICS (RFC 5545) export/import + round-trip.</summary>
public class IcsCalendarSerializerTests
{
    private readonly IcsCalendarSerializer _ics = new();

    private static TmScheduleEvent Meeting() => new()
    {
        Id = "e1",
        Title = "Meeting",
        Start = new DateTimeOffset(2025, 6, 10, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2025, 6, 10, 10, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public void Export_ProducesVCalendarStructure()
    {
        var ics = _ics.Export([Meeting()], "My Cal");

        _ics.ContentType.Should().Be("text/calendar");
        _ics.FileExtension.Should().Be("ics");
        ics.Should().Contain("BEGIN:VCALENDAR");
        ics.Should().Contain("VERSION:2.0");
        ics.Should().Contain("X-WR-CALNAME:My Cal");
        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("SUMMARY:Meeting");
        ics.Should().Contain("DTSTART:20250610T090000Z");
        ics.Should().Contain("DTEND:20250610T100000Z");
        ics.Should().Contain("END:VEVENT");
        ics.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public void Export_AllDay_UsesValueDate()
    {
        var ics = _ics.Export([new TmScheduleEvent
        {
            Title = "Holiday",
            AllDay = true,
            Start = new DateTimeOffset(2025, 7, 4, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2025, 7, 5, 0, 0, 0, TimeSpan.Zero)
        }]);

        ics.Should().Contain("DTSTART;VALUE=DATE:20250704");
        ics.Should().Contain("DTEND;VALUE=DATE:20250705");
    }

    [Fact]
    public void RoundTrip_PreservesTitleInstantAndRule()
    {
        var original = new TmScheduleEvent
        {
            Id = "e1",
            Title = "Standup",
            Description = "Daily sync",
            Start = new DateTimeOffset(2025, 6, 10, 9, 0, 0, TimeSpan.FromHours(2)),
            End = new DateTimeOffset(2025, 6, 10, 9, 30, 0, TimeSpan.FromHours(2)),
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO,WE,FR"
        };

        var back = _ics.Import(_ics.Export([original]));

        back.Should().ContainSingle();
        var e = back[0];
        e.Id.Should().Be("e1");
        e.Title.Should().Be("Standup");
        e.Description.Should().Be("Daily sync");
        e.Start.ToUniversalTime().Should().Be(original.Start.ToUniversalTime());
        e.End.ToUniversalTime().Should().Be(original.End.ToUniversalTime());
        e.RecurrenceRule.Should().Be("FREQ=WEEKLY;BYDAY=MO,WE,FR");
    }

    [Fact]
    public void RoundTrip_PreservesWkstInRrule()
    {
        var original = new TmScheduleEvent
        {
            Id = "wk1",
            Title = "Biweekly pair",
            Start = new DateTimeOffset(1997, 8, 5, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(1997, 8, 5, 10, 0, 0, TimeSpan.Zero),
            RecurrenceRule = RecurrenceEngine.Serialize(new TmRecurrenceRule
            {
                Frequency = TmRecurrenceFrequency.Weekly,
                Interval = 2,
                ByDay = [DayOfWeek.Tuesday, DayOfWeek.Sunday],
                WeekStart = DayOfWeek.Sunday
            })
        };

        var ics = _ics.Export([original]);
        ics.Should().Contain("WKST=SU");

        var back = _ics.Import(ics);
        back[0].RecurrenceRule.Should().Contain("WKST=SU");
        RecurrenceEngine.Parse(back[0].RecurrenceRule)!.WeekStart.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public void RoundTrip_EscapesSpecialCharacters()
    {
        var original = new TmScheduleEvent
        {
            Title = "Lunch, then; meeting",
            Description = "line1\nline2",
            Start = new DateTimeOffset(2025, 6, 10, 12, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2025, 6, 10, 13, 0, 0, TimeSpan.Zero)
        };

        var ics = _ics.Export([original]);
        ics.Should().Contain("SUMMARY:Lunch\\, then\\; meeting");

        var back = _ics.Import(ics);
        back[0].Title.Should().Be("Lunch, then; meeting");
        back[0].Description.Should().Be("line1\nline2");
    }

    [Fact]
    public void Import_AllDay_SetsAllDayFlag()
    {
        const string ics =
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:h1\r\nSUMMARY:Holiday\r\n" +
            "DTSTART;VALUE=DATE:20250704\r\nDTEND;VALUE=DATE:20250705\r\nEND:VEVENT\r\nEND:VCALENDAR";

        var back = _ics.Import(ics);

        back.Should().ContainSingle();
        back[0].AllDay.Should().BeTrue();
        back[0].StartLocal.Date.Should().Be(new DateTime(2025, 7, 4));
    }

    [Fact]
    public void RoundTrip_PreservesLiteralBackslash()
    {
        var original = new TmScheduleEvent
        {
            Title = @"Path C:\notes, and more",
            Start = new DateTimeOffset(2025, 6, 10, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2025, 6, 10, 10, 0, 0, TimeSpan.Zero)
        };

        var back = _ics.Import(_ics.Export([original]));

        back[0].Title.Should().Be(@"Path C:\notes, and more");
    }

    [Fact]
    public void Import_WithDuration_NoDtEnd_ComputesEnd()
    {
        const string ics =
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:d1\r\nSUMMARY:Call\r\n" +
            "DTSTART:20250610T090000Z\r\nDURATION:PT1H30M\r\nEND:VEVENT\r\nEND:VCALENDAR";

        var e = _ics.Import(ics)[0];

        (e.End - e.Start).Should().Be(TimeSpan.FromMinutes(90));
    }

    [Fact]
    public void Import_NoDtEndNoDuration_DefaultsEndToStart()
    {
        const string ics =
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x\r\nSUMMARY:Ping\r\n" +
            "DTSTART:20250610T090000Z\r\nEND:VEVENT\r\nEND:VCALENDAR";

        var e = _ics.Import(ics)[0];

        e.End.Should().Be(e.Start);
    }

    [Fact]
    public void Export_LongLine_IsFoldedTo75Octets()
    {
        var ics = _ics.Export([new TmScheduleEvent
        {
            Title = "X",
            Description = new string('x', 200),
            Start = new DateTimeOffset(2025, 6, 10, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2025, 6, 10, 10, 0, 0, TimeSpan.Zero)
        }]);

        ics.Split("\r\n").Where(l => l.Length > 0).Should().OnlyContain(l => l.Length <= 75);
    }
}
