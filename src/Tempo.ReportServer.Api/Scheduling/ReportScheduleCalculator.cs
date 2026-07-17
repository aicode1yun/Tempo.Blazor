namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>Behaviour when a schedule has missed one or more occurrences (e.g. after downtime).</summary>
public enum MissedRunPolicy
{
    /// <summary>Fire a single run for the most recent missed occurrence and skip the rest.</summary>
    Skip,

    /// <summary>Fire one run for every missed occurrence, oldest first.</summary>
    CatchUp,
}

/// <summary>Result of evaluating a schedule against the current instant.</summary>
/// <param name="Occurrences">
/// The logical occurrence timestamps that must be executed now, oldest first. Empty when the
/// schedule is not yet due.
/// </param>
/// <param name="NextRunUtc">The next occurrence strictly after <c>now</c> that should be persisted.</param>
public sealed record MissedRunDecision(IReadOnlyList<DateTimeOffset> Occurrences, DateTimeOffset NextRunUtc)
{
    /// <summary>True when at least one occurrence is due.</summary>
    public bool IsDue => Occurrences.Count > 0;
}

/// <summary>
/// Pure, clock-free timing logic for report schedules: computes the next run, resolves which missed
/// occurrences are due under a <see cref="MissedRunPolicy"/>, and derives exponential retry backoff.
/// Every method takes the reference instant as an argument so the worker can pass
/// <see cref="TimeProvider.GetUtcNow"/> and tests can pass a fixed value.
/// </summary>
public static class ReportScheduleCalculator
{
    /// <summary>Computes the first occurrence strictly after <paramref name="afterUtc"/>.</summary>
    public static DateTimeOffset ComputeNextRun(string cronExpression, DateTimeOffset afterUtc)
        => ReportScheduleCron.Parse(cronExpression).GetNextOccurrence(afterUtc);

    /// <summary>
    /// Resolves the occurrences that are due at <paramref name="nowUtc"/>.
    /// A schedule is due when <paramref name="nextRunUtc"/> is at or before <paramref name="nowUtc"/>.
    /// Under <see cref="MissedRunPolicy.Skip"/> at most one run (the latest missed occurrence) is
    /// returned; under <see cref="MissedRunPolicy.CatchUp"/> every missed occurrence is returned.
    /// </summary>
    public static MissedRunDecision ResolveDueRuns(
        string cronExpression,
        DateTimeOffset? lastRunUtc,
        DateTimeOffset nextRunUtc,
        DateTimeOffset nowUtc,
        MissedRunPolicy policy,
        int maxCatchUpRuns = 1000)
    {
        var cron = ReportScheduleCron.Parse(cronExpression);
        if (nextRunUtc > nowUtc)
        {
            return new MissedRunDecision([], nextRunUtc);
        }

        // The window opens just before the scheduled trigger (or after the last successful run when
        // one exists) and closes at "now", inclusive, so the triggering occurrence is always caught.
        var windowStart = lastRunUtc is { } last && last >= nextRunUtc
            ? last
            : nextRunUtc.AddTicks(-1);

        var due = cron.GetOccurrencesBetween(windowStart, nowUtc, maxCatchUpRuns);
        if (due.Count == 0)
        {
            // Defensive: the persisted next-run pointer is stale relative to the expression; treat the
            // pointer itself as the single due occurrence so a due schedule never silently stalls.
            due = [nextRunUtc];
        }

        var occurrences = policy == MissedRunPolicy.CatchUp ? due : [due[^1]];
        var newNext = cron.GetNextOccurrence(nowUtc);
        return new MissedRunDecision(occurrences, newNext);
    }

    /// <summary>
    /// Exponential retry backoff for delivery attempt <paramref name="attempt"/> (1-based): the delay
    /// doubles per attempt starting from <paramref name="baseDelay"/> and is clamped to
    /// <paramref name="maxDelay"/>.
    /// </summary>
    public static TimeSpan ComputeRetryBackoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempt must be 1 or greater.");
        }

        if (baseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay, "Base delay must be positive.");
        }

        // Cap the exponent so the multiplication cannot overflow for large attempt counts.
        var exponent = Math.Min(attempt - 1, 30);
        var scaled = baseDelay.Ticks * (double)(1L << exponent);
        var ticks = scaled >= maxDelay.Ticks ? maxDelay.Ticks : (long)scaled;
        return TimeSpan.FromTicks(Math.Min(ticks, maxDelay.Ticks));
    }

    /// <summary>
    /// Computes the next retry instant after a failed attempt, or <c>null</c> when the attempt count
    /// has reached <paramref name="maxAttempts"/> (delivery is then abandoned).
    /// </summary>
    public static DateTimeOffset? ComputeRetryAt(
        int attempt,
        int maxAttempts,
        DateTimeOffset nowUtc,
        TimeSpan baseDelay,
        TimeSpan maxDelay)
    {
        if (attempt >= maxAttempts)
        {
            return null;
        }

        return nowUtc + ComputeRetryBackoff(attempt, baseDelay, maxDelay);
    }
}
