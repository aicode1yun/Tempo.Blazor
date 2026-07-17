namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Match state of a ledger entry.</summary>
public enum LedgerMatchState
{
    /// <summary>Not paired with any other entry.</summary>
    Unmatched = 0,

    /// <summary>Paired in a balanced group (debits equal credits).</summary>
    Matched = 1,

    /// <summary>Paired in a group whose sides do not balance.</summary>
    PartiallyMatched = 2,

    /// <summary>Flagged as disputed.</summary>
    Disputed = 3
}

/// <summary>Single ledger movement. Exactly one of <see cref="Debit"/>/<see cref="Credit"/>
/// is typically set; both use exact <see cref="decimal"/> arithmetic.</summary>
public sealed class LedgerEntry
{
    /// <summary>Stable entry identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Booking date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Source document number, when any.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Movement description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional counterparty display name.</summary>
    public string? Counterparty { get; set; }

    /// <summary>Debit amount, when the movement is a debit.</summary>
    public decimal? Debit { get; set; }

    /// <summary>Credit amount, when the movement is a credit.</summary>
    public decimal? Credit { get; set; }

    /// <summary>ISO 4217 currency code. Default is "CZK".</summary>
    public string Currency { get; set; } = "CZK";

    /// <summary>Match state. Default is <see cref="LedgerMatchState.Unmatched"/>.</summary>
    public LedgerMatchState MatchState { get; set; } = LedgerMatchState.Unmatched;

    /// <summary>Identifier of the match group this entry belongs to, when matched.</summary>
    public string? MatchGroupId { get; set; }

    /// <summary>Free-form metadata.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>Signed effect of the movement on the balance (debit − credit).</summary>
    public decimal SignedAmount => (Debit ?? 0m) - (Credit ?? 0m);
}

/// <summary>Filtered, paged ledger query. Results are ordered ascending by date then id,
/// so running balances are well-defined.</summary>
public sealed class LedgerQuery
{
    /// <summary>Number of matching entries to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of entries to return. Default is 100.</summary>
    public int Take { get; set; } = 100;

    /// <summary>Case-insensitive text matched against document number, description, and counterparty.</summary>
    public string? SearchText { get; set; }

    /// <summary>Restrict to a single match state.</summary>
    public LedgerMatchState? MatchState { get; set; }

    /// <summary>Restrict to a single currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>Inclusive lower bound of the booking date.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Inclusive upper bound of the booking date.</summary>
    public DateOnly? To { get; set; }
}

/// <summary>Server-side aggregates of the whole filtered set (not just one page).</summary>
public sealed class LedgerAggregates
{
    /// <summary>Sum of all debit amounts of the filtered set.</summary>
    public decimal DebitTotal { get; set; }

    /// <summary>Sum of all credit amounts of the filtered set.</summary>
    public decimal CreditTotal { get; set; }

    /// <summary>Debit total minus credit total.</summary>
    public decimal Balance { get; set; }

    /// <summary>Number of entries in the filtered set.</summary>
    public long Count { get; set; }
}

/// <summary>One page of ledger results with aggregates and the opening balance
/// (sum of all filtered rows BEFORE the page) for running-balance rendering.</summary>
public sealed class LedgerPage
{
    /// <summary>Entries of the requested page, ordered ascending by date then id.</summary>
    public IReadOnlyList<LedgerEntry> Items { get; set; } = [];

    /// <summary>Total number of entries matching the query filter.</summary>
    public long TotalCount { get; set; }

    /// <summary>Aggregates of the whole filtered set.</summary>
    public LedgerAggregates Aggregates { get; set; } = new();

    /// <summary>Signed sum (debit − credit) of all filtered rows before this page.</summary>
    public decimal OpeningBalance { get; set; }
}

/// <summary>Result of matching a set of ledger entries into one group.</summary>
public sealed class LedgerMatchResult
{
    /// <summary>Identifier of the created match group.</summary>
    public string? MatchGroupId { get; set; }

    /// <summary>Resulting state: Matched when the sides balance, otherwise PartiallyMatched.</summary>
    public LedgerMatchState State { get; set; }

    /// <summary>Ids of the entries included in the group.</summary>
    public IReadOnlyList<string> EntryIds { get; set; } = [];
}

