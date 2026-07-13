using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>
/// TDD tests for the additive <see cref="IExchangeRateProvider"/> support on TmCurrencyInput.
/// The existing callback-based <c>ConvertAsync</c> FX must keep working as a fallback.
/// </summary>
public class TmCurrencyInputProviderTests : LocalizationTestBase
{
    private static InMemoryExchangeRateProvider Rates(params (string From, string To, decimal Rate)[] table)
    {
        var dict = new Dictionary<(string, string), decimal>();
        foreach (var (from, to, rate) in table)
        {
            dict[(from, to)] = rate;
        }

        return new InMemoryExchangeRateProvider(dict);
    }

    private static IElement AmountInput(IRenderedComponent<TmCurrencyInput> cut)
        => cut.Find(".tm-currency-input__amount input");

    [Fact]
    public void Provider_ConvertsAmountUsingFetchedRate()
    {
        decimal? converted = null;
        var cut = RenderComponent<TmCurrencyInput>(p => p
            .Add(x => x.Currency, "USD")
            .Add(x => x.TargetCurrency, "CZK")
            .Add(x => x.ExchangeRateProvider, Rates(("USD", "CZK", 23.10m)))
            .Add(x => x.ConvertedAmountChanged,
                EventCallback.Factory.Create<decimal?>(this, v => converted = v)));

        AmountInput(cut).Change("100");

        converted.Should().Be(2310.00m);
    }

    [Fact]
    public void Provider_RateUnavailable_LeavesConvertedUnchanged()
    {
        var convertedFired = false;
        var cut = RenderComponent<TmCurrencyInput>(p => p
            .Add(x => x.Currency, "USD")
            .Add(x => x.TargetCurrency, "CZK")
            .Add(x => x.ExchangeRateProvider, Rates()) // empty table => rate unavailable (null)
            .Add(x => x.ConvertedAmountChanged,
                EventCallback.Factory.Create<decimal?>(this, _ => convertedFired = true)));

        AmountInput(cut).Change("100");

        convertedFired.Should().BeFalse();
    }

    [Fact]
    public void Provider_ShowsConvertedColumn_EvenWithoutCallback()
    {
        var cut = RenderComponent<TmCurrencyInput>(p => p
            .Add(x => x.Currency, "USD")
            .Add(x => x.TargetCurrency, "CZK")
            .Add(x => x.ExchangeRateProvider, Rates(("USD", "CZK", 23.10m))));

        cut.FindAll(".tm-currency-input__converted").Should().NotBeEmpty();
    }

    [Fact]
    public void Provider_TakesPrecedenceOverCallback()
    {
        decimal? converted = null;
        var cut = RenderComponent<TmCurrencyInput>(p => p
            .Add(x => x.Currency, "USD")
            .Add(x => x.TargetCurrency, "CZK")
            .Add(x => x.ExchangeRateProvider, Rates(("USD", "CZK", 23.10m)))
            .Add(x => x.ConvertAsync, (amount, _) => Task.FromResult<decimal?>(999m))
            .Add(x => x.ConvertedAmountChanged,
                EventCallback.Factory.Create<decimal?>(this, v => converted = v)));

        AmountInput(cut).Change("100");

        converted.Should().Be(2310.00m); // provider rate, not the callback's 999
    }

    [Fact]
    public void NoProvider_UsesCallbackExactlyAsBefore()
    {
        decimal? converted = null;
        var cut = RenderComponent<TmCurrencyInput>(p => p
            .Add(x => x.Currency, "USD")
            .Add(x => x.ConvertAsync, (amount, _) => Task.FromResult<decimal?>(amount * 2m))
            .Add(x => x.ConvertedAmountChanged,
                EventCallback.Factory.Create<decimal?>(this, v => converted = v)));

        AmountInput(cut).Change("100");

        converted.Should().Be(200m);
    }

    [Fact]
    public void NoProviderNoCallback_HidesConvertedColumn()
    {
        var cut = RenderComponent<TmCurrencyInput>(p => p
            .Add(x => x.Currency, "USD"));

        cut.FindAll(".tm-currency-input__converted").Should().BeEmpty();
    }

    [Fact]
    public async Task InMemoryExchangeRateProvider_ReturnsSeededRate()
    {
        var provider = Rates(("USD", "CZK", 23.10m));

        var rate = await provider.GetRateAsync("USD", "CZK");

        rate.Should().Be(23.10m);
    }

    [Fact]
    public async Task InMemoryExchangeRateProvider_UnknownPair_ReturnsNull()
    {
        var provider = Rates(("USD", "CZK", 23.10m));

        var rate = await provider.GetRateAsync("EUR", "CZK");

        rate.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryExchangeRateProvider_SameCurrency_ReturnsOne()
    {
        var provider = Rates(("USD", "CZK", 23.10m));

        var rate = await provider.GetRateAsync("EUR", "EUR");

        rate.Should().Be(1m);
    }

    [Fact]
    public async Task InMemoryExchangeRateProvider_IsCaseInsensitive()
    {
        var provider = Rates(("USD", "CZK", 23.10m));

        var rate = await provider.GetRateAsync("usd", "czk");

        rate.Should().Be(23.10m);
    }
}
