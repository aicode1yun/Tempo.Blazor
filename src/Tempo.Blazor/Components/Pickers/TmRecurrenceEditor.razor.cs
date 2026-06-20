using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Pickers;

/// <summary>
/// A standalone editor for configuring recurring event rules in iCal RRULE format.
/// Supports Daily, Weekly, Monthly, and Yearly patterns with flexible end conditions.
/// </summary>
public partial class TmRecurrenceEditor
{
    private RecurrenceRule _rule = new();
    private string _monthlyMode = "day";
    private string _endMode = "never";
    private string _currentRRule = string.Empty;

    private readonly string[] _dayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private readonly IReadOnlyList<SelectOption<RecurrencePattern>> _patternOptions = new List<SelectOption<RecurrencePattern>>
    {
        new() { Value = RecurrencePattern.Daily, Label = "Daily" },
        new() { Value = RecurrencePattern.Weekly, Label = "Weekly" },
        new() { Value = RecurrencePattern.Monthly, Label = "Monthly" },
        new() { Value = RecurrencePattern.Yearly, Label = "Yearly" },
    };

    private readonly IReadOnlyList<SelectOption<int?>> _positionOptions = new List<SelectOption<int?>>
    {
        new() { Value = 1, Label = "first" },
        new() { Value = 2, Label = "second" },
        new() { Value = 3, Label = "third" },
        new() { Value = 4, Label = "fourth" },
        new() { Value = -1, Label = "last" },
    };

    private readonly IReadOnlyList<SelectOption<int>> _dayOfWeekOptions = new List<SelectOption<int>>
    {
        new() { Value = 1, Label = "Monday" },
        new() { Value = 2, Label = "Tuesday" },
        new() { Value = 3, Label = "Wednesday" },
        new() { Value = 4, Label = "Thursday" },
        new() { Value = 5, Label = "Friday" },
        new() { Value = 6, Label = "Saturday" },
        new() { Value = 0, Label = "Sunday" },
    };

    private readonly IReadOnlyList<SelectOption<int?>> _monthOptions = new List<SelectOption<int?>>
    {
        new() { Value = 1, Label = "January" },
        new() { Value = 2, Label = "February" },
        new() { Value = 3, Label = "March" },
        new() { Value = 4, Label = "April" },
        new() { Value = 5, Label = "May" },
        new() { Value = 6, Label = "June" },
        new() { Value = 7, Label = "July" },
        new() { Value = 8, Label = "August" },
        new() { Value = 9, Label = "September" },
        new() { Value = 10, Label = "October" },
        new() { Value = 11, Label = "November" },
        new() { Value = 12, Label = "December" },
    };

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>The recurrence rule value (iCal RRULE string).</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Fires when the rule changes.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>The start date used when parsing the rule. Default is today.</summary>
    [Parameter] public DateTime StartDate { get; set; } = DateTime.Today;

    /// <summary>Whether to show the generated RRULE summary. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowSummary { get; set; } = true;

    /// <summary>Disables the editor.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _rule = RecurrenceParser.FromRRule(Value ?? string.Empty, StartDate);
        if (_rule.StartDate == default)
            _rule.StartDate = StartDate;

        // Sync monthly mode
        if (_rule.Pattern == RecurrencePattern.Monthly)
        {
            _monthlyMode = _rule.Position.HasValue ? "position" : "day";
        }

        // Sync end mode
        _endMode = _rule.EndAfter switch
        {
            int => "count",
            DateTime => "date",
            _ => "never"
        };

        _currentRRule = RecurrenceParser.ToRRule(_rule);
    }

    // ── Event Handlers ───────────────────────────────────────────

    private async Task OnPatternChangedAsync(RecurrencePattern pattern)
    {
        _rule.Pattern = pattern;
        await NotifyChangeAsync();
    }

    private async Task OnIntervalChangedAsync(int? interval)
    {
        _rule.Interval = interval ?? 1;
        await NotifyChangeAsync();
    }

    private async Task ToggleDayAsync(int dayIndex)
    {
        var days = _rule.DaysOfWeek.ToList();
        if (days.Contains(dayIndex))
            days.Remove(dayIndex);
        else
            days.Add(dayIndex);
        _rule.DaysOfWeek = days.OrderBy(d => d).ToList();
        await NotifyChangeAsync();
    }

    private async Task SetMonthlyModeAsync(string mode)
    {
        _monthlyMode = mode;
        if (mode == "day")
        {
            _rule.Position = null;
            _rule.DaysOfWeek = [];
            if (!_rule.DayOfMonth.HasValue)
                _rule.DayOfMonth = _rule.StartDate.Day;
        }
        else
        {
            _rule.DayOfMonth = null;
            if (!_rule.Position.HasValue)
                _rule.Position = 1;
            if (_rule.DaysOfWeek.Count == 0)
                _rule.DaysOfWeek = [(int)_rule.StartDate.DayOfWeek];
        }
        await NotifyChangeAsync();
    }

    private async Task OnDayOfMonthChangedAsync(int? day)
    {
        _rule.DayOfMonth = day;
        await NotifyChangeAsync();
    }

    private async Task OnPositionChangedAsync(int? position)
    {
        _rule.Position = position;
        await NotifyChangeAsync();
    }

    private async Task OnMonthlyDayChangedAsync(int dayOfWeek)
    {
        _rule.DaysOfWeek = [dayOfWeek];
        await NotifyChangeAsync();
    }

    private async Task OnMonthOfYearChangedAsync(int? month)
    {
        _rule.MonthOfYear = month;
        await NotifyChangeAsync();
    }

    private async Task SetEndModeAsync(string mode)
    {
        _endMode = mode;
        _rule.EndAfter = mode switch
        {
            "count" => 10,
            "date" => _rule.StartDate.AddYears(1),
            _ => null
        };
        await NotifyChangeAsync();
    }

    private async Task OnEndCountChangedAsync(int? count)
    {
        if (_endMode == "count")
            _rule.EndAfter = count ?? 1;
        await NotifyChangeAsync();
    }

    private async Task OnEndDateChangedAsync(DateOnly? date)
    {
        if (_endMode == "date")
            _rule.EndAfter = date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue) : null;
        await NotifyChangeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task NotifyChangeAsync()
    {
        _currentRRule = RecurrenceParser.ToRRule(_rule);
        await ValueChanged.InvokeAsync(_currentRRule);
    }

    private static string GetIntervalLabel(RecurrencePattern pattern) => pattern switch
    {
        RecurrencePattern.Daily => "day(s)",
        RecurrencePattern.Weekly => "week(s)",
        RecurrencePattern.Monthly => "month(s)",
        RecurrencePattern.Yearly => "year(s)",
        _ => "day(s)"
    };

    private int? GetEndCount() => _rule.EndAfter is int c ? c : null;
    private DateOnly? GetEndDate() => _rule.EndAfter is DateTime d ? DateOnly.FromDateTime(d) : null;
}
