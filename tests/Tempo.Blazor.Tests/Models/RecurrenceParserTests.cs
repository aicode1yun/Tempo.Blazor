using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Models;

public class RecurrenceParserTests
{
    [Fact]
    public void ToRRule_Daily_Returns_Correct_String()
    {
        var rule = new RecurrenceRule { Pattern = RecurrencePattern.Daily, Interval = 1 };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=DAILY");
    }

    [Fact]
    public void ToRRule_Daily_With_Interval()
    {
        var rule = new RecurrenceRule { Pattern = RecurrencePattern.Daily, Interval = 3 };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=DAILY;INTERVAL=3");
    }

    [Fact]
    public void ToRRule_Weekly_With_Days()
    {
        var rule = new RecurrenceRule
        {
            Pattern = RecurrencePattern.Weekly,
            Interval = 1,
            DaysOfWeek = [1, 3, 5] // Mon, Wed, Fri
        };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=WEEKLY;BYDAY=MO,WE,FR");
    }

    [Fact]
    public void ToRRule_Monthly_By_DayOfMonth()
    {
        var rule = new RecurrenceRule
        {
            Pattern = RecurrencePattern.Monthly,
            DayOfMonth = 15
        };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=MONTHLY;BYMONTHDAY=15");
    }

    [Fact]
    public void ToRRule_Monthly_By_Position()
    {
        var rule = new RecurrenceRule
        {
            Pattern = RecurrencePattern.Monthly,
            Position = 2,
            DaysOfWeek = [1] // Monday
        };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=MONTHLY;BYDAY=+2MO");
    }

    [Fact]
    public void ToRRule_Yearly()
    {
        var rule = new RecurrenceRule
        {
            Pattern = RecurrencePattern.Yearly,
            MonthOfYear = 6,
            DayOfMonth = 15
        };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=YEARLY;BYMONTH=6;BYMONTHDAY=15");
    }

    [Fact]
    public void ToRRule_With_Count_End()
    {
        var rule = new RecurrenceRule { Pattern = RecurrencePattern.Daily, Interval = 1, EndAfter = 10 };
        var result = RecurrenceParser.ToRRule(rule);
        result.Should().Be("FREQ=DAILY;COUNT=10");
    }

    [Fact]
    public void FromRRule_Daily_Parses_Correctly()
    {
        var rule = RecurrenceParser.FromRRule("FREQ=DAILY");
        rule.Pattern.Should().Be(RecurrencePattern.Daily);
        rule.Interval.Should().Be(1);
    }

    [Fact]
    public void FromRRule_Weekly_With_Days_Parses_Correctly()
    {
        var rule = RecurrenceParser.FromRRule("FREQ=WEEKLY;BYDAY=MO,WE,FR");
        rule.Pattern.Should().Be(RecurrencePattern.Weekly);
        rule.DaysOfWeek.Should().BeEquivalentTo(new[] { 1, 3, 5 });
    }

    [Fact]
    public void FromRRule_Monthly_By_DayOfMonth_Parses_Correctly()
    {
        var rule = RecurrenceParser.FromRRule("FREQ=MONTHLY;BYMONTHDAY=15");
        rule.Pattern.Should().Be(RecurrencePattern.Monthly);
        rule.DayOfMonth.Should().Be(15);
    }

    [Fact]
    public void FromRRule_Monthly_By_Position_Parses_Correctly()
    {
        var rule = RecurrenceParser.FromRRule("FREQ=MONTHLY;BYDAY=-1FR");
        rule.Pattern.Should().Be(RecurrencePattern.Monthly);
        rule.Position.Should().Be(-1);
        rule.DaysOfWeek.Should().BeEquivalentTo(new[] { 5 });
    }

    [Fact]
    public void FromRRule_Yearly_Parses_Correctly()
    {
        var rule = RecurrenceParser.FromRRule("FREQ=YEARLY;BYMONTH=6;BYMONTHDAY=15");
        rule.Pattern.Should().Be(RecurrencePattern.Yearly);
        rule.MonthOfYear.Should().Be(6);
        rule.DayOfMonth.Should().Be(15);
    }

    [Fact]
    public void FromRRule_With_Count_Parses_Correctly()
    {
        var rule = RecurrenceParser.FromRRule("FREQ=DAILY;COUNT=5");
        rule.EndAfter.Should().Be(5);
    }

    [Fact]
    public void RoundTrip_Preserves_All_Properties()
    {
        var original = new RecurrenceRule
        {
            Pattern = RecurrencePattern.Weekly,
            Interval = 2,
            DaysOfWeek = [2, 4], // Tue, Thu
            EndAfter = 20
        };

        var rrule = RecurrenceParser.ToRRule(original);
        var parsed = RecurrenceParser.FromRRule(rrule);

        parsed.Pattern.Should().Be(original.Pattern);
        parsed.Interval.Should().Be(original.Interval);
        parsed.DaysOfWeek.Should().BeEquivalentTo(original.DaysOfWeek);
        parsed.EndAfter.Should().Be(original.EndAfter);
    }
}
