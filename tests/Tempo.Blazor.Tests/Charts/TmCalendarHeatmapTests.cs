using System.Globalization;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Charts;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Charts;

/// <summary>TDD tests for the calendar heatmap date grid.</summary>
public class TmCalendarHeatmapTests : LocalizationTestBase
{
    private static readonly IReadOnlyDictionary<DateOnly, decimal> NoValues =
        new Dictionary<DateOnly, decimal>();

    [Fact]
    public void CalendarHeatmap_RendersRootAndEveryDayOfNonLeapYear()
    {
        var cut = Render<TmCalendarHeatmap>(parameters => parameters
            .Add(component => component.Values, NoValues)
            .Add(component => component.Year, 2025));

        cut.Find(".tm-calendar-heatmap").Should().NotBeNull();
        cut.FindAll(".tm-calendar-heatmap__day").Should().HaveCount(365);
    }

    [Theory]
    [InlineData("cs-CZ", "grid-column: 1", "grid-row: 3")]
    [InlineData("en-US", "grid-column: 1", "grid-row: 4")]
    public void CalendarHeatmap_AlignsFirstWeekToCulturesFirstDay(
        string cultureName,
        string expectedColumn,
        string expectedRow)
    {
        using var _ = UseCulture(cultureName);

        var cut = Render<TmCalendarHeatmap>(parameters => parameters
            .Add(component => component.Values, NoValues)
            .Add(component => component.Year, 2025));

        var firstDay = cut.Find("[data-date='2025-01-01']");
        firstDay.GetAttribute("style").Should()
            .Contain(expectedColumn)
            .And.Contain(expectedRow);
    }

    [Fact]
    public void CalendarHeatmap_FromAndToLimitRenderedDatesInclusively()
    {
        var cut = Render<TmCalendarHeatmap>(parameters => parameters
            .Add(component => component.Values, NoValues)
            .Add(component => component.From, new DateOnly(2025, 3, 10))
            .Add(component => component.To, new DateOnly(2025, 3, 16)));

        var days = cut.FindAll(".tm-calendar-heatmap__day");

        days.Should().HaveCount(7);
        days.Should().OnlyContain(day => day.HasAttribute("data-date"));
        days.Select(day => day.GetAttribute("data-date")).Should().Equal(
            "2025-03-10",
            "2025-03-11",
            "2025-03-12",
            "2025-03-13",
            "2025-03-14",
            "2025-03-15",
            "2025-03-16");
        cut.FindAll("[data-date='2025-03-09']").Should().BeEmpty();
        cut.FindAll("[data-date='2025-03-17']").Should().BeEmpty();
    }

    [Fact]
    public void CalendarHeatmap_YearTakesPrecedenceOverFromAndTo()
    {
        var cut = Render<TmCalendarHeatmap>(parameters => parameters
            .Add(component => component.Values, NoValues)
            .Add(component => component.Year, 2025)
            .Add(component => component.From, new DateOnly(2024, 3, 10))
            .Add(component => component.To, new DateOnly(2024, 3, 16)));

        cut.FindAll(".tm-calendar-heatmap__day").Should().HaveCount(365);
        cut.Find("[data-date='2025-01-01']").Should().NotBeNull();
        cut.FindAll("[data-date='2024-03-10']").Should().BeEmpty();
    }

    [Fact]
    public void CalendarHeatmap_CellSizeSetsRootCssCustomProperty()
    {
        var cut = Render<TmCalendarHeatmap>(parameters => parameters
            .Add(component => component.Values, NoValues)
            .Add(component => component.Year, 2025)
            .Add(component => component.CellSize, 18));

        cut.Find(".tm-calendar-heatmap")
            .GetAttribute("style")
            .Should().Contain("--tm-heatmap-cell-size: 18px");
    }

    private static IDisposable UseCulture(string name)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        return new CultureRestore(previousCulture, previousUiCulture);
    }

    private sealed class CultureRestore(CultureInfo culture, CultureInfo uiCulture) : IDisposable
    {
        public void Dispose()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
