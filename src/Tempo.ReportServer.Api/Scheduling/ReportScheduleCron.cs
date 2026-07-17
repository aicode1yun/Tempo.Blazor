using System.Globalization;

namespace Tempo.ReportServer.Api.Scheduling;

/// <summary>
/// Deterministic five-field cron parser (minute, hour, day-of-month, month, day-of-week) evaluated
/// in UTC. Supports <c>*</c>, comma lists, ranges (<c>a-b</c>) and step values (<c>*&#47;n</c> or
/// <c>a-b&#47;n</c>), plus the <c>@hourly</c>/<c>@daily</c>/<c>@weekly</c>/<c>@monthly</c> macros.
/// The type is immutable and clock-free: callers pass the reference instant explicitly so the whole
/// schedule-timing surface is unit-testable without touching the wall clock.
/// </summary>
public sealed class ReportScheduleCron
{
    private const int LookaheadMinutes = 366 * 24 * 60;

    private readonly CronField _minute;
    private readonly CronField _hour;
    private readonly CronField _dayOfMonth;
    private readonly CronField _month;
    private readonly CronField _dayOfWeek;

    private ReportScheduleCron(
        string expression,
        CronField minute,
        CronField hour,
        CronField dayOfMonth,
        CronField month,
        CronField dayOfWeek)
    {
        Expression = expression;
        _minute = minute;
        _hour = hour;
        _dayOfMonth = dayOfMonth;
        _month = month;
        _dayOfWeek = dayOfWeek;
    }

    /// <summary>The normalized cron expression this schedule was parsed from.</summary>
    public string Expression { get; }

    /// <summary>Parses a five-field cron expression (UTC). Throws <see cref="FormatException"/> when invalid.</summary>
    public static ReportScheduleCron Parse(string expression)
    {
        var normalized = Normalize(expression);
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            throw new FormatException("Cron expression must contain five whitespace-separated fields.");
        }

        return new ReportScheduleCron(
            normalized,
            CronField.Parse(parts[0], 0, 59),
            CronField.Parse(parts[1], 0, 23),
            CronField.Parse(parts[2], 1, 31),
            CronField.Parse(parts[3], 1, 12),
            CronField.Parse(parts[4], 0, 7));
    }

    /// <summary>Returns true when <paramref name="expression"/> is a valid five-field cron expression.</summary>
    public static bool TryParse(string expression, out ReportScheduleCron? schedule)
    {
        try
        {
            schedule = Parse(expression);
            return true;
        }
        catch (FormatException)
        {
            schedule = null;
            return false;
        }
    }

    /// <summary>Finds the next matching whole UTC minute strictly after <paramref name="afterUtc"/>.</summary>
    public DateTimeOffset GetNextOccurrence(DateTimeOffset afterUtc)
    {
        var candidate = FloorToMinute(afterUtc).AddMinutes(1);
        for (var i = 0; i < LookaheadMinutes; i++)
        {
            if (Matches(candidate))
            {
                return candidate;
            }

            candidate = candidate.AddMinutes(1);
        }

        throw new InvalidOperationException("Cron expression produced no occurrence within one year.");
    }

    /// <summary>
    /// Enumerates every matching occurrence in the half-open window
    /// (<paramref name="afterExclusiveUtc"/>, <paramref name="throughInclusiveUtc"/>], oldest first,
    /// capped at <paramref name="maxOccurrences"/> entries (the cap protects catch-up from unbounded
    /// backfill after a long outage).
    /// </summary>
    public IReadOnlyList<DateTimeOffset> GetOccurrencesBetween(
        DateTimeOffset afterExclusiveUtc,
        DateTimeOffset throughInclusiveUtc,
        int maxOccurrences = 1000)
    {
        if (maxOccurrences <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOccurrences));
        }

        var results = new List<DateTimeOffset>();
        if (throughInclusiveUtc <= afterExclusiveUtc)
        {
            return results;
        }

        var candidate = GetNextOccurrence(afterExclusiveUtc);
        while (candidate <= throughInclusiveUtc && results.Count < maxOccurrences)
        {
            results.Add(candidate);
            candidate = GetNextOccurrence(candidate);
        }

        return results;
    }

    private static DateTimeOffset FloorToMinute(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }

    private static string Normalize(string expression)
        => (expression ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "@hourly" => "0 * * * *",
            "@daily" or "@midnight" => "0 0 * * *",
            "@weekly" => "0 0 * * 0",
            "@monthly" => "0 0 1 * *",
            var value when !string.IsNullOrWhiteSpace(value) => value,
            _ => throw new FormatException("Cron expression is required."),
        };

    private bool Matches(DateTimeOffset candidate)
    {
        var dayOfWeek = (int)candidate.DayOfWeek;
        return _minute.Contains(candidate.Minute)
            && _hour.Contains(candidate.Hour)
            && _dayOfMonth.Contains(candidate.Day)
            && _month.Contains(candidate.Month)
            && (_dayOfWeek.Contains(dayOfWeek) || (dayOfWeek == 0 && _dayOfWeek.Contains(7)));
    }

    private sealed class CronField
    {
        private readonly HashSet<int>? _values;

        private CronField(HashSet<int>? values) => _values = values;

        public static CronField Parse(string field, int min, int max)
        {
            if (field == "*")
            {
                return new CronField(null);
            }

            var values = new HashSet<int>();
            foreach (var token in field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                ParseToken(token, min, max, values);
            }

            if (values.Count == 0)
            {
                throw new FormatException("Cron field must not be empty.");
            }

            return new CronField(values);
        }

        public bool Contains(int value) => _values is null || _values.Contains(value);

        private static void ParseToken(string token, int min, int max, HashSet<int> values)
        {
            var step = 1;
            var body = token;
            var slashIndex = token.IndexOf('/', StringComparison.Ordinal);
            if (slashIndex >= 0)
            {
                body = token[..slashIndex];
                if (!int.TryParse(token[(slashIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out step) || step <= 0)
                {
                    throw new FormatException($"Cron step '{token}' is invalid.");
                }
            }

            int rangeStart;
            int rangeEnd;
            if (body == "*")
            {
                rangeStart = min;
                rangeEnd = max;
            }
            else
            {
                var dashIndex = body.IndexOf('-', StringComparison.Ordinal);
                if (dashIndex >= 0)
                {
                    rangeStart = ParseValue(body[..dashIndex], min, max);
                    rangeEnd = ParseValue(body[(dashIndex + 1)..], min, max);
                }
                else
                {
                    rangeStart = rangeEnd = ParseValue(body, min, max);
                }
            }

            if (rangeEnd < rangeStart)
            {
                throw new FormatException($"Cron range '{token}' is descending.");
            }

            for (var value = rangeStart; value <= rangeEnd; value += step)
            {
                values.Add(value);
            }
        }

        private static int ParseValue(string text, int min, int max)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            {
                throw new FormatException($"Cron field value '{text}' is out of range [{min}, {max}].");
            }

            return value;
        }
    }
}
