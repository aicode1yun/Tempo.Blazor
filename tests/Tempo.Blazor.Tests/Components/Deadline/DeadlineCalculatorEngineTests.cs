using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Deadline;

/// <summary>
/// Reference test set for the pure deadline rule engine: calendar and business-day units,
/// weekend/holiday shifts, chaining, leap years, end-of-month clamping, DST-transition
/// safety, and the step protocol.
/// </summary>
public class DeadlineCalculatorEngineTests
{
    private static DeadlineCalendar Calendar(params (int Year, int Month, int Day, string Name)[] holidays)
        => new(holidays.Select(h => new DeadlineHoliday { Date = new DateOnly(h.Year, h.Month, h.Day), Name = h.Name }));

    private static DeadlineRule Rule(int amount, DeadlineUnit unit = DeadlineUnit.Days,
        DeadlineNonBusinessShift shift = DeadlineNonBusinessShift.NextBusinessDay)
        => DeadlineRule.Single(amount, unit, shift);

    // ── Flagship case: 15 days ending on Saturday → Monday ──────────────────

    [Fact]
    public void FifteenDays_EndingOnSaturday_ShiftsToMonday()
    {
        // 2026-07-03 is a Friday; +15 days = 2026-07-18, a Saturday.
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 3), Rule(15), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 7, 20));
        result.Deadline.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.Protocol.Should().Contain(e => e.Kind == DeadlineStepKind.ShiftedFromWeekend);
        result.Protocol.Last().Kind.Should().Be(DeadlineStepKind.Final);
        result.Protocol.Last().Date.Should().Be(new DateOnly(2026, 7, 20));
    }

    [Fact]
    public void ShiftNone_KeepsWeekendDate()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 3),
            Rule(15, shift: DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 7, 18));
        result.Deadline.DayOfWeek.Should().Be(DayOfWeek.Saturday);
    }

    [Fact]
    public void ShiftPreviousBusinessDay_MovesBackToFriday()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 3),
            Rule(15, shift: DeadlineNonBusinessShift.PreviousBusinessDay), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 7, 17));
        result.Deadline.DayOfWeek.Should().Be(DayOfWeek.Friday);
    }

    // ── Holidays ─────────────────────────────────────────────────────────────

    [Fact]
    public void HolidayOnDeadline_ShiftsPastIt_AndProtocolNamesTheHoliday()
    {
        // 2026-12-24 (Thu) + 1 day = 2026-12-25 (Fri) which is a holiday; shift → Monday 12-28.
        var calendar = Calendar((2026, 12, 25, "Christmas Day"));
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 12, 24), Rule(1), calendar);

        result.Deadline.Should().Be(new DateOnly(2026, 12, 28));
        result.Protocol.Should().Contain(e =>
            e.Kind == DeadlineStepKind.ShiftedFromHoliday && e.HolidayName == "Christmas Day");
    }

    [Fact]
    public void BusinessDays_SkipWeekendsAndHolidaysWhileCounting()
    {
        // From Wed 2026-07-01, +5 business days skipping Mon 7/6 holiday:
        // Thu 2, Fri 3, Mon 6 is holiday → skip, Tue 7, Wed 8, Thu 9.
        var calendar = Calendar((2026, 7, 6, "Founding Day"));
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 1),
            Rule(5, DeadlineUnit.BusinessDays), calendar);

        result.Deadline.Should().Be(new DateOnly(2026, 7, 9));
    }

    [Fact]
    public void NegativeBusinessDays_CountBackwards()
    {
        // From Mon 2026-07-13, -2 business days: Fri 7/10, Thu 7/9.
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 13),
            Rule(-2, DeadlineUnit.BusinessDays), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 7, 9));
    }

    // ── Weeks / months / years, leap years, end of month ────────────────────

    [Fact]
    public void Weeks_AddSevenDaysEach()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 1),
            Rule(2, DeadlineUnit.Weeks, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 7, 15));
    }

    [Fact]
    public void EndOfMonth_ClampsWhenTargetMonthIsShorter()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 3, 31),
            Rule(1, DeadlineUnit.Months, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 4, 30));
    }

    [Fact]
    public void LeapYear_JanuaryThirtyFirstPlusOneMonth_IsFebruaryTwentyNinth()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2028, 1, 31),
            Rule(1, DeadlineUnit.Months, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void LeapDay_PlusOneYear_ClampsToFebruaryTwentyEighth()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2028, 2, 29),
            Rule(1, DeadlineUnit.Years, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2029, 2, 28));
    }

    // ── DST safety: date arithmetic is calendar-based, never time-based ─────

    [Fact]
    public void DstSpringForward_DoesNotSkewCalendarAddition()
    {
        // Europe: DST starts 2026-03-29. Naive DateTime+TimeSpan(72h) arithmetic in local
        // time would land on 03-31 23:00; calendar addition must yield exactly 03-31.
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 3, 28),
            Rule(3, DeadlineUnit.Days, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void DstFallBack_DoesNotSkewCalendarAddition()
    {
        // Europe: DST ends 2026-10-25.
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 10, 24),
            Rule(2, DeadlineUnit.Days, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 10, 26));
    }

    // ── Chaining ─────────────────────────────────────────────────────────────

    [Fact]
    public void ChainedSteps_ApplySequentially_WithPerStepShifts()
    {
        // Step 1: +1 month from 2026-06-30 → 2026-07-30 (Thu), no shift needed.
        // Step 2: +2 days → 2026-08-01 (Sat) → shift to Monday 2026-08-03.
        var rule = new DeadlineRule
        {
            Steps =
            [
                new DeadlineRuleStep { Amount = 1, Unit = DeadlineUnit.Months },
                new DeadlineRuleStep { Amount = 2, Unit = DeadlineUnit.Days }
            ]
        };

        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 6, 30), rule, new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 8, 3));
        result.Protocol.Count(e => e.Kind == DeadlineStepKind.AddUnits).Should().Be(2);
    }

    [Fact]
    public void EmptyRule_ReturnsBaseDate_WithStartAndFinalProtocol()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 1), new DeadlineRule(), new DeadlineCalendar());

        result.Deadline.Should().Be(new DateOnly(2026, 7, 1));
        result.Protocol.First().Kind.Should().Be(DeadlineStepKind.Start);
        result.Protocol.Last().Kind.Should().Be(DeadlineStepKind.Final);
    }

    [Fact]
    public void Protocol_StartsWithBaseDate_AndTracksEachStep()
    {
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 3), Rule(15), new DeadlineCalendar());

        result.Protocol.First().Kind.Should().Be(DeadlineStepKind.Start);
        result.Protocol.First().Date.Should().Be(new DateOnly(2026, 7, 3));
        var add = result.Protocol.First(e => e.Kind == DeadlineStepKind.AddUnits);
        add.Amount.Should().Be(15);
        add.Unit.Should().Be(DeadlineUnit.Days);
        add.Date.Should().Be(new DateOnly(2026, 7, 18));
    }

    // ── Custom weekend definitions ───────────────────────────────────────────

    [Fact]
    public void CustomWeekend_FridaySaturday_IsRespected()
    {
        var calendar = new DeadlineCalendar(holidays: null, weekend: [DayOfWeek.Friday, DayOfWeek.Saturday]);
        // 2026-07-01 (Wed) + 2 days = Fri 2026-07-03 → shifted to Sunday 2026-07-05.
        var result = DeadlineCalculator.Calculate(new DateOnly(2026, 7, 1), Rule(2), calendar);

        result.Deadline.Should().Be(new DateOnly(2026, 7, 5));
        result.Deadline.DayOfWeek.Should().Be(DayOfWeek.Sunday);
    }

    // ── Engine guards ────────────────────────────────────────────────────────

    [Fact]
    public void AllDaysNonBusiness_ThrowsInsteadOfSpinning()
    {
        var everyDayWeekend = new DeadlineCalendar(null, Enum.GetValues<DayOfWeek>());

        var act = () => DeadlineCalculator.Calculate(new DateOnly(2026, 7, 3), Rule(1), everyDayWeekend);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExcessiveStepAmount_ThrowsArgumentException()
    {
        var act = () => DeadlineCalculator.Calculate(new DateOnly(2026, 7, 3),
            Rule(400_000_000, DeadlineUnit.Weeks, DeadlineNonBusinessShift.None), new DeadlineCalendar());

        act.Should().Throw<ArgumentException>();
    }

    // ── Calendar loading from IHolidayProvider ───────────────────────────────

    [Fact]
    public async Task Calendar_LoadAsync_MaterializesProviderYears()
    {
        var provider = new InMemoryHolidayProvider(
        [
            new DeadlineHoliday { Date = new DateOnly(2026, 1, 1), Name = "New Year" },
            new DeadlineHoliday { Date = new DateOnly(2027, 1, 1), Name = "New Year" }
        ]);

        var calendar = await DeadlineCalendar.LoadAsync(provider, 2026, 2027);

        calendar.TryGetHoliday(new DateOnly(2026, 1, 1), out var name).Should().BeTrue();
        name.Should().Be("New Year");
        calendar.IsBusinessDay(new DateOnly(2027, 1, 1)).Should().BeFalse();
    }

    [Fact]
    public async Task Calendar_LoadAsync_WithoutProvider_HasWeekendsOnly()
    {
        var calendar = await DeadlineCalendar.LoadAsync(null, 2026, 2026);

        calendar.IsBusinessDay(new DateOnly(2026, 7, 18)).Should().BeFalse(); // Saturday
        calendar.IsBusinessDay(new DateOnly(2026, 7, 20)).Should().BeTrue();  // Monday
    }
}
