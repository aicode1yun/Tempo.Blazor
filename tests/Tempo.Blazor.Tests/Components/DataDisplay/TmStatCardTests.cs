using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DataDisplay;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DataDisplay;

/// <summary>TDD tests for TmStatCard.</summary>
public class TmStatCardTests : LocalizationTestBase
{
    [Fact]
    public void TmStatCard_Has_Base_CssClass()
    {
        var cut = Render<TmStatCard>(p => p
            .Add(c => c.Title, "Users")
            .Add(c => c.Value, "1,234"));

        cut.Find(".tm-stat-card").Should().NotBeNull();
    }

    [Fact]
    public void TmStatCard_Renders_Value()
    {
        var cut = Render<TmStatCard>(p => p
            .Add(c => c.Title, "Users")
            .Add(c => c.Value, "1,234"));

        cut.Find(".tm-stat-value").TextContent.Should().Contain("1,234");
    }

    [Fact]
    public void TmStatCard_Renders_Title()
    {
        var cut = Render<TmStatCard>(p => p
            .Add(c => c.Title, "Active users")
            .Add(c => c.Value, "42"));

        cut.Find(".tm-stat-label").TextContent.Should().Contain("Active users");
    }

    [Fact]
    public void TmStatCard_Renders_SubValue_When_Set()
    {
        var cut = Render<TmStatCard>(p => p
            .Add(c => c.Title, "Revenue")
            .Add(c => c.Value, "$5,000")
            .Add(c => c.SubValue, "+12% this month"));

        cut.Find(".tm-stat-subvalue").TextContent.Should().Contain("+12% this month");
    }

    [Fact]
    public void TmStatCard_No_SubValue_When_Null()
    {
        var cut = Render<TmStatCard>(p => p
            .Add(c => c.Title, "Revenue")
            .Add(c => c.Value, "$5,000"));

        cut.FindAll(".tm-stat-subvalue").Should().BeEmpty();
    }

    /// <summary>
    /// <c>SubValueColor</c> without <c>SubValue</c> is an affordance without a mechanism: the call site
    /// states an intent that rendering silently denies, because the whole span hangs off
    /// <c>SubValue</c>. The card refuses it instead of ignoring it.
    /// </summary>
    /// <remarks>
    /// The invariant lives in the COMPONENT, not in a markup scanner, and that placement is the point: it
    /// covers splatted <c>@attributes</c>, <c>DynamicComponent</c> and consumers outside this repository,
    /// and it fails at the moment of misuse. A scanner is a legitimate second line for the release gate,
    /// but it cannot see those paths and must carry its own denominator.
    /// </remarks>
    [Fact]
    public void TmStatCard_Rejects_SubValueColor_Without_SubValue()
    {
        var act = () => Render<TmStatCard>(p => p
            .Add(c => c.Title, "Revenue")
            .Add(c => c.Value, "$5,000")
            .Add(c => c.SubValueColor, "tm-text-success"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SubValueColor*")
            .WithMessage("*SubValue*")
            .WithMessage("*Revenue*", "the message has to say WHICH card, or it is a riddle in a log");
    }

    /// <summary>The invariant is about the PAIR, so neither half alone may be made illegal.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("+12%", null)]
    [InlineData("+12%", "tm-text-success")]
    public void TmStatCard_Accepts_EverySoundCombination(string? subValue, string? subValueColor)
    {
        var act = () => Render<TmStatCard>(p => p
            .Add(c => c.Title, "Revenue")
            .Add(c => c.Value, "$5,000")
            .Add(c => c.SubValue, subValue)
            .Add(c => c.SubValueColor, subValueColor));

        act.Should().NotThrow();
    }

    /// <summary>
    /// The rejection must survive a LATER parameter change too — a card that starts sound and is then
    /// re-rendered with the value removed is the same broken state, reached one render later.
    /// </summary>
    [Fact]
    public void TmStatCard_Rejects_SubValue_Removed_On_Rerender()
    {
        var cut = Render<TmStatCard>(p => p
            .Add(c => c.Title, "Revenue")
            .Add(c => c.Value, "$5,000")
            .Add(c => c.SubValue, "+12%")
            .Add(c => c.SubValueColor, "tm-text-success"));

        var act = () => cut.Render(p => p
            .Add(c => c.Title, "Revenue")
            .Add(c => c.Value, "$5,000")
            .Add(c => c.SubValue, (string?)null)
            .Add(c => c.SubValueColor, "tm-text-success"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*SubValueColor*");
    }
}
