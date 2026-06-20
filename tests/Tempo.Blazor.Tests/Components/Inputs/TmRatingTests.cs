using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>TDD tests for TmRating.</summary>
public class TmRatingTests : LocalizationTestBase
{
    [Fact]
    public void TmRating_Renders_Star_Count()
    {
        var cut = RenderComponent<TmRating>(p => p.Add(x => x.Max, 5));
        cut.FindAll(".tm-rating__star").Count.Should().Be(5);
    }

    [Fact]
    public void TmRating_Value_3_Has_3_Full_Stars()
    {
        var cut = RenderComponent<TmRating>(p => p.Add(x => x.Value, 3).Add(x => x.Max, 5));
        var stars = cut.FindAll(".tm-rating__star");
        stars[0].ClassList.Contains("tm-rating__star--full").Should().BeTrue();
        stars[1].ClassList.Contains("tm-rating__star--full").Should().BeTrue();
        stars[2].ClassList.Contains("tm-rating__star--full").Should().BeTrue();
        stars[3].ClassList.Contains("tm-rating__star--full").Should().BeFalse();
        stars[4].ClassList.Contains("tm-rating__star--full").Should().BeFalse();
    }

    [Fact]
    public void TmRating_Click_Sets_Value()
    {
        int? captured = null;
        var cut = RenderComponent<TmRating>(p => p
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        var stars = cut.FindAll(".tm-rating__star");
        stars[2].Click();

        captured.Should().Be(3);
    }

    [Fact]
    public void TmRating_ReadOnly_Does_Not_Fire_ValueChanged()
    {
        int? captured = null;
        var cut = RenderComponent<TmRating>(p => p
            .Add(x => x.ReadOnly, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        var stars = cut.FindAll(".tm-rating__star");
        stars[2].Click();

        captured.Should().BeNull();
    }

    [Fact]
    public void TmRating_Disabled_Has_Disabled_Class()
    {
        var cut = RenderComponent<TmRating>(p => p.Add(x => x.Disabled, true));
        cut.Find(".tm-rating").ClassList.Contains("tm-rating--disabled").Should().BeTrue();
    }

    [Fact]
    public void TmRating_Keyboard_Left_Decreases_Value()
    {
        int? captured = null;
        var cut = RenderComponent<TmRating>(p => p
            .Add(x => x.Value, 3)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        cut.Find(".tm-rating").KeyDown("ArrowLeft");
        captured.Should().Be(2);
    }

    [Fact]
    public void TmRating_Keyboard_Right_Increases_Value()
    {
        int? captured = null;
        var cut = RenderComponent<TmRating>(p => p
            .Add(x => x.Value, 3)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        cut.Find(".tm-rating").KeyDown("ArrowRight");
        captured.Should().Be(4);
    }

    [Fact]
    public void TmRating_Value_0_Has_No_Full_Stars()
    {
        var cut = RenderComponent<TmRating>(p => p.Add(x => x.Value, 0));
        cut.FindAll(".tm-rating__star--full").Count.Should().Be(0);
    }

    [Fact]
    public void TmRating_Max_10_Renders_10_Stars()
    {
        var cut = RenderComponent<TmRating>(p => p.Add(x => x.Max, 10));
        cut.FindAll(".tm-rating__star").Count.Should().Be(10);
    }
}