/// <summary>Data source contract of TmLedgerGrid: filtered paged queries with server-side
/// aggregation and opening balances.</summary>
public interface ILedgerDataProvider
{
    /// <summary>Runs a filtered, paged query with aggregates over the whole filtered set.</summary>
    /// <param name="query">Filter and paging.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<LedgerPage> QueryAsync(LedgerQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns the distinct currency codes present in the ledger.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<string>> GetCurrenciesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Marker capability interface: providers that can pair entries into match
/// groups. TmLedgerGrid shows the matching UI only when its provider implements this.</summary>
public interface ILedgerMatchingProvider : ILedgerDataProvider
{
    /// <summary>Pairs the given entries into one match group. The provider decides the
    /// resulting state (Matched when debits equal credits, otherwise PartiallyMatched).</summary>
    /// <param name="entryIds">Ids of the entries to pair.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<LedgerMatchResult> MatchAsync(IReadOnlyList<string> entryIds, CancellationToken cancellationToken = default);

    /// <summary>Dissolves a match group, resetting its entries to unmatched.</summary>
    /// <param name="matchGroupId">Identifier of the group to dissolve.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task UnmatchAsync(string matchGroupId, CancellationToken cancellationToken = default);
}

/// <summary>Exact money arithmetic helpers: ISO 4217 minor-unit decimals and rounding.</summary>
public static class MoneyMath
{
    private static readonly Dictionary<string, int> _minorUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        // Zero-decimal currencies.
        ["JPY"] = 0, ["KRW"] = 0, ["VND"] = 0, ["CLP"] = 0, ["ISK"] = 0,
        ["PYG"] = 0, ["RWF"] = 0, ["UGX"] = 0, ["XAF"] = 0, ["XOF"] = 0, ["XPF"] = 0,
        // Three-decimal currencies.
        ["BHD"] = 3, ["IQD"] = 3, ["JOD"] = 3, ["KWD"] = 3, ["LYD"] = 3, ["OMR"] = 3, ["TND"] = 3
    };

    /// <summary>Returns the ISO 4217 minor-unit count of a currency. Unknown codes default to 2.</summary>
    /// <param name="currency">ISO 4217 currency code.</param>
    public static int GetCurrencyDecimals(string? currency)
        => currency is not null && _minorUnits.TryGetValue(currency, out var decimals) ? decimals : 2;

    /// <summary>Rounds an amount to the currency's minor units.</summary>
    /// <param name="amount">Amount to round.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <param name="mode">Midpoint rounding mode. Default is banker's rounding (<see cref="MidpointRounding.ToEven"/>).</param>
    public static decimal Round(decimal amount, string? currency, MidpointRounding mode = MidpointRounding.ToEven)
        => Math.Round(amount, GetCurrencyDecimals(currency), mode);
}

/// <summary>
/// In-memory <see cref="ILedgerDataProvider"/> and <see cref="ILedgerMatchingProvider"/>
/// over a fixed entry list. Aggregates sum exact decimals first and round once per
/// currency. Suitable for demos, tests, and small ledgers.
/// </summary>
public sealed class InMemoryLedgerDataProvider : ILedgerDataProvider, ILedgerMatchingProvider
{
    private readonly List<LedgerEntry> _entries;
    private readonly MidpointRounding _rounding;

