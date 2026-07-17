namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Unit of a deadline rule step.</summary>
public enum DeadlineUnit
{
    /// <summary>Calendar days.</summary>
    Days = 0,

    /// <summary>Business days (weekends and holidays are skipped while counting).</summary>
    BusinessDays = 1,

    /// <summary>Calendar weeks (seven days each).</summary>
    Weeks = 2,

    /// <summary>Calendar months (end-of-month dates clamp to the target month's length).</summary>
    Months = 3,

    /// <summary>Calendar years (Feb 29 clamps to Feb 28 in non-leap years).</summary>
    Years = 4
}

/// <summary>How a deadline landing on a weekend or holiday is shifted.</summary>
public enum DeadlineNonBusinessShift
{
    /// <summary>Keep the date even when it is not a business day.</summary>
    None = 0,

    /// <summary>Move forward to the next business day.</summary>
    NextBusinessDay = 1,

    /// <summary>Move backward to the previous business day.</summary>
    PreviousBusinessDay = 2
}

/// <summary>One step of a deadline rule. Steps chain: each starts from the previous result.</summary>
public sealed class DeadlineRuleStep
{
    /// <summary>Number of units to add. Negative counts backwards.</summary>
    public int Amount { get; set; }

    /// <summary>Unit of the step. Default is <see cref="DeadlineUnit.Days"/>.</summary>
    public DeadlineUnit Unit { get; set; } = DeadlineUnit.Days;

    /// <summary>Shift applied when the step result is not a business day.
    /// Default is <see cref="DeadlineNonBusinessShift.NextBusinessDay"/>.</summary>
    public DeadlineNonBusinessShift NonBusinessShift { get; set; } = DeadlineNonBusinessShift.NextBusinessDay;

    /// <summary>Optional display label of the step.</summary>
    public string? Label { get; set; }
}

/// <summary>Deadline computation rule: an ordered chain of steps.</summary>
public sealed class DeadlineRule
{
    /// <summary>Optional display name of the rule.</summary>
    public string? Name { get; set; }

    /// <summary>Ordered rule steps.</summary>
    public List<DeadlineRuleStep> Steps { get; set; } = [];

    /// <summary>Creates a single-step rule.</summary>
    /// <param name="amount">Number of units to add.</param>
    /// <param name="unit">Unit of the step.</param>
    /// <param name="shift">Shift applied when the result is not a business day.</param>
    public static DeadlineRule Single(
        int amount,
        DeadlineUnit unit = DeadlineUnit.Days,
        DeadlineNonBusinessShift shift = DeadlineNonBusinessShift.NextBusinessDay)
        => new()
        {
            Steps = [new DeadlineRuleStep { Amount = amount, Unit = unit, NonBusinessShift = shift }]
        };
}

/// <summary>Holiday entry of a holiday calendar.</summary>
public sealed class DeadlineHoliday
{
    /// <summary>Holiday date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Displayed holiday name.</summary>
    public string? Name { get; set; }
}

/// <summary>Supplies holiday calendars (typically per country) to deadline calculations.</summary>
public interface IHolidayProvider
{
    /// <summary>Returns the holidays of one calendar year.</summary>
    /// <param name="year">Calendar year.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<DeadlineHoliday>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default);
}

/// <summary>In-memory <see cref="IHolidayProvider"/> over a fixed holiday list.</summary>
public sealed class InMemoryHolidayProvider : IHolidayProvider
{
    private readonly List<DeadlineHoliday> _holidays;

    /// <summary>Creates a provider over the given holidays.</summary>
    /// <param name="holidays">Holiday entries.</param>
    public InMemoryHolidayProvider(IEnumerable<DeadlineHoliday> holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);
        _holidays = [.. holidays];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadlineHoliday>> GetHolidaysAsync(int year, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeadlineHoliday>>(
            _holidays.Where(h => h.Date.Year == year).ToList());
}

/// <summary>
/// Materialized business-day context used by the pure deadline engine: a weekend
/// definition plus a holiday lookup. Build directly from holidays or load the needed
/// year range from an <see cref="IHolidayProvider"/> via <see cref="LoadAsync"/>.
/// </summary>
public sealed class DeadlineCalendar
{
    private static readonly DayOfWeek[] _defaultWeekend = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    private readonly Dictionary<DateOnly, string?> _holidays = [];
    private readonly HashSet<DayOfWeek> _weekend;

