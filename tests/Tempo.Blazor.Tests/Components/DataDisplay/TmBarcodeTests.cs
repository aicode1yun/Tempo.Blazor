using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DataDisplay;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DataDisplay;

public class TmBarcodeTests : LocalizationTestBase
{
    [Fact]
    public void TmBarcode_Code128_Renders_Svg()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST1234")
            .Add(x => x.Format, BarcodeFormat.Code128));

        cut.Find("svg").Should().NotBeNull();
    }

    [Fact]
    public void TmBarcode_DisplayValue_Renders_Text()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST1234")
            .Add(x => x.Format, BarcodeFormat.Code128)
            .Add(x => x.DisplayValue, true));

        var text = cut.Find(".tm-barcode__value");
        text.TextContent.Should().Be("TEST1234");
    }

    [Fact]
    public void TmBarcode_Invalid_Value_Renders_Error()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST!@#")
            .Add(x => x.Format, BarcodeFormat.EAN13));

        cut.Find(".tm-barcode__error").Should().NotBeNull();
    }

    [Fact]
    public void TmBarcode_Width_Applies_To_Svg()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST1234")
            .Add(x => x.Format, BarcodeFormat.Code128)
            .Add(x => x.Width, 300));

        var svg = cut.Find("svg");
        svg.GetAttribute("width").Should().Be("300");
    }

    [Fact]
    public void TmBarcode_Height_Applies_To_Svg()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST1234")
            .Add(x => x.Format, BarcodeFormat.Code128)
            .Add(x => x.Height, 120));

        var svg = cut.Find("svg");
        svg.GetAttribute("height").Should().Be("120");
    }

    [Fact]
    public void TmBarcode_DisplayValue_Exactly_One_Text_Element()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST1234")
            .Add(x => x.Format, BarcodeFormat.Code128)
            .Add(x => x.DisplayValue, true));

        cut.FindAll(".tm-barcode__value").Count.Should().Be(1);
    }

    [Fact]
    public void TmBarcode_DisplayValue_False_Removes_ZXing_Text()
    {
        var cut = RenderComponent<TmBarcode>(p => p
            .Add(x => x.Value, "TEST1234")
            .Add(x => x.Format, BarcodeFormat.Code128)
            .Add(x => x.DisplayValue, false));

        cut.FindAll("text").Count.Should().Be(0);
        cut.FindAll(".tm-barcode__value").Count.Should().Be(0);
    }

    [Fact]
    public void TmBarcode_Empty_Value_Renders_Nothing()
    {
        var cut = RenderComponent<TmBarcode>(p => p.Add(x => x.Value, ""));
        cut.FindAll("svg").Count.Should().Be(0);
    }
}
