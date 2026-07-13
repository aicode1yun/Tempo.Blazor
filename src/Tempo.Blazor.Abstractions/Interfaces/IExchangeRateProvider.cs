namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Optional async provider that supplies foreign-exchange rates to <c>TmCurrencyInput</c>.
/// <para>
/// When a provider is supplied, the component fetches the rate for a conversion via
/// <see cref="GetRateAsync"/> and multiplies the entered amount by it. When no provider is
/// supplied, the component falls back to the existing callback-based <c>ConvertAsync</c> FX,
/// so existing consumers keep working unchanged.
/// </para>
/// <para>
/// For a ready-made implementation seeded from an in-memory rate table, use
/// <see cref="InMemoryExchangeRateProvider"/>.
/// </para>
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Returns the exchange rate to convert one unit of <paramref name="fromCurrency"/> into
    /// <paramref name="toCurrency"/> (i.e. <c>toAmount = fromAmount * rate</c>).
    /// </summary>
    /// <param name="fromCurrency">Source currency code (e.g. <c>"USD"</c>).</param>
    /// <param name="toCurrency">Target currency code (e.g. <c>"CZK"</c>).</param>
    /// <param name="ct">Token used to cancel the operation.</param>
    /// <returns>
    /// The rate, or <c>null</c> when the rate is unavailable. A <c>null</c> result signals the
    /// consumer to keep the existing value and not convert (rather than throwing or zeroing it out).
    /// </returns>
    Task<decimal?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default);
}