    /// <summary>Creates a provider over the given entries.</summary>
    /// <param name="entries">Ledger entries.</param>
    /// <param name="rounding">Rounding mode applied to aggregates. Default is banker's rounding.</param>
    public InMemoryLedgerDataProvider(IEnumerable<LedgerEntry> entries, MidpointRounding rounding = MidpointRounding.ToEven)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
        _rounding = rounding;
    }

    /// <inheritdoc />
    public Task<LedgerPage> QueryAsync(LedgerQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = Filter(query)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();

        var skip = Math.Max(0, query.Skip);
        var items = filtered.Skip(skip).Take(Math.Max(0, query.Take)).ToList();

        // Rounding decimals come from the set's single currency; a mixed-currency set has
        // no meaningful minor-unit count, so it falls back to the two-decimal default.
        var distinctCurrencies = filtered
            .Select(e => e.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        var currency = query.Currency ?? (distinctCurrencies.Count == 1 ? distinctCurrencies[0] : null);

        var debitTotal = MoneyMath.Round(filtered.Sum(e => e.Debit ?? 0m), currency, _rounding);
        var creditTotal = MoneyMath.Round(filtered.Sum(e => e.Credit ?? 0m), currency, _rounding);

        return Task.FromResult(new LedgerPage
        {
            Items = items,
            TotalCount = filtered.Count,
            OpeningBalance = MoneyMath.Round(filtered.Take(skip).Sum(e => e.SignedAmount), currency, _rounding),
            Aggregates = new LedgerAggregates
            {
                DebitTotal = debitTotal,
                CreditTotal = creditTotal,
                Balance = debitTotal - creditTotal,
                Count = filtered.Count
            }
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(
            _entries.Select(e => e.Currency)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList());

    /// <inheritdoc />
    public Task<LedgerMatchResult> MatchAsync(IReadOnlyList<string> entryIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryIds);

        var entries = _entries.Where(e => entryIds.Contains(e.Id, StringComparer.Ordinal)).ToList();
        if (entries.Count == 0)
        {
            return Task.FromResult(new LedgerMatchResult { State = LedgerMatchState.Unmatched, EntryIds = [] });
        }

        // Entries stolen from existing groups leave those groups behind: re-evaluate each
        // affected group afterwards so no dangling member keeps a stale Matched state.
        var affectedGroups = entries
            .Where(e => !string.IsNullOrEmpty(e.MatchGroupId))
            .Select(e => e.MatchGroupId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var groupId = Guid.NewGuid().ToString("N");
        var state = EvaluateGroupState(entries);

        foreach (var entry in entries)
        {
            entry.MatchGroupId = groupId;
            entry.MatchState = state;
        }

        foreach (var affected in affectedGroups)
        {
            ReevaluateGroup(affected);
        }

        return Task.FromResult(new LedgerMatchResult
        {
            MatchGroupId = groupId,
            State = state,
            EntryIds = entries.Select(e => e.Id).ToList()
        });
    }

    /// <inheritdoc />
    public Task UnmatchAsync(string matchGroupId, CancellationToken cancellationToken = default)
    {
        foreach (var entry in _entries.Where(e => string.Equals(e.MatchGroupId, matchGroupId, StringComparison.Ordinal)))
        {
            entry.MatchGroupId = null;
            entry.MatchState = LedgerMatchState.Unmatched;
        }

        return Task.CompletedTask;
    }

    // A group is balanced only when every member shares one currency and debits equal credits.
    private static LedgerMatchState EvaluateGroupState(IReadOnlyList<LedgerEntry> entries)
    {
        var singleCurrency = entries
            .Select(e => e.Currency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 1;

        return singleCurrency && entries.Sum(e => e.Debit ?? 0m) == entries.Sum(e => e.Credit ?? 0m)
            ? LedgerMatchState.Matched
            : LedgerMatchState.PartiallyMatched;
    }

    private void ReevaluateGroup(string groupId)
    {
        var members = _entries
            .Where(e => string.Equals(e.MatchGroupId, groupId, StringComparison.Ordinal))
            .ToList();

        if (members.Count <= 1)
        {
            foreach (var member in members)
            {
                member.MatchGroupId = null;
                member.MatchState = LedgerMatchState.Unmatched;
            }

            return;
        }

        var state = EvaluateGroupState(members);
        foreach (var member in members)
        {
            member.MatchState = state;
        }
    }

    private IEnumerable<LedgerEntry> Filter(LedgerQuery query)
    {
        IEnumerable<LedgerEntry> result = _entries;

        if (query.MatchState is not null)
        {
            result = result.Where(e => e.MatchState == query.MatchState);
        }

        if (!string.IsNullOrEmpty(query.Currency))
        {
            result = result.Where(e => string.Equals(e.Currency, query.Currency, StringComparison.OrdinalIgnoreCase));
        }

        if (query.From is not null)
        {
            result = result.Where(e => e.Date >= query.From.Value);
        }

        if (query.To is not null)
        {
            result = result.Where(e => e.Date <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var text = query.SearchText.Trim();
            result = result.Where(e =>
                Contains(e.DocumentNumber, text)
                || Contains(e.Description, text)
                || Contains(e.Counterparty, text));
        }

        return result;
    }

    private static bool Contains(string? value, string text)
        => value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
