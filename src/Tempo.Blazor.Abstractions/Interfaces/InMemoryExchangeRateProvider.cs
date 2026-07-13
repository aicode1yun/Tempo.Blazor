namespace Tempo.Blazor.Interfaces;

/// <summary>
/// In-memory <see cref="IExchangeRateProvider"/> backed by a directional rate table keyed by
/// <c>(from, to)</c> currency-code pairs. Currency codes are compared case-insensitively.
/// Suitable for demos, tests, and prototypes.
/// <para>
/// Same-currency conversions (<c>from == to</c>) return <c>1</c> by default; any pair not present
/// in the table returns <c>null</c> (rate unavailable), which tells <c>TmCurrencyInput</c> to keep
/// the existing value.
/// </para>
/// </summary>
public sealed class InMemoryExchangeRateProvider : IExchangeRateProvider
{
    private readonly Dictionary<(string From, string To), decimal> _rates = new();
    private readonly bool _identityForSameCurrency;

    /// <summary>Creates a provider seeded with a directional rate table.</summary>
    /// <param name="rates">
    /// Rates keyed by <c>(from, to)</c> currency-code pairs, where the value is the multiplier to
    /// convert one unit of <c>from</c> into <c>to</c>.
    /// </param>
    /// <param name="identityForSameCurrency">
    /// When <c>true</c> (default), a conversion from a currency to itself returns <c>1</c> without a
    /// table lookup.
    /// </param>
    public InMemoryExchangeRateProvider(
        IReadOnlyDictionary<(string From, string To), decimal> rates,
        bool identityForSameCurrency = true)
    {
        ArgumentNullException.ThrowIfNull(rates);
        _identityForSameCurrency = identityForSameCurrency;
        foreach (var pair in rates)
        {
            _rates[Normalize(pair.Key.From, pair.Key.To)] = pair.Value;
        }
    }

    /// <inheritdoc />
    public Task<decimal?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency))
        {
            return Task.FromResult<decimal?>(null);
        }

        if (_identityForSameCurrency &&
            string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<decimal?>(1m);
        }

        return Task.FromResult(
            _rates.TryGetValue(Normalize(fromCurrency, toCurrency), out var rate)
                ? rate
                : (decimal?)null);
    }

    private static (string From, string To) Normalize(string from, string to)
        => (from.ToUpperInvariant(), to.ToUpperInvariant());
}
