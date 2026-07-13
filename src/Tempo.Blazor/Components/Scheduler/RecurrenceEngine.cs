using System.Globalization;
using System.Text;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Scheduler;

/// <summary>
/// Engine for parsing, serializing, and expanding recurrence rules (subset of RFC 5545 RRULE).
/// </summary>
internal static class RecurrenceEngine
{
    private const int MaxExpansions = 1000;

    private static readonly Dictionary<string, DayOfWeek> DayMap = new()
    {
        ["MO"] = DayOfWeek.Monday,
        ["TU"] = DayOfWeek.Tuesday,
        ["WE"] = DayOfWeek.Wednesday,
        ["TH"] = DayOfWeek.Thursday,
        ["FR"] = DayOfWeek.Friday,
        ["SA"] = DayOfWeek.Saturday,
        ["SU"] = DayOfWeek.Sunday,
    };

    private static readonly Dictionary<DayOfWeek, string> ReverseDayMap =
        DayMap.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>
    /// Parse an RRULE string into a TmRecurrenceRule object.
    /// </summary>
    public static TmRecurrenceRule? Parse(string? rrule)
    {
        if (string.IsNullOrWhiteSpace(rrule)) return null;

        var rule = new TmRecurrenceRule();
        var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = part[..eqIndex].Trim().ToUpperInvariant();
            var value = part[(eqIndex + 1)..].Trim();

            switch (key)
            {
                case "FREQ":
                    rule.Frequency = value.ToUpperInvariant() switch
                    {
                        "DAILY" => TmRecurrenceFrequency.Daily,
                        "WEEKLY" => TmRecurrenceFrequency.Weekly,
                        "MONTHLY" => TmRecurrenceFrequency.Monthly,
                        "YEARLY" => TmRecurrenceFrequency.Yearly,
                        _ => TmRecurrenceFrequency.Daily
                    };
                    break;

                case "INTERVAL":
                    if (int.TryParse(value, out var interval))
                        rule.Interval = Math.Max(1, interval);
                    break;

                case "COUNT":
                    if (int.TryParse(value, out var count))
                        rule.Count = count;
                    break;

                case "UNTIL":
                    rule.Until = ParseUntil(value);
                    break;

                case "BYDAY":
                    ParseByDay(value, rule);
                    break;

                case "WKST":
                    if (DayMap.TryGetValue(value.ToUpperInvariant(), out var weekStart))
                        rule.WeekStart = weekStart;
                    break;

                case "BYSETPOS":
                    rule.BySetPos = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => int.TryParse(p.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var pos) ? pos : 0)
                        .Where(p => p != 0)
                        .ToArray();
                    break;

                case "BYMONTHDAY":
                    rule.ByMonthDay = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(d => int.TryParse(d.Trim(), out var day) ? day : 0)
                        .Where(d => d >= 1 && d <= 31)
                        .ToArray();
                    break;

                case "BYMONTH":
                    rule.ByMonth = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => int.TryParse(m.Trim(), out var month) ? month : 0)
                        .Where(m => m >= 1 && m <= 12)
                        .ToArray();
                    break;
            }
        }

        return rule;
    }

    private static readonly string[] UntilFormats =
    [
        "yyyyMMdd'T'HHmmss'Z'",
        "yyyyMMdd'T'HHmmss",
        "yyyyMMdd"
    ];

    private static DateTime? ParseUntil(string value)
    {
        value = value.Trim();
        var utc = value.EndsWith('Z');
        if (DateTime.TryParseExact(value, UntilFormats, CultureInfo.InvariantCulture,
                utc ? DateTimeStyles.AssumeUniversal : DateTimeStyles.None, out var parsed))
        {
            return utc ? parsed.ToUniversalTime() : parsed;
        }

        return null;
    }

    private static void ParseByDay(string value, TmRecurrenceRule rule)
    {
        var plain = new List<DayOfWeek>();
        var positional = new List<(int, DayOfWeek)>();

        foreach (var raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim().ToUpperInvariant();
            if (token.Length < 2) continue;

            var dayCode = token[^2..];
            if (!DayMap.TryGetValue(dayCode, out var day)) continue;

            var prefix = token[..^2];
            if (string.IsNullOrEmpty(prefix))
            {
                plain.Add(day);
            }
            else if (int.TryParse(prefix, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ordinal) && ordinal != 0)
            {
                positional.Add((ordinal, day));
            }
        }

        rule.ByDay = plain.Count > 0 ? plain.ToArray() : null;
        rule.ByDayPositional = positional.Count > 0 ? positional.ToArray() : null;
    }

    /// <summary>
    /// Serialize a TmRecurrenceRule into an RRULE string.
    /// </summary>
    public static string Serialize(TmRecurrenceRule rule)
    {
        var sb = new StringBuilder();

        sb.Append("FREQ=");
        sb.Append(rule.Frequency switch
        {
            TmRecurrenceFrequency.Daily => "DAILY",
            TmRecurrenceFrequency.Weekly => "WEEKLY",
            TmRecurrenceFrequency.Monthly => "MONTHLY",
            TmRecurrenceFrequency.Yearly => "YEARLY",
            _ => "DAILY"
        });

        sb.Append(";INTERVAL=");
        sb.Append(Math.Max(1, rule.Interval));

        if (rule.Count.HasValue)
        {
            sb.Append(";COUNT=");
            sb.Append(rule.Count.Value);
        }

        if (rule.Until.HasValue)
        {
            sb.Append(";UNTIL=");
            sb.Append(rule.Until.Value.Kind == DateTimeKind.Utc
                ? rule.Until.Value.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
                : rule.Until.Value.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
        }

        var byDayParts = new List<string>();
        if (rule.ByDay is { Length: > 0 })
            byDayParts.AddRange(rule.ByDay.Select(d => ReverseDayMap[d]));
        if (rule.ByDayPositional is { Length: > 0 })
            byDayParts.AddRange(rule.ByDayPositional.Select(p => $"{p.Ordinal}{ReverseDayMap[p.Day]}"));
        if (byDayParts.Count > 0)
        {
            sb.Append(";BYDAY=");
            sb.Append(string.Join(",", byDayParts));
        }

        if (rule.ByMonthDay is { Length: > 0 })
        {
            sb.Append(";BYMONTHDAY=");
            sb.Append(string.Join(",", rule.ByMonthDay));
        }

        if (rule.ByMonth is { Length: > 0 })
        {
            sb.Append(";BYMONTH=");
            sb.Append(string.Join(",", rule.ByMonth));
        }

        if (rule.BySetPos is { Length: > 0 })
        {
            sb.Append(";BYSETPOS=");
            sb.Append(string.Join(",", rule.BySetPos));
        }

        // WKST is only emitted when it differs from the RFC 5545 default (Monday), keeping existing
        // RRULE strings byte-for-byte identical.
        if (rule.WeekStart != DayOfWeek.Monday)
        {
            sb.Append(";WKST=");
            sb.Append(ReverseDayMap[rule.WeekStart]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Expand a recurring event into individual occurrence instances within the given range.
    /// Occurrences preserve the source wall-clock time; when a <paramref name="timeZone"/> is supplied
    /// each occurrence is stamped with that zone's UTC offset for its own date, so recurrences stay at
    /// the same local time across DST transitions.
    /// </summary>
    public static IReadOnlyList<TmScheduleEvent> ExpandRecurrence(
        TmScheduleEvent source, DateTime rangeStart, DateTime rangeEnd, TimeZoneInfo? timeZone = null)
    {
        if (string.IsNullOrWhiteSpace(source.RecurrenceRule))
            return [];

        var rule = Parse(source.RecurrenceRule);
        if (rule is null) return [];

        var zone = timeZone ?? TimeZoneInfo.Local;
        var duration = source.End - source.Start;
        var exceptions = source.RecurrenceExceptions?
            .Select(d => d.Date)
            .ToHashSet() ?? [];

        var occurrences = new List<TmScheduleEvent>();
        var count = 0;
        var seriesStart = source.Start.DateTime;
        var current = seriesStart;

        while (current < rangeEnd && count < MaxExpansions)
        {
            if (rule.Count.HasValue && count >= rule.Count.Value)
                break;

            if (rule.Until.HasValue && IsAfterUntil(current, rule.Until.Value, zone))
                break;

            var candidates = GetCandidatesForDate(current, rule);

            foreach (var candidate in candidates)
            {
                if (candidate < seriesStart) continue; // occurrences never precede DTSTART
                if (candidate >= rangeEnd) break;
                if (rule.Count.HasValue && occurrences.Count >= rule.Count.Value) break;
                if (rule.Until.HasValue && IsAfterUntil(candidate, rule.Until.Value, zone)) break;

                if (candidate >= rangeStart && !exceptions.Contains(candidate.Date))
                {
                    occurrences.Add(CreateOccurrence(source, candidate, duration, zone));
                }

                count++;
            }

            current = Advance(current, rule);
        }

        return occurrences;
    }

    /// <summary>
    /// Determines whether an occurrence at the given wall-clock time falls after the recurrence's
    /// <c>UNTIL</c> boundary, comparing at day granularity but normalized into the event's target
    /// timezone so a DST offset shift cannot push the boundary ±1 day.
    /// </summary>
    /// <remarks>
    /// A UTC <c>UNTIL</c> (RFC 5545 DATE-TIME, ending in <c>Z</c>) is compared as UTC calendar dates:
    /// the occurrence is projected to its UTC instant using its own per-occurrence offset (matching
    /// <see cref="CreateOccurrence"/>), so an event that keeps its local wall-clock time across a DST
    /// transition is still bounded correctly. A floating / date-only <c>UNTIL</c> carries no timezone
    /// and is compared directly against the occurrence's wall-clock date.
    /// </remarks>
    private static bool IsAfterUntil(DateTime wallClock, DateTime until, TimeZoneInfo zone)
    {
        if (until.Kind == DateTimeKind.Utc)
        {
            var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
            var occurrenceUtc = new DateTimeOffset(local, zone.GetUtcOffset(local)).UtcDateTime;
            return occurrenceUtc.Date > until.Date;
        }

        return wallClock.Date > until.Date;
    }

    private static List<DateTime> GetCandidatesForDate(DateTime current, TmRecurrenceRule rule)
    {
        var candidates = new List<DateTime>();

        switch (rule.Frequency)
        {
            case TmRecurrenceFrequency.Daily:
                candidates.Add(current);
                break;

            case TmRecurrenceFrequency.Weekly:
                if (rule.ByDay is { Length: > 0 })
                {
                    // Anchor the week on WKST (RFC 5545). The default (Monday) reduces to the original
                    // ((int)d + 6) % 7 math, so behavior is unchanged when WKST is absent. WKST only
                    // changes results for INTERVAL > 1 with multiple BYDAY days that straddle the
                    // week boundary — e.g. WKST=SU vs MO groups SU/SA into different weeks.
                    var wkst = (int)rule.WeekStart;
                    var daysSinceWeekStart = ((int)current.DayOfWeek - wkst + 7) % 7;
                    var weekStart = current.Date.AddDays(-daysSinceWeekStart);
                    foreach (var day in rule.ByDay.OrderBy(d => ((int)d - wkst + 7) % 7))
                    {
                        var offset = ((int)day - wkst + 7) % 7;
                        // All BYDAY weekdays in the week; the DTSTART guard in ExpandRecurrence
                        // trims those before the series start (only the first week).
                        candidates.Add(weekStart.AddDays(offset).Add(current.TimeOfDay));
                    }
                }
                else
                {
                    candidates.Add(current);
                }
                break;

            case TmRecurrenceFrequency.Monthly:
                candidates.AddRange(MonthlyCandidates(current, current.Month, rule));
                break;

            case TmRecurrenceFrequency.Yearly:
                if (rule.ByMonth is { Length: > 0 })
                {
                    foreach (var month in rule.ByMonth.OrderBy(m => m))
                    {
                        candidates.AddRange(MonthlyCandidates(current, month, rule));
                    }
                }
                else if (rule.ByDayPositional is { Length: > 0 })
                {
                    candidates.AddRange(MonthlyCandidates(current, current.Month, rule));
                }
                else
                {
                    candidates.Add(current);
                }
                break;
        }

        candidates = candidates.OrderBy(c => c).ToList();

        if (rule.BySetPos is { Length: > 0 } && candidates.Count > 0)
        {
            candidates = ApplyBySetPos(candidates, rule.BySetPos);
        }

        return candidates;
    }

    private static IEnumerable<DateTime> MonthlyCandidates(DateTime current, int month, TmRecurrenceRule rule)
    {
        var time = new TimeOnly(current.Hour, current.Minute, current.Second);

        if (rule.ByDayPositional is { Length: > 0 })
        {
            foreach (var (ordinal, day) in rule.ByDayPositional)
            {
                var dt = NthWeekdayOfMonth(current.Year, month, day, ordinal, time);
                if (dt.HasValue) yield return dt.Value;
            }
        }
        else if (rule.ByDay is { Length: > 0 })
        {
            // All matching weekdays in the month (typically combined with BYSETPOS).
            var set = new HashSet<DayOfWeek>(rule.ByDay);
            var daysInMonth = DateTime.DaysInMonth(current.Year, month);
            for (var d = 1; d <= daysInMonth; d++)
            {
                var candidate = new DateTime(current.Year, month, d, time.Hour, time.Minute, time.Second);
                if (set.Contains(candidate.DayOfWeek)) yield return candidate;
            }
        }
        else if (rule.ByMonthDay is { Length: > 0 })
        {
            var daysInMonth = DateTime.DaysInMonth(current.Year, month);
            foreach (var day in rule.ByMonthDay.OrderBy(d => d))
            {
                if (day <= daysInMonth)
                    yield return new DateTime(current.Year, month, day, time.Hour, time.Minute, time.Second);
            }
        }
        else
        {
            var dayInMonth = Math.Min(current.Day, DateTime.DaysInMonth(current.Year, month));
            yield return new DateTime(current.Year, month, dayInMonth, time.Hour, time.Minute, time.Second);
        }
    }

    private static DateTime? NthWeekdayOfMonth(int year, int month, DayOfWeek day, int ordinal, TimeOnly time)
    {
        var matches = new List<int>();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        for (var d = 1; d <= daysInMonth; d++)
        {
            if (new DateTime(year, month, d).DayOfWeek == day) matches.Add(d);
        }

        if (matches.Count == 0) return null;

        var index = ordinal > 0 ? ordinal - 1 : matches.Count + ordinal;
        if (index < 0 || index >= matches.Count) return null;

        return new DateTime(year, month, matches[index], time.Hour, time.Minute, time.Second);
    }

    private static List<DateTime> ApplyBySetPos(List<DateTime> candidates, int[] setPos)
    {
        var result = new List<DateTime>();
        foreach (var pos in setPos)
        {
            var index = pos > 0 ? pos - 1 : candidates.Count + pos;
            if (index >= 0 && index < candidates.Count) result.Add(candidates[index]);
        }

        return result.Distinct().OrderBy(c => c).ToList();
    }

    private static DateTime Advance(DateTime current, TmRecurrenceRule rule)
    {
        return rule.Frequency switch
        {
            TmRecurrenceFrequency.Daily => current.AddDays(rule.Interval),
            TmRecurrenceFrequency.Weekly => current.AddDays(7 * rule.Interval),
            TmRecurrenceFrequency.Monthly => current.AddMonths(rule.Interval),
            TmRecurrenceFrequency.Yearly => current.AddYears(rule.Interval),
            _ => current.AddDays(1)
        };
    }

    private static TmScheduleEvent CreateOccurrence(TmScheduleEvent source, DateTime start, TimeSpan duration, TimeZoneInfo zone)
    {
        var startOffset = new DateTimeOffset(
            DateTime.SpecifyKind(start, DateTimeKind.Unspecified),
            zone.GetUtcOffset(DateTime.SpecifyKind(start, DateTimeKind.Unspecified)));

        return new TmScheduleEvent
        {
            Id = $"{source.Id}_{start:yyyyMMdd}",
            Title = source.Title,
            Description = source.Description,
            Start = startOffset,
            End = startOffset + duration,
            AllDay = source.AllDay,
            Color = source.Color,
            CssClass = source.CssClass,
            ResourceId = source.ResourceId,
            RecurrenceRule = source.RecurrenceRule,
            IsReadOnly = source.IsReadOnly,
            Metadata = source.Metadata,
        };
    }
}
