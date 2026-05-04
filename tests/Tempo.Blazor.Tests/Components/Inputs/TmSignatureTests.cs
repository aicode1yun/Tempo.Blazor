using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmSignatureTests : LocalizationTestBase
{
    [Fact]
    public void TmSignature_Renders_Svg_Canvas()
    {
        var cut = RenderComponent<TmSignature>();

        cut.Find("svg.tm-signature__canvas").Should().NotBeNull();
    }

    [Fact]
    public void TmSignature_Default_Dimensions_Are_400x200()
    {
        var cut = RenderComponent<TmSignature>();
        var svg = cut.Find("svg.tm-signature__canvas");

        svg.GetAttribute("width").Should().Be("400");
        svg.GetAttribute("height").Should().Be("200");
    }

    [Fact]
    public void TmSignature_Custom_Dimensions_Applied()
    {
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.Width, 600)
            .Add(c => c.Height, 300));

        var svg = cut.Find("svg.tm-signature__canvas");
        svg.GetAttribute("width").Should().Be("600");
        svg.GetAttribute("height").Should().Be("300");
    }

    [Fact]
    public void TmSignature_Clear_Button_Removes_Paths()
    {
        var cut = RenderComponent<TmSignature>();

        // Simulate a stroke via pointer events using TriggerEvent
        var svg = cut.Find("svg.tm-signature__canvas");
        svg.TriggerEvent("onpointerdown", new PointerEventArgs { OffsetX = 10, OffsetY = 10, PointerId = 1 });
        svg.TriggerEvent("onpointermove", new PointerEventArgs { OffsetX = 20, OffsetY = 20, PointerId = 1 });
        svg.TriggerEvent("onpointerup", new PointerEventArgs { OffsetX = 20, OffsetY = 20, PointerId = 1 });

        cut.FindAll("polyline").Count.Should().BeGreaterThan(0);

        // Click clear
        cut.Find(".tm-signature__clear").Click();

        cut.FindAll("polyline").Should().BeEmpty();
    }

    [Fact]
    public void TmSignature_Clear_Resets_Value()
    {
        string? value = "initial";
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.Value, "some-svg-data")
            .Add(c => c.ValueChanged, (string? v) => value = v));

        cut.Find(".tm-signature__clear").Click();

        value.Should().BeNullOrEmpty();
    }

    [Fact]
    public void TmSignature_Disabled_Hides_Clear_Button()
    {
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.Disabled, true));

        cut.FindAll(".tm-signature__clear").Should().BeEmpty();
    }

    [Fact]
    public void TmSignature_Disabled_Canvas_Has_Disabled_Class()
    {
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.Disabled, true));

        cut.Find(".tm-signature--disabled").Should().NotBeNull();
    }

    [Fact]
    public void TmSignature_StrokeColor_Applied_To_Path()
    {
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.StrokeColor, "#ff0000"));

        // Draw a stroke
        var svg = cut.Find("svg.tm-signature__canvas");
        svg.TriggerEvent("onpointerdown", new PointerEventArgs { OffsetX = 10, OffsetY = 10, PointerId = 1 });
        svg.TriggerEvent("onpointermove", new PointerEventArgs { OffsetX = 20, OffsetY = 20, PointerId = 1 });
        svg.TriggerEvent("onpointerup", new PointerEventArgs { OffsetX = 20, OffsetY = 20, PointerId = 1 });

        var polyline = cut.Find("polyline");
        polyline.GetAttribute("stroke").Should().Be("#ff0000");
    }

    [Fact]
    public void TmSignature_BackgroundColor_Applied_To_Svg()
    {
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.BackgroundColor, "#f0f0f0"));

        var svg = cut.Find("svg.tm-signature__canvas");
        svg.GetAttribute("style").Should().Contain("background-color: #f0f0f0");
    }

    [Fact]
    public void TmSignature_ShowClearButton_False_Hides_Button()
    {
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.ShowClearButton, false));

        cut.FindAll(".tm-signature__clear").Should().BeEmpty();
    }

    [Fact]
    public void TmSignature_ValueChanged_Fires_After_Stroke()
    {
        string? capturedValue = null;
        var cut = RenderComponent<TmSignature>(p => p
            .Add(c => c.ValueChanged, (string? v) => capturedValue = v));

        var svg = cut.Find("svg.tm-signature__canvas");
        svg.TriggerEvent("onpointerdown", new PointerEventArgs { OffsetX = 10, OffsetY = 10, PointerId = 1 });
        svg.TriggerEvent("onpointermove", new PointerEventArgs { OffsetX = 20, OffsetY = 20, PointerId = 1 });
        svg.TriggerEvent("onpointerup", new PointerEventArgs { OffsetX = 20, OffsetY = 20, PointerId = 1 });

        capturedValue.Should().NotBeNullOrEmpty();
    }
}
