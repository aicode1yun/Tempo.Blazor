using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Pickers;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Pickers;

/// <summary>
/// The date-picker trigger's calendar affordance must be a real TmIcon (crisp, theme-aware SVG)
/// rather than the old emoji rendered via CSS ::after content.
/// </summary>
public class TmDatePickerIconTests : LocalizationTestBase
{
    [Fact]
    public void DatePicker_RendersCalendarSvgIcon_InsideIconSlot()
    {
        var cut = RenderComponent<TmDatePicker>();

        var iconSlot = cut.Find(".tm-date-picker-icon");
        var svg = iconSlot.QuerySelector("svg.tm-icon");
        svg.Should().NotBeNull("the calendar affordance is a TmIcon SVG, not an emoji");
    }

    [Fact]
    public void DatePicker_IconSlot_IsAriaHidden()
    {
        var cut = RenderComponent<TmDatePicker>();

        cut.Find(".tm-date-picker-icon").GetAttribute("aria-hidden").Should().Be("true");
    }
}
