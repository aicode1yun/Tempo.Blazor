using System.Globalization;
using System.Text;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Scheduler;

/// <summary>
/// Dependency-free iCalendar (RFC 5545) exporter/importer for scheduler events. Timed events are
/// emitted as UTC instants; all-day events use <c>VALUE=DATE</c>. Recurrence maps to <c>RRULE</c>
/// and exception dates to <c>EXDATE</c>.
/// </summary>
public sealed class IcsCalendarSerializer : IScheduleExporter, IScheduleImporter
{
    /// <inheritdoc />
    public string ContentType => "text/calendar";

    /// <inheritdoc />
    public string FileExtension => "ics";

    /// <inheritdoc />
    public string Export(IEnumerable<TmScheduleEvent> events, string? calendarName = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        var sb = new StringBuilder();
        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, "PRODID:-//Tempo.Blazor//Scheduler//EN");
        AppendLine(sb, "CALSCALE:GREGORIAN");
        if (!string.IsNullOrWhiteSpace(calendarName))
        {
            AppendLine(sb, "X-WR-CALNAME:" + Escape(calendarName));
        }

        var stamp = FormatUtc(DateTimeOffset.UtcNow);
        foreach (var e in events)
        {
            AppendLine(sb, "BEGIN:VEVENT");
            AppendLine(sb, "UID:" + (string.IsNullOrWhiteSpace(e.Id) ? Guid.NewGuid().ToString("N") : e.Id));
            AppendLine(sb, "DTSTAMP:" + stamp);

            if (e.AllDay)
            {
                AppendLine(sb, "DTSTART;VALUE=DATE:" + e.Start.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                AppendLine(sb, "DTEND;VALUE=DATE:" + e.End.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }
            else
            {
                AppendLine(sb, "DTSTART:" + FormatUtc(e.Start));
                AppendLine(sb, "DTEND:" + FormatUtc(e.End));
            }

            AppendLine(sb, "SUMMARY:" + Escape(e.Title));
            if (!string.IsNullOrWhiteSpace(e.Description))
            {
                AppendLine(sb, "DESCRIPTION:" + Escape(e.Description));
            }

            if (!string.IsNullOrWhiteSpace(e.RecurrenceRule))
            {
                AppendLine(sb, "RRULE:" + e.RecurrenceRule);
            }

            if (e.RecurrenceExceptions is { Count: > 0 })
            {
                AppendLine(sb, "EXDATE:" + string.Join(",", e.RecurrenceExceptions
                    .Select(d => d.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture))));
            }

            AppendLine(sb, "END:VEVENT");
        }

        AppendLine(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    /// <inheritdoc />
    public IReadOnlyList<TmScheduleEvent> Import(string content)
    {
        var events = new List<TmScheduleEvent>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return events;
        }

        TmScheduleEvent? current = null;
        var hasEnd = false;
        TimeSpan? duration = null;
        foreach (var line in Unfold(content))
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new TmScheduleEvent();
                hasEnd = false;
                duration = null;
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                {
                    if (!hasEnd)
                    {
                        // DTEND is optional (RFC 5545): fall back to DURATION, or a same-instant /
                        // next-day (all-day) default so End is never left at DateTimeOffset.MinValue.
                        current.End = duration.HasValue
                            ? current.Start + duration.Value
                            : (current.AllDay ? current.Start.AddDays(1) : current.Start);
                    }

                    events.Add(current);
                }

                current = null;
                continue;
            }

            if (current is null) continue;

            var (name, parameters, value) = SplitLine(line);
            switch (name)
            {
                case "UID": current.Id = value; break;
                case "SUMMARY": current.Title = Unescape(value); break;
                case "DESCRIPTION": current.Description = Unescape(value); break;
                case "DTSTART":
                    var (start, allDay) = ParseDate(value, parameters);
                    current.Start = start;
                    if (allDay) current.AllDay = true;
                    break;
                case "DTEND":
                    current.End = ParseDate(value, parameters).Value;
                    hasEnd = true;
                    break;
                case "DURATION":
                    duration = ParseDuration(value);
                    break;
                case "RRULE": current.RecurrenceRule = value; break;
                case "EXDATE": current.RecurrenceExceptions = ParseExDates(value); break;
            }
        }

        return events;
    }

    // ── Formatting ────────────────────────────────────────────────

    private static string FormatUtc(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static void AppendLine(StringBuilder sb, string line)
    {
        const int max = 75;
        if (line.Length <= max)
        {
            sb.Append(line).Append("\r\n");
            return;
        }

        sb.Append(line, 0, max).Append("\r\n");
        var index = max;
        while (index < line.Length)
        {
            var length = Math.Min(max - 1, line.Length - index);
            sb.Append(' ').Append(line, index, length).Append("\r\n");
            index += length;
        }
    }

    private static string Escape(string? value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Unescape(string value)
    {
        // Single left-to-right pass: a sequential set of Replace calls corrupts an escaped backslash
        // followed by n/,/; (e.g. "C:\\notes" would wrongly unescape "\n").
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[++i];
                sb.Append(next switch
                {
                    'n' or 'N' => '\n',
                    '\\' => '\\',
                    ',' => ',',
                    ';' => ';',
                    _ => next
                });
            }
            else
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }

    // ── Parsing ───────────────────────────────────────────────────

    private static List<string> Unfold(string content)
    {
        var raw = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var result = new List<string>();
        foreach (var line in raw)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && result.Count > 0)
            {
                result[^1] += line[1..];
            }
            else if (line.Length > 0)
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static (string Name, Dictionary<string, string> Parameters, string Value) SplitLine(string line)
    {
        var colon = line.IndexOf(':');
        var head = colon < 0 ? line : line[..colon];
        var value = colon < 0 ? string.Empty : line[(colon + 1)..];

        var parts = head.Split(';');
        var name = parts[0].Trim().ToUpperInvariant();
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < parts.Length; i++)
        {
            var eq = parts[i].IndexOf('=');
            if (eq > 0) parameters[parts[i][..eq]] = parts[i][(eq + 1)..];
        }

        return (name, parameters, value);
    }

    private static (DateTimeOffset Value, bool AllDay) ParseDate(string value, Dictionary<string, string> parameters)
    {
        value = value.Trim();

        var isDate = (parameters.TryGetValue("VALUE", out var v) && string.Equals(v, "DATE", StringComparison.OrdinalIgnoreCase))
                     || value.Length == 8;
        if (isDate && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return (new DateTimeOffset(date, TimeSpan.Zero), true);
        }

        if (value.EndsWith('Z')
            && DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            return (new DateTimeOffset(utc, TimeSpan.Zero), false);
        }

        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return (new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)), false);
        }

        return (default, false);
    }

    private static TimeSpan? ParseDuration(string value)
    {
        value = value.Trim();

        // ISO 8601 week form (PnW) — not valid xs:duration, handle it explicitly.
        var week = System.Text.RegularExpressions.Regex.Match(value, @"^([+-]?)P(\d+)W$");
        if (week.Success)
        {
            var span = TimeSpan.FromDays(int.Parse(week.Groups[2].Value, CultureInfo.InvariantCulture) * 7);
            return week.Groups[1].Value == "-" ? -span : span;
        }

        try { return System.Xml.XmlConvert.ToTimeSpan(value); }
        catch { return null; }
    }

    private static List<DateTime> ParseExDates(string value)
    {
        var result = new List<DateTime>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim().TrimEnd('Z');
            if (DateTime.TryParseExact(token, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                || DateTime.TryParseExact(token, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                result.Add(dt);
            }
        }

        return result;
    }
}