    /// <summary>Creates a calendar.</summary>
    /// <param name="holidays">Holidays, when any.</param>
    /// <param name="weekend">Weekend days. Default is Saturday and Sunday.</param>
    public DeadlineCalendar(IEnumerable<DeadlineHoliday>? holidays = null, IEnumerable<DayOfWeek>? weekend = null)
    {
        _weekend = [.. weekend ?? _defaultWeekend];
        foreach (var holiday in holidays ?? [])
        {
            _holidays[holiday.Date] = holiday.Name;
        }
    }

    /// <summary>Returns true when the date falls on a weekend day.</summary>
    /// <param name="date">Date to test.</param>
    public bool IsWeekend(DateOnly date) => _weekend.Contains(date.DayOfWeek);

    /// <summary>Returns true and the holiday name when the date is a holiday.</summary>
    /// <param name="date">Date to test.</param>
    /// <param name="name">Holiday name, when found.</param>
    public bool TryGetHoliday(DateOnly date, out string? name) => _holidays.TryGetValue(date, out name);

    /// <summary>Returns true when the date is neither a weekend day nor a holiday.</summary>
    /// <param name="date">Date to test.</param>
    public bool IsBusinessDay(DateOnly date) => !IsWeekend(date) && !_holidays.ContainsKey(date);

    /// <summary>Loads the holidays of a year range from a provider into a calendar.</summary>
    /// <param name="provider">Holiday provider; null yields a weekends-only calendar.</param>
    /// <param name="fromYear">First year to load (inclusive).</param>
    /// <param name="toYear">Last year to load (inclusive).</param>
    /// <param name="weekend">Weekend days. Default is Saturday and Sunday.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task<DeadlineCalendar> LoadAsync(
        IHolidayProvider? provider,
        int fromYear,
        int toYear,
        IEnumerable<DayOfWeek>? weekend = null,
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
        {
            return new DeadlineCalendar(null, weekend);
        }

        var holidays = new List<DeadlineHoliday>();
        for (var year = fromYear; year <= toYear; year++)
        {
            holidays.AddRange(await provider.GetHolidaysAsync(year, cancellationToken));
        }

        return new DeadlineCalendar(holidays, weekend);
    }
}

/// <summary>Kind of a deadline protocol entry.</summary>
public enum DeadlineStepKind
{
    /// <summary>Base date of the calculation.</summary>
    Start = 0,

    /// <summary>Units of one rule step were added.</summary>
    AddUnits = 1,

    /// <summary>The date was moved off a weekend day.</summary>
    ShiftedFromWeekend = 2,

    /// <summary>The date was moved off a holiday.</summary>
    ShiftedFromHoliday = 3,

    /// <summary>Final deadline.</summary>
    Final = 4
}

/// <summary>One entry of the step-by-step deadline protocol.</summary>
public sealed class DeadlineProtocolEntry
{
    /// <summary>Entry kind.</summary>
    public DeadlineStepKind Kind { get; set; }

    /// <summary>Zero-based rule step index, or -1 for Start/Final entries.</summary>
    public int RuleStepIndex { get; set; } = -1;

    /// <summary>Date after this entry was applied.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Units added, for <see cref="DeadlineStepKind.AddUnits"/>.</summary>
    public int Amount { get; set; }

    /// <summary>Unit added, for <see cref="DeadlineStepKind.AddUnits"/>.</summary>
    public DeadlineUnit Unit { get; set; }

    /// <summary>Name of the holiday shifted from, for <see cref="DeadlineStepKind.ShiftedFromHoliday"/>.</summary>
    public string? HolidayName { get; set; }

    /// <summary>Optional label of the originating rule step.</summary>
    public string? StepLabel { get; set; }
}

/// <summary>Result of a deadline calculation: the deadline plus the step protocol.</summary>
public sealed class DeadlineResult
{
    /// <summary>Base date the calculation started from.</summary>
    public DateOnly BaseDate { get; set; }

    /// <summary>Computed deadline.</summary>
    public DateOnly Deadline { get; set; }

    /// <summary>Step-by-step protocol of the calculation.</summary>
    public IReadOnlyList<DeadlineProtocolEntry> Protocol { get; set; } = [];
}

/// <summary>
/// Pure deadline rule engine: applies a chained rule to a base date over a
/// business-day calendar and records a step-by-step protocol. All arithmetic is
/// calendar-based (<see cref="DateOnly"/>), so DST transitions cannot skew results.
/// </summary>
public static class DeadlineCalculator
{
    /// <summary>Upper bound of a step amount (roughly one century of days).</summary>
    public const int MaxStepAmount = 36_500;

