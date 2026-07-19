using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DataDisplay;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DataDisplay;

public class TmQRCodeTests : LocalizationTestBase
{
    [Fact]
    public void TmQRCode_Renders_Svg()
    {
        var cut = Render<TmQRCode>(p => p.Add(x => x.Value, "https://example.com"));
        cut.Find("svg").Should().NotBeNull();
    }

    [Fact]
    public void TmQRCode_Size_Applies_Width_And_Height()
    {
        var cut = Render<TmQRCode>(p => p
            .Add(x => x.Value, "test")
            .Add(x => x.Size, 300));

        var svg = cut.Find("svg");
        svg.GetAttribute("width").Should().Be("300");
        svg.GetAttribute("height").Should().Be("300");
    }

    [Fact]
    public void TmQRCode_Default_Size_Is_200()
    {
        var cut = Render<TmQRCode>(p => p.Add(x => x.Value, "test"));
        var svg = cut.Find("svg");
        svg.GetAttribute("width").Should().Be("200");
        svg.GetAttribute("height").Should().Be("200");
    }

    [Fact]
    public void TmQRCode_Changed_Value_Regenerates()
    {
        var cut = Render<TmQRCode>(p => p.Add(x => x.Value, "first"));
        var firstSvg = cut.Find("svg").OuterHtml;

        cut.Render(p => p.Add(x => x.Value, "second"));
        var secondSvg = cut.Find("svg").OuterHtml;

        secondSvg.Should().NotBe(firstSvg);
    }

    [Fact]
    public void TmQRCode_ForegroundColor_Applies_To_Svg()
    {
        var cut = Render<TmQRCode>(p => p
            .Add(x => x.Value, "test")
            .Add(x => x.ForegroundColor, "#ff0000"));

        var svg = cut.Find("svg");
        svg.InnerHtml.Should().Contain("fill=\"#ff0000\"");
    }

    [Fact]
    public void TmQRCode_BackgroundColor_Applies_To_Svg()
    {
        var cut = Render<TmQRCode>(p => p
            .Add(x => x.Value, "test")
            .Add(x => x.BackgroundColor, "#00ff00"));

        var svg = cut.Find("svg");
        svg.GetAttribute("style").Should().Contain("background-color: #00ff00");
    }

    [Fact]
    public void TmQRCode_Empty_Value_Renders_Nothing()
    {
        var cut = Render<TmQRCode>(p => p.Add(x => x.Value, ""));
        cut.FindAll("svg").Count.Should().Be(0);
    }

    [Fact]
    public void TmQRCode_Internal_Rects_Keep_Original_Dimensions()
    {
        var cut = Render<TmQRCode>(p => p
            .Add(x => x.Value, "test")
            .Add(x => x.Size, 300));

        var rects = cut.FindAll("rect");
        rects.Count.Should().BeGreaterThan(10);

        // At least one rect must have a small width (a QR module), not the full 300px
        var hasSmallRect = rects.Any(r =>
        {
            var w = r.GetAttribute("width");
            return w != null && int.TryParse(w, out var wi) && wi < 100;
        });
        hasSmallRect.Should().BeTrue("QR module rects should retain their original small widths");
    }

    [Fact]
    public void TmQRCode_ErrorCorrectionLevel_Propagates()
    {
        // H level should still produce valid QR output (just different module pattern)
        var cut = Render<TmQRCode>(p => p
            .Add(x => x.Value, "test")
            .Add(x => x.ErrorCorrectionLevel, QRErrorCorrectionLevel.H));

        cut.Find("svg").Should().NotBeNull();
    }
}
