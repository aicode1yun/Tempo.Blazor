using System.Globalization;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DataDisplay;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Ledger;

/// <summary>
/// bUnit tests for the atomic TmMoneyDisplay: currency-aware decimals, sign handling,
/// semantic color classes, invariant data attribute, and the empty placeholder.
/// </summary>
public class TmMoneyDisplayTests : LocalizationTestBase
{
    private IRenderedComponent<TmMoneyDisplay> Render(
        decimal? amount,
        string currency = "CZK",
        Action<Bunit.ComponentParameterCollectionBuilder<TmMoneyDisplay>>? configure = null)
        => RenderComponent<TmMoneyDisplay>(p =>
        {
            p.Add(x => x.Amount, amount);
            p.Add(x => x.Currency, currency);
            configure?.Invoke(p);
        });

    [Fact]
    public void FormatsAmountWithCurrencyCode_AndInvariantDataAttribute()
    {
        var cut = Render(1234.5m);

        var span = cut.Find(".tm-money");
        span.GetAttribute("data-amount").Should().Be("1234.50");
        span.GetAttribute("data-currency").Should().Be("CZK");
        span.TextContent.Should().Contain("CZK");
    }

    [Fact]
    public void ZeroDecimalCurrency_FormatsWithoutFraction()
    {
        var cut = Render(1234.56m, "JPY");

        cut.Find(".tm-money").GetAttribute("data-amount").Should().Be("1235");
    }

    [Fact]
    public void DecimalsOverride_Wins()
    {
        var cut = Render(1.23456m, configure: p => p.Add(x => x.Decimals, 4));

        cut.Find(".tm-money").GetAttribute("data-amount").Should().Be("1.2346");
    }

    [Fact]
    public void NegativeAmount_GetsNegativeClass_PositiveGetsPositive()
    {
        Render(-5m).Find(".tm-money").ClassList.Should().Contain("tm-money--negative");
        Render(5m).Find(".tm-money").ClassList.Should().Contain("tm-money--positive");
        Render(0m).Find(".tm-money").ClassList.Should().Contain("tm-money--zero");
    }

    [Fact]
    public void Colored_CanBeDisabled()
    {
        var cut = Render(-5m, configure: p => p.Add(x => x.Colored, false));

        cut.Find(".tm-money").ClassList.Should().NotContain("tm-money--negative");
    }

    [Fact]
    public void ShowSign_PrefixesExplicitPlus()
    {
        var cut = Render(5m, configure: p => p.Add(x => x.ShowSign, true));

        cut.Find(".tm-money").TextContent.TrimStart().Should().StartWith("+");
    }

    [Fact]
    public void NullAmount_RendersPlaceholder()
    {
        var cut = Render(null);

        cut.Find(".tm-money").ClassList.Should().Contain("tm-money--empty");
        cut.Find(".tm-money").GetAttribute("data-amount").Should().BeNull();
    }

    [Fact]
    public void WithoutCurrency_RendersPlainNumber()
    {
        var cut = Render(12.5m, currency: "");

        var span = cut.Find(".tm-money");
        span.TextContent.Should().NotContain("CZK");
        span.GetAttribute("data-amount").Should().Be("12.50");
    }

    [Fact]
    public void UsesCurrentCultureNumberFormatting()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
            var cut = Render(1234.5m);

            // Czech formatting uses a decimal comma; the data attribute stays invariant.
            cut.Find(".tm-money").TextContent.Should().Contain(",5");
            cut.Find(".tm-money").GetAttribute("data-amount").Should().Be("1234.50");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
