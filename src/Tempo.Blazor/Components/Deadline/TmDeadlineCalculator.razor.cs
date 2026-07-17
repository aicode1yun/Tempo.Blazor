using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Deadline;

/// <summary>
/// Rule-based deadline calculator widget. Computes a deadline from a base date and a
/// chained rule (calendar/business units with weekend/holiday shifts) through the pure
/// <see cref="DeadlineCalculator"/> engine, showing a live result and a step-by-step
/// protocol. Holiday calendars are pluggable via <see cref="IHolidayProvider"/>; an
/// embed mode renders only the result.
/// </summary>
public partial class TmDeadlineCalculator : TmComponentBase
{
    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>Base date the periods count from. Defaults to today.</summary>
    [Parameter] public DateOnly? BaseDate { get; set; }

    /// <summary>Callback invoked when the base date changes in the form.</summary>
    [Parameter] public EventCallback<DateOnly> BaseDateChanged { get; set; }

    /// <summary>Initial deadline rule. The component edits its own copy.</summary>
    [Parameter] public DeadlineRule? Rule { get; set; }

    /// <summary>Callback invoked when the edited rule changes.</summary>
    [Parameter] public EventCallback<DeadlineRule> RuleChanged { get; set; }

    /// <summary>Holiday calendar source, when any.</summary>
    [Parameter] public IHolidayProvider? HolidayProvider { get; set; }

    /// <summary>Weekend days. Default is Saturday and Sunday.</summary>
    [Parameter] public IReadOnlyList<DayOfWeek>? Weekend { get; set; }

    /// <summary>Whether the rule form is rendered. False gives the embed mode. Default is true.</summary>
    [Parameter] public bool ShowForm { get; set; } = true;

    /// <summary>Whether the step protocol is rendered. Default is true.</summary>
    [Parameter] public bool ShowProtocol { get; set; } = true;

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Callback invoked with every recalculated result.</summary>
    [Parameter] public EventCallback<DeadlineResult> OnCalculated { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private const int MaxAmount = 3650;

    private DateOnly _baseDate = DateOnly.FromDateTime(DateTime.Today);
    private List<DeadlineRuleStep> _steps = [new DeadlineRuleStep { Amount = 15 }];
    private DeadlineResult? _result;
    private string? _errorKey;
    private DateOnly? _lastBaseDateParameter;
    private DeadlineRule? _lastRuleParameter;
    private IHolidayProvider? _lastProvider;
    private DeadlineCalendar? _calendarCache;
    private (int From, int To)? _calendarYears;
    private int _calcGeneration;

    /// <summary>Latest calculation result, when available.</summary>
    public DeadlineResult? Result => _result;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var changed = false;

        if (BaseDate is not null && BaseDate != _lastBaseDateParameter)
        {
            _lastBaseDateParameter = BaseDate;
            _baseDate = BaseDate.Value;
            changed = true;
        }

        if (!ReferenceEquals(Rule, _lastRuleParameter))
        {
            _lastRuleParameter = Rule;
            _steps = Rule is { Steps.Count: > 0 }
                ? Rule.Steps.Select(CloneStep).ToList()
                : [new DeadlineRuleStep { Amount = 15 }];
            changed = true;
        }

        if (!ReferenceEquals(HolidayProvider, _lastProvider))
        {
            _lastProvider = HolidayProvider;
            _calendarCache = null;
            _calendarYears = null;
            changed = true;
        }

        if (changed || _result is null)
        {
            await RecalculateAsync();
        }
    }

    private static DeadlineRuleStep CloneStep(DeadlineRuleStep step)
        => new()
        {
            Amount = step.Amount,
            Unit = step.Unit,
            NonBusinessShift = step.NonBusinessShift,
            Label = step.Label
        };

    // ── Calculation ──────────────────────────────────────────────────────────

    private DeadlineRule BuildRule()
        => new() { Steps = _steps.Select(CloneStep).ToList() };

