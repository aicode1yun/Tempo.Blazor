using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Deadline;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Deadline;

/// <summary>
/// bUnit tests for TmDeadlineCalculator: live form calculation, step protocol, chaining
/// rows, embed mode, holiday-provider integration, validation, and the OnCalculated event.
/// </summary>
public class TmDeadlineCalculatorTests : LocalizationTestBase
{
    private static readonly DateOnly Base = new(2026, 7, 3); // Friday

    private IRenderedComponent<TmDeadlineCalculator> Render(
        Action<Bunit.ComponentParameterCollectionBuilder<TmDeadlineCalculator>>? configure = null,
        DeadlineRule? rule = null)
        => RenderComponent<TmDeadlineCalculator>(p =>
        {
            p.Add(x => x.BaseDate, Base);
            p.Add(x => x.Rule, rule ?? DeadlineRule.Single(15));
            configure?.Invoke(p);
        });

    // ── Live result ──────────────────────────────────────────────────────────

    [Fact]
    public void RendersLiveResult_ForInitialRule()
    {
        var cut = Render();

        cut.WaitForAssertion(() =>
        {
            // 15 days from Friday 2026-07-03 lands on Saturday → shifted to Monday 2026-07-20.
            cut.Find("[data-testid='deadline-result-date']").GetAttribute("data-date").Should().Be("2026-07-20");
        });
    }

    [Fact]
    public void ChangingAmount_RecalculatesLive()
    {
        var cut = Render();
        cut.WaitForElement("[data-testid='deadline-result-date']");

        cut.Find("[data-testid='deadline-amount']").Change("1");

        cut.WaitForAssertion(() =>
            // +1 day from Friday = Saturday 7/4 → shifted to Monday 2026-07-06.
            cut.Find("[data-testid='deadline-result-date']").GetAttribute("data-date").Should().Be("2026-07-06"));
    }

    [Fact]
    public void ChangingBaseDate_RecalculatesLive()
    {
        var cut = Render();
        cut.WaitForElement("[data-testid='deadline-result-date']");

        cut.Find("[data-testid='deadline-base']").Change("2026-07-06");

        cut.WaitForAssertion(() =>
            // 15 days from Monday 7/6 = Tuesday 2026-07-21 (business day, no shift).
            cut.Find("[data-testid='deadline-result-date']").GetAttribute("data-date").Should().Be("2026-07-21"));
    }

    // ── Protocol ─────────────────────────────────────────────────────────────

    [Fact]
    public void Protocol_ListsStartAddShiftAndFinalEntries()
    {
        var cut = Render();

        cut.WaitForAssertion(() =>
        {
            var entries = cut.FindAll("[data-testid='deadline-protocol-entry']");
            entries.Count.Should().BeGreaterThanOrEqualTo(4); // start, add, ≥1 weekend shift, final
        });
    }

    [Fact]
    public void Protocol_CanBeHidden()
    {
        var cut = Render(p => p.Add(x => x.ShowProtocol, false));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='deadline-result-date']").Should().HaveCount(1);
            cut.FindAll("[data-testid='deadline-protocol']").Should().BeEmpty();
        });
    }

    // ── Chaining rows ────────────────────────────────────────────────────────

    [Fact]
    public void AddStep_AppendsRow_AndRecalculates()
    {
        var cut = Render();
        cut.WaitForElement("[data-testid='deadline-result-date']");

        cut.Find("[data-testid='deadline-add-step']").Click();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='deadline-step']").Should().HaveCount(2));
    }

    [Fact]
    public void RemoveStep_DeletesRow_AndRecalculates()
    {
        var rule = new DeadlineRule
        {
            Steps =
            [
                new DeadlineRuleStep { Amount = 15 },
                new DeadlineRuleStep { Amount = 1, Unit = DeadlineUnit.Months }
            ]
        };
        var cut = Render(rule: rule);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='deadline-step']").Should().HaveCount(2));

        cut.FindAll("[data-testid='deadline-remove-step']")[1].Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='deadline-step']").Should().HaveCount(1));
    }

    // ── Embed mode ───────────────────────────────────────────────────────────

    [Fact]
    public void EmbedMode_RendersResultWithoutForm()
    {
        var cut = Render(p => p.Add(x => x.ShowForm, false));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='deadline-result-date']").Should().HaveCount(1);
            cut.FindAll("[data-testid='deadline-base']").Should().BeEmpty();
            cut.FindAll("[data-testid='deadline-amount']").Should().BeEmpty();
            cut.FindAll("[data-testid='deadline-add-step']").Should().BeEmpty();
        });
    }

    // ── Holiday provider ─────────────────────────────────────────────────────

    [Fact]
    public void HolidayProvider_ShiftsDeadline_AndProtocolShowsHolidayName()
    {
        var provider = new InMemoryHolidayProvider(
        [
            new DeadlineHoliday { Date = new DateOnly(2026, 7, 6), Name = "Founding Day" }
        ]);
        // +1 day from Friday 7/3 = Saturday → Monday 7/6 is a holiday → Tuesday 7/7.
        var cut = Render(
            p => p.Add(x => x.HolidayProvider, provider),
            rule: DeadlineRule.Single(1));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='deadline-result-date']").GetAttribute("data-date").Should().Be("2026-07-07");
            cut.Find("[data-testid='deadline-protocol']").TextContent.Should().Contain("Founding Day");
        });
    }

    // ── Validation & events ──────────────────────────────────────────────────

    [Fact]
    public void ZeroAmount_ShowsValidationError()
    {
        var cut = Render();
        cut.WaitForElement("[data-testid='deadline-result-date']");

        cut.Find("[data-testid='deadline-amount']").Change("0");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='deadline-error']").Should().NotBeEmpty());
    }

    [Fact]
    public void OnCalculated_FiresWithResult()
    {
        DeadlineResult? calculated = null;
        var cut = Render(p => p
            .Add(x => x.OnCalculated, EventCallback.Factory.Create<DeadlineResult>(this, r => calculated = r)));

        cut.WaitForAssertion(() =>
        {
            calculated.Should().NotBeNull();
            calculated!.Deadline.Should().Be(new DateOnly(2026, 7, 20));
        });
    }
}
