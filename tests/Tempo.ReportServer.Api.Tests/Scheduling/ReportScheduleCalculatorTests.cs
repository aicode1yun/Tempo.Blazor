using System.Globalization;
using Tempo.ReportServer.Api.Scheduling;

namespace Tempo.ReportServer.Api.Tests.Scheduling;

/// <summary>
/// Deterministic unit tests for the clock-free schedule timing surface: cron next-run computation,
/// missed-run policy (catch-up vs skip) and exponential retry backoff. All instants are supplied
/// explicitly, so the tests never read the wall clock.
/// </summary>
public sealed class ReportScheduleCalculatorTests
{
    private static DateTimeOffset Utc(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    [Fact]
    public void ComputeNextRun_WeeklyMondayEightAm_ReturnsNextMonday()
    {
        // 2026-07-17 is a Friday; the next "0 8 * * 1" (Monday 08:00) is 2026-07-20.
        var next = ReportScheduleCalculator.ComputeNextRun("0 8 * * 1", Utc("2026-07-17T09:00:00Z"));

        next.Should().Be(Utc("2026-07-20T08:00:00Z"));
    }

    [Fact]
    public void ComputeNextRun_IsStrictlyAfterReferenceInstant()
    {
        // Standing exactly on an occurrence must advance to the following one, never return "now".
        var next = ReportScheduleCalculator.ComputeNextRun("0 8 * * *", Utc("2026-07-17T08:00:00Z"));

        next.Should().Be(Utc("2026-07-18T08:00:00Z"));
    }

    [Theory]
    [InlineData("@hourly", "2026-07-17T08:30:00Z", "2026-07-17T09:00:00Z")]
    [InlineData("@daily", "2026-07-17T08:30:00Z", "2026-07-18T00:00:00Z")]
    [InlineData("*/15 * * * *", "2026-07-17T08:07:00Z", "2026-07-17T08:15:00Z")]
    [InlineData("0 9 * * 1-5", "2026-07-17T10:00:00Z", "2026-07-20T09:00:00Z")]
    public void ComputeNextRun_SupportsMacrosStepsAndRanges(string cron, string after, string expected)
    {
        ReportScheduleCalculator.ComputeNextRun(cron, Utc(after)).Should().Be(Utc(expected));
    }

    [Fact]
    public void Parse_InvalidExpression_Throws()
    {
        var act = () => ReportScheduleCron.Parse("not a cron");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ResolveDueRuns_NotYetDue_ReturnsNoOccurrences()
    {
        var decision = ReportScheduleCalculator.ResolveDueRuns(
            "0 * * * *",
            lastRunUtc: null,
            nextRunUtc: Utc("2026-07-17T10:00:00Z"),
            nowUtc: Utc("2026-07-17T09:30:00Z"),
            MissedRunPolicy.CatchUp);

        decision.IsDue.Should().BeFalse();
        decision.Occurrences.Should().BeEmpty();
        decision.NextRunUtc.Should().Be(Utc("2026-07-17T10:00:00Z"));
    }

    [Fact]
    public void ResolveDueRuns_CatchUp_ReturnsEveryMissedOccurrence()
    {
        // Hourly schedule last ran at 08:00, worker wakes at 12:10 after downtime => 09,10,11,12 due.
        var decision = ReportScheduleCalculator.ResolveDueRuns(
            "0 * * * *",
            lastRunUtc: Utc("2026-07-17T08:00:00Z"),
            nextRunUtc: Utc("2026-07-17T09:00:00Z"),
            nowUtc: Utc("2026-07-17T12:10:00Z"),
            MissedRunPolicy.CatchUp);

        decision.IsDue.Should().BeTrue();
        decision.Occurrences.Should().Equal(
            Utc("2026-07-17T09:00:00Z"),
            Utc("2026-07-17T10:00:00Z"),
            Utc("2026-07-17T11:00:00Z"),
            Utc("2026-07-17T12:00:00Z"));
        decision.NextRunUtc.Should().Be(Utc("2026-07-17T13:00:00Z"));
    }

    [Fact]
    public void ResolveDueRuns_Skip_CollapsesMissedOccurrencesToTheLatest()
    {
        var decision = ReportScheduleCalculator.ResolveDueRuns(
            "0 * * * *",
            lastRunUtc: Utc("2026-07-17T08:00:00Z"),
            nextRunUtc: Utc("2026-07-17T09:00:00Z"),
            nowUtc: Utc("2026-07-17T12:10:00Z"),
            MissedRunPolicy.Skip);

        decision.Occurrences.Should().ContainSingle()
            .Which.Should().Be(Utc("2026-07-17T12:00:00Z"));
        decision.NextRunUtc.Should().Be(Utc("2026-07-17T13:00:00Z"));
    }

    [Fact]
    public void ResolveDueRuns_CatchUp_RespectsMaxCatchUpCap()
    {
        var decision = ReportScheduleCalculator.ResolveDueRuns(
            "* * * * *",
            lastRunUtc: Utc("2026-07-17T08:00:00Z"),
            nextRunUtc: Utc("2026-07-17T08:01:00Z"),
            nowUtc: Utc("2026-07-17T20:00:00Z"),
            MissedRunPolicy.CatchUp,
            maxCatchUpRuns: 5);

        decision.Occurrences.Should().HaveCount(5);
    }

    [Fact]
    public void ComputeRetryBackoff_DoublesPerAttemptAndClampsToMax()
    {
        var baseDelay = TimeSpan.FromMinutes(1);
        var max = TimeSpan.FromMinutes(30);

        ReportScheduleCalculator.ComputeRetryBackoff(1, baseDelay, max).Should().Be(TimeSpan.FromMinutes(1));
        ReportScheduleCalculator.ComputeRetryBackoff(2, baseDelay, max).Should().Be(TimeSpan.FromMinutes(2));
        ReportScheduleCalculator.ComputeRetryBackoff(3, baseDelay, max).Should().Be(TimeSpan.FromMinutes(4));
        ReportScheduleCalculator.ComputeRetryBackoff(4, baseDelay, max).Should().Be(TimeSpan.FromMinutes(8));
        ReportScheduleCalculator.ComputeRetryBackoff(10, baseDelay, max).Should().Be(max);
    }

    [Fact]
    public void ComputeRetryAt_AddsBackoffToNow_WhenAttemptsRemain()
    {
        var now = Utc("2026-07-17T09:00:00Z");

        var retryAt = ReportScheduleCalculator.ComputeRetryAt(2, maxAttempts: 5, now, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));

        retryAt.Should().Be(now.AddMinutes(2));
    }

    [Fact]
    public void ComputeRetryAt_ReturnsNull_WhenAttemptsExhausted()
    {
        var retryAt = ReportScheduleCalculator.ComputeRetryAt(5, maxAttempts: 5, Utc("2026-07-17T09:00:00Z"), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));

        retryAt.Should().BeNull();
    }
}