    /// <summary>Recalculates the deadline from the current form state.</summary>
    public async Task RecalculateAsync()
    {
        // Every entry claims a new generation so an in-flight calculation for an older
        // form state can never publish its result — including over a validation error.
        var generation = ++_calcGeneration;

        if (_steps.Any(step => step.Amount == 0))
        {
            _errorKey = "TmDeadlineCalculator_ErrorAmountZero";
            await InvokeAsync(StateHasChanged);
            return;
        }

        _errorKey = null;
        var rule = BuildRule();

        try
        {
            if (HolidayProvider is null)
            {
                _result = DeadlineCalculator.Calculate(_baseDate, rule, new DeadlineCalendar(null, Weekend));
                await OnCalculated.InvokeAsync(_result);
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Pass 1 estimates the affected year span with a weekends-only calendar, so the
            // provider is asked exactly for the years the calculation can touch. Holiday
            // skips can push the real deadline further out, so extend the range and retry
            // until the result stays inside the loaded years.
            var weekendOnly = new DeadlineCalendar(null, Weekend);
            var estimate = DeadlineCalculator.Calculate(_baseDate, rule, weekendOnly);
            var fromYear = Math.Min(_baseDate.Year, estimate.Deadline.Year) - 1;
            var toYear = Math.Max(_baseDate.Year, estimate.Deadline.Year) + 1;

            DeadlineResult result;
            for (var attempt = 0; ; attempt++)
            {
                var calendar = await GetCalendarAsync(fromYear, toYear);
                if (generation != _calcGeneration)
                {
                    return;
                }

                result = DeadlineCalculator.Calculate(_baseDate, rule, calendar);
                var withinRange = result.Deadline.Year >= fromYear + 1 && result.Deadline.Year <= toYear - 1;
                if (withinRange || attempt >= 4)
                {
                    break;
                }

                fromYear = Math.Min(fromYear, result.Deadline.Year - 1);
                toYear = Math.Max(toYear, result.Deadline.Year + 1);
            }

            _result = result;
            await OnCalculated.InvokeAsync(_result);
        }
        catch
        {
            if (generation == _calcGeneration)
            {
                _errorKey = "TmDeadlineCalculator_ErrorCalculation";
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task<DeadlineCalendar> GetCalendarAsync(int fromYear, int toYear)
    {
        if (HolidayProvider is null)
        {
            return new DeadlineCalendar(null, Weekend);
        }

        if (_calendarCache is not null && _calendarYears is { } years
            && years.From <= fromYear && years.To >= toYear)
        {
            return _calendarCache;
        }

        var from = Math.Min(fromYear, _calendarYears?.From ?? fromYear);
        var to = Math.Max(toYear, _calendarYears?.To ?? toYear);
        _calendarCache = await DeadlineCalendar.LoadAsync(HolidayProvider, from, to, Weekend);
        _calendarYears = (from, to);
        return _calendarCache;
    }

    // ── Form handlers ────────────────────────────────────────────────────────

    private string BaseDateInputValue
        => _baseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task HandleBaseDateChangedAsync(ChangeEventArgs e)
    {
        if (DateOnly.TryParseExact(e.Value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            _baseDate = date;
            await BaseDateChanged.InvokeAsync(date);
            await RecalculateAsync();
        }
    }

    private async Task HandleAmountChangedAsync(DeadlineRuleStep step, ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
        {
            step.Amount = Math.Clamp(amount, -MaxAmount, MaxAmount);
            await NotifyRuleChangedAsync();
            await RecalculateAsync();
        }
        else
        {
            _errorKey = "TmDeadlineCalculator_ErrorAmountInvalid";
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleUnitChangedAsync(DeadlineRuleStep step, ChangeEventArgs e)
    {
        if (Enum.TryParse<DeadlineUnit>(e.Value?.ToString(), out var unit))
        {
            step.Unit = unit;
            await NotifyRuleChangedAsync();
            await RecalculateAsync();
        }
    }

    private async Task HandleShiftChangedAsync(DeadlineRuleStep step, ChangeEventArgs e)
    {
        if (Enum.TryParse<DeadlineNonBusinessShift>(e.Value?.ToString(), out var shift))
        {
            step.NonBusinessShift = shift;
            await NotifyRuleChangedAsync();
            await RecalculateAsync();
        }
    }

    private async Task AddStepAsync()
    {
        _steps.Add(new DeadlineRuleStep { Amount = 1 });
        await NotifyRuleChangedAsync();
        await RecalculateAsync();
    }

    private async Task RemoveStepAsync(DeadlineRuleStep step)
    {
        if (_steps.Count <= 1)
        {
            return;
        }

        _steps.Remove(step);
        await NotifyRuleChangedAsync();
        await RecalculateAsync();
    }

    private Task NotifyRuleChangedAsync()
        => RuleChanged.HasDelegate ? RuleChanged.InvokeAsync(BuildRule()) : Task.CompletedTask;

    // ── Display helpers ──────────────────────────────────────────────────────

    private string UnitLabel(DeadlineUnit unit)
        => unit switch
        {
            DeadlineUnit.BusinessDays => Loc["TmDeadlineCalculator_UnitBusinessDays"],
            DeadlineUnit.Weeks => Loc["TmDeadlineCalculator_UnitWeeks"],
            DeadlineUnit.Months => Loc["TmDeadlineCalculator_UnitMonths"],
            DeadlineUnit.Years => Loc["TmDeadlineCalculator_UnitYears"],
            _ => Loc["TmDeadlineCalculator_UnitDays"]
        };

    private string ShiftLabel(DeadlineNonBusinessShift shift)
        => shift switch
        {
            DeadlineNonBusinessShift.NextBusinessDay => Loc["TmDeadlineCalculator_ShiftNext"],
            DeadlineNonBusinessShift.PreviousBusinessDay => Loc["TmDeadlineCalculator_ShiftPrevious"],
            _ => Loc["TmDeadlineCalculator_ShiftNone"]
        };

    private static string FormatDate(DateOnly date)
        => date.ToString("d", CultureInfo.CurrentCulture);

    private string FormatDayOfWeek(DateOnly date)
        => CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(date.DayOfWeek);

    private static string IsoDate(DateOnly date)
        => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private string ProtocolText(DeadlineProtocolEntry entry)
        => entry.Kind switch
        {
            DeadlineStepKind.Start => string.Format(CultureInfo.CurrentCulture,
                Loc["TmDeadlineCalculator_ProtocolStart"], FormatDate(entry.Date), FormatDayOfWeek(entry.Date)),
            DeadlineStepKind.AddUnits => string.Format(CultureInfo.CurrentCulture,
                Loc["TmDeadlineCalculator_ProtocolAdd"], entry.Amount, UnitLabel(entry.Unit), FormatDate(entry.Date), FormatDayOfWeek(entry.Date)),
            DeadlineStepKind.ShiftedFromWeekend => string.Format(CultureInfo.CurrentCulture,
                Loc["TmDeadlineCalculator_ProtocolWeekendShift"], FormatDate(entry.Date), FormatDayOfWeek(entry.Date)),
            DeadlineStepKind.ShiftedFromHoliday => string.Format(CultureInfo.CurrentCulture,
                Loc["TmDeadlineCalculator_ProtocolHolidayShift"], entry.HolidayName ?? string.Empty, FormatDate(entry.Date), FormatDayOfWeek(entry.Date)),
            _ => string.Format(CultureInfo.CurrentCulture,
                Loc["TmDeadlineCalculator_ProtocolFinal"], FormatDate(entry.Date), FormatDayOfWeek(entry.Date))
        };
}
