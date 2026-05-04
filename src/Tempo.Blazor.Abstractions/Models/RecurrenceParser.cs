using System.Globalization;
using System.Text;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Parses and generates iCal RRULE strings for <see cref="RecurrenceRule"/>.
/// </summary>
public static class RecurrenceParser
{
    private static readonly string[] DayNames = ["SU", "MO", "TU", "WE", "TH", "FR", "SA"];

    /// <summary>Converts a <see cref="RecurrenceRule"/> to an iCal RRULE string.</summary>
    public static string ToRRule(RecurrenceRule rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));

        var sb = new StringBuilder();
        sb.Append("FREQ=");
        sb.Append(rule.Pattern switch
        {
            RecurrencePattern.Daily => "DAILY",
            RecurrencePattern.Weekly => "WEEKLY",
            RecurrencePattern.Monthly => "MONTHLY",
            RecurrencePattern.Yearly => "YEARLY",
            _ => "DAILY"
        });

        if (rule.Interval > 1)
        {
            sb.Append($";INTERVAL={rule.Interval}");
        }

        if (rule.Pattern == RecurrencePattern.Weekly && rule.DaysOfWeek.Count > 0)
        {
            var days = rule.DaysOfWeek.Select(d => DayNames[Math.Clamp(d, 0, 6)]);
            sb.Append($";BYDAY={string.Join(",", days)}");
        }

        if (rule.Pattern == RecurrencePattern.Monthly)
        {
            if (rule.Position.HasValue && rule.DaysOfWeek.Count > 0)
            {
                var pos = rule.Position.Value;
                var prefix = pos > 0 ? $"+{pos}" : pos.ToString();
                var days = rule.DaysOfWeek.Select(d => $"{prefix}{DayNames[Math.Clamp(d, 0, 6)]}");
                sb.Append($";BYDAY={string.Join(",", days)}");
            }
            else if (rule.DayOfMonth.HasValue)
            {
                sb.Append($";BYMONTHDAY={rule.DayOfMonth.Value}");
            }
        }

        if (rule.Pattern == RecurrencePattern.Yearly)
        {
            if (rule.MonthOfYear.HasValue)
                sb.Append($";BYMONTH={rule.MonthOfYear.Value}");
            if (rule.DayOfMonth.HasValue)
                sb.Append($";BYMONTHDAY={rule.DayOfMonth.Value}");
        }

        if (rule.EndAfter is int count)
        {
            sb.Append($";COUNT={count}");
        }
        else if (rule.EndAfter is DateTime until)
        {
            sb.Append($";UNTIL={until:yyyyMMddTHHmmssZ}");
        }

        return sb.ToString();
    }

    /// <summary>Parses an iCal RRULE string into a <see cref="RecurrenceRule"/>.</summary>
    public static RecurrenceRule FromRRule(string rrule, DateTime? startDate = null)
    {
        if (string.IsNullOrWhiteSpace(rrule))
            return new RecurrenceRule { StartDate = startDate ?? DateTime.Today };

        var rule = new RecurrenceRule { StartDate = startDate ?? DateTime.Today };
        var parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq].Trim().ToUpperInvariant();
            var value = part[(eq + 1)..].Trim();

            switch (key)
            {
                case "FREQ":
                    rule.Pattern = value switch
                    {
                        "DAILY" => RecurrencePattern.Daily,
                        "WEEKLY" => RecurrencePattern.Weekly,
                        "MONTHLY" => RecurrencePattern.Monthly,
                        "YEARLY" => RecurrencePattern.Yearly,
                        _ => RecurrencePattern.Daily
                    };
                    break;
                case "INTERVAL":
                    if (int.TryParse(value, out var interval))
                        rule.Interval = interval;
                    break;
                case "BYDAY":
                    ParseByDay(rule, value);
                    break;
                case "BYMONTHDAY":
                    if (int.TryParse(value, out var monthDay))
                        rule.DayOfMonth = monthDay;
                    break;
                case "BYMONTH":
                    if (int.TryParse(value, out var month))
                        rule.MonthOfYear = month;
                    break;
                case "COUNT":
                    if (int.TryParse(value, out var count))
                        rule.EndAfter = count;
                    break;
                case "UNTIL":
                    if (DateTime.TryParseExact(value, ["yyyyMMddTHHmmssZ", "yyyyMMddTHHmmss", "yyyyMMdd"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var until))
                        rule.EndAfter = until;
                    break;
            }
        }

        return rule;
    }

    private static void ParseByDay(RecurrenceRule rule, string value)
    {
        var days = new List<int>();
        int? position = null;

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = part.Trim();
            if (p.Length < 2) continue;

            // Check for position prefix like +1MO, -1FR, 2TU
            var dayStart = 0;
            if (char.IsDigit(p[0]) || p[0] == '+' || p[0] == '-')
            {
                var numEnd = 1;
                while (numEnd < p.Length && char.IsDigit(p[numEnd])) numEnd++;
                if (int.TryParse(p[..numEnd], out var pos))
                    position = pos;
                dayStart = numEnd;
            }

            var dayCode = p[dayStart..];
            var dayIndex = Array.IndexOf(DayNames, dayCode.ToUpperInvariant());
            if (dayIndex >= 0)
                days.Add(dayIndex);
        }

        rule.DaysOfWeek = days;
        if (position.HasValue)
            rule.Position = position.Value;
    }
}