    // Longest run of consecutive non-business days the engine tolerates before it
    // concludes the calendar has no business days at all (a leap-year of them, with slack).
    private const int MaxNonBusinessRun = 400;

    /// <summary>Calculates the deadline for a base date and rule.</summary>
    /// <param name="baseDate">Date the periods count from.</param>
    /// <param name="rule">Rule to apply. Step amounts must stay within ±<see cref="MaxStepAmount"/>.</param>
    /// <param name="calendar">Business-day calendar. Defaults to Saturday/Sunday weekends without holidays.</param>
    /// <exception cref="ArgumentException">A step amount exceeds ±<see cref="MaxStepAmount"/>.</exception>
    /// <exception cref="InvalidOperationException">The calendar contains no business days to shift to.</exception>
    public static DeadlineResult Calculate(DateOnly baseDate, DeadlineRule rule, DeadlineCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.Steps.Any(step => Math.Abs(step.Amount) > MaxStepAmount))
        {
            throw new ArgumentException($"Step amounts must stay within ±{MaxStepAmount}.", nameof(rule));
        }

        calendar ??= new DeadlineCalendar();

        var protocol = new List<DeadlineProtocolEntry>
        {
            new() { Kind = DeadlineStepKind.Start, Date = baseDate }
        };

        var date = baseDate;
        for (var index = 0; index < rule.Steps.Count; index++)
        {
            var step = rule.Steps[index];
            date = AddUnits(date, step, calendar);
            protocol.Add(new DeadlineProtocolEntry
            {
                Kind = DeadlineStepKind.AddUnits,
                RuleStepIndex = index,
                Date = date,
                Amount = step.Amount,
                Unit = step.Unit,
                StepLabel = step.Label
            });

            date = ApplyShift(date, step.NonBusinessShift, calendar, index, protocol);
        }

        protocol.Add(new DeadlineProtocolEntry { Kind = DeadlineStepKind.Final, Date = date });

        return new DeadlineResult
        {
            BaseDate = baseDate,
            Deadline = date,
            Protocol = protocol
        };
    }

    private static DateOnly AddUnits(DateOnly date, DeadlineRuleStep step, DeadlineCalendar calendar)
        => step.Unit switch
        {
            DeadlineUnit.BusinessDays => AddBusinessDays(date, step.Amount, calendar),
            DeadlineUnit.Weeks => date.AddDays(step.Amount * 7),
            DeadlineUnit.Months => date.AddMonths(step.Amount),
            DeadlineUnit.Years => date.AddYears(step.Amount),
            _ => date.AddDays(step.Amount)
        };

    private static DateOnly AddBusinessDays(DateOnly date, int amount, DeadlineCalendar calendar)
    {
        var direction = amount >= 0 ? 1 : -1;
        var remaining = Math.Abs(amount);
        var nonBusinessRun = 0;
        while (remaining > 0)
        {
            date = date.AddDays(direction);
            if (calendar.IsBusinessDay(date))
            {
                remaining--;
                nonBusinessRun = 0;
            }
            else if (++nonBusinessRun > MaxNonBusinessRun)
            {
                throw new InvalidOperationException("The calendar contains no business days to count.");
            }
        }

        return date;
    }

    private static DateOnly ApplyShift(
        DateOnly date,
        DeadlineNonBusinessShift shift,
        DeadlineCalendar calendar,
        int stepIndex,
        List<DeadlineProtocolEntry> protocol)
    {
        if (shift == DeadlineNonBusinessShift.None)
        {
            return date;
        }

        var direction = shift == DeadlineNonBusinessShift.PreviousBusinessDay ? -1 : 1;
        var shifted = 0;
        while (!calendar.IsBusinessDay(date))
        {
            if (++shifted > MaxNonBusinessRun)
            {
                throw new InvalidOperationException("The calendar contains no business days to shift to.");
            }

            var isHoliday = calendar.TryGetHoliday(date, out var holidayName);
            date = date.AddDays(direction);
            protocol.Add(new DeadlineProtocolEntry
            {
                Kind = isHoliday ? DeadlineStepKind.ShiftedFromHoliday : DeadlineStepKind.ShiftedFromWeekend,
                RuleStepIndex = stepIndex,
                Date = date,
                HolidayName = holidayName
            });
        }

        return date;
    }
}
