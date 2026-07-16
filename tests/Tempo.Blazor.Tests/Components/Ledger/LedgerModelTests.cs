using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Ledger;

/// <summary>
/// Model tests for the ledger stack: MoneyMath rounding rules per currency,
/// InMemoryLedgerDataProvider paging with server-side aggregation, opening balances
/// for running sums, filters, and the matching capability.
/// </summary>
public class LedgerModelTests
{
    private static LedgerEntry Entry(
        int i,
        decimal? debit = null,
        decimal? credit = null,
        string currency = "CZK",
        LedgerMatchState state = LedgerMatchState.Unmatched,
        int dayOffset = 0,
        string? description = null)
        => new()
        {
            Id = $"L{i:D3}",
            Date = new DateOnly(2026, 1, 5).AddDays(dayOffset),
            DocumentNumber = $"DOC-{i:D3}",
            Description = description ?? $"Movement {i}",
            Debit = debit,
            Credit = credit,
            Currency = currency,
            MatchState = state
        };

    // ── MoneyMath: currency decimals + rounding rules ────────────────────────

    [Theory]
    [InlineData("CZK", 2)]
    [InlineData("EUR", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KWD", 3)]
    [InlineData("XYZ", 2)] // unknown → default 2
    public void CurrencyDecimals_FollowIso4217MinorUnits(string currency, int expected)
        => MoneyMath.GetCurrencyDecimals(currency).Should().Be(expected);

    [Fact]
    public void Round_UsesBankersRoundingByDefault()
    {
        MoneyMath.Round(2.345m, "CZK").Should().Be(2.34m);  // ties to even
        MoneyMath.Round(2.355m, "CZK").Should().Be(2.36m);
    }

    [Fact]
    public void Round_AwayFromZero_WhenRequested()
    {
        MoneyMath.Round(2.345m, "CZK", MidpointRounding.AwayFromZero).Should().Be(2.35m);
        MoneyMath.Round(-2.345m, "CZK", MidpointRounding.AwayFromZero).Should().Be(-2.35m);
    }

    [Fact]
    public void Round_RespectsZeroDecimalCurrencies()
    {
        MoneyMath.Round(1234.56m, "JPY").Should().Be(1235m);
    }

    // ── Provider: paging + server-side aggregation ───────────────────────────

    [Fact]
    public async Task Query_PagesAndAggregatesWholeFilteredSet()
    {
        var provider = new InMemoryLedgerDataProvider(
            Enumerable.Range(1, 30).Select(i => Entry(i, debit: i % 2 == 1 ? 100m : null, credit: i % 2 == 0 ? 100m : null, dayOffset: i)));

        var page = await provider.QueryAsync(new LedgerQuery { Skip = 10, Take = 5 });

        page.TotalCount.Should().Be(30);
        page.Items.Should().HaveCount(5);
        page.Aggregates.DebitTotal.Should().Be(1500m);   // 15 odd rows × 100
        page.Aggregates.CreditTotal.Should().Be(1500m);  // 15 even rows × 100
        page.Aggregates.Balance.Should().Be(0m);
        page.Aggregates.Count.Should().Be(30);
    }

    [Fact]
    public async Task Query_OpeningBalance_SumsRowsBeforeThePage()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m, dayOffset: 1),
            Entry(2, credit: 30m, dayOffset: 2),
            Entry(3, debit: 50m, dayOffset: 3),
            Entry(4, credit: 20m, dayOffset: 4)
        ]);

        var page = await provider.QueryAsync(new LedgerQuery { Skip = 2, Take = 2 });

        // Rows before the page: +100 (debit) − 30 (credit) = 70.
        page.OpeningBalance.Should().Be(70m);
        page.Items[0].Id.Should().Be("L003");
    }

    [Fact]
    public async Task Query_OrdersAscendingByDateThenId_ForRunningBalances()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(2, debit: 1m, dayOffset: 5),
            Entry(1, debit: 1m, dayOffset: 1),
            Entry(3, debit: 1m, dayOffset: 5)
        ]);

        var page = await provider.QueryAsync(new LedgerQuery());

        page.Items.Select(e => e.Id).Should().ContainInOrder("L001", "L002", "L003");
    }

    [Fact]
    public async Task Query_Filters_ByMatchStateCurrencySearchAndPeriod()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 10m, state: LedgerMatchState.Matched),
            Entry(2, credit: 10m, state: LedgerMatchState.Unmatched, description: "Refund for Novák"),
            Entry(3, debit: 5m, currency: "EUR", dayOffset: 10)
        ]);

        (await provider.QueryAsync(new LedgerQuery { MatchState = LedgerMatchState.Matched })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new LedgerQuery { Currency = "EUR" })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new LedgerQuery { SearchText = "novák" })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new LedgerQuery { From = new DateOnly(2026, 1, 10) })).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Aggregates_AreRoundedPerCurrency()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 0.005m),
            Entry(2, debit: 0.005m)
        ]);

        var page = await provider.QueryAsync(new LedgerQuery());

        // Sum first, round once: 0.01 — not 0.00 from rounding each row.
        page.Aggregates.DebitTotal.Should().Be(0.01m);
    }

    [Fact]
    public async Task GetCurrencies_ReturnsDistinctSortedCodes()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 1m, currency: "EUR"),
            Entry(2, debit: 1m, currency: "CZK"),
            Entry(3, debit: 1m, currency: "EUR")
        ]);

        (await provider.GetCurrenciesAsync()).Should().ContainInOrder("CZK", "EUR");
    }

    // ── Matching capability ──────────────────────────────────────────────────

    [Fact]
    public async Task Match_BalancedSelection_BecomesMatched()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m),
            Entry(2, credit: 100m)
        ]);

        var result = await provider.MatchAsync(["L001", "L002"]);

        result.State.Should().Be(LedgerMatchState.Matched);
        result.MatchGroupId.Should().NotBeNullOrEmpty();
        var page = await provider.QueryAsync(new LedgerQuery());
        page.Items.Should().OnlyContain(e => e.MatchState == LedgerMatchState.Matched);
        page.Items.Should().OnlyContain(e => e.MatchGroupId == result.MatchGroupId);
    }

    [Fact]
    public async Task Match_UnbalancedSelection_BecomesPartiallyMatched()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m),
            Entry(2, credit: 60m)
        ]);

        var result = await provider.MatchAsync(["L001", "L002"]);

        result.State.Should().Be(LedgerMatchState.PartiallyMatched);
    }

    [Fact]
    public async Task Match_StealingFromExistingGroup_ReevaluatesTheOldGroup()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m),
            Entry(2, credit: 100m),
            Entry(3, credit: 100m)
        ]);
        var first = await provider.MatchAsync(["L001", "L002"]);
        first.State.Should().Be(LedgerMatchState.Matched);

        // Re-match the invoice with a different payment; the old group loses a member.
        await provider.MatchAsync(["L001", "L003"]);

        var page = await provider.QueryAsync(new LedgerQuery());
        var orphan = page.Items.Single(e => e.Id == "L002");
        orphan.MatchState.Should().Be(LedgerMatchState.Unmatched);
        orphan.MatchGroupId.Should().BeNull();
    }

    [Fact]
    public async Task Match_CrossCurrencyEqualAmounts_IsOnlyPartiallyMatched()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 5000m, currency: "CZK"),
            Entry(2, credit: 5000m, currency: "EUR")
        ]);

        var result = await provider.MatchAsync(["L001", "L002"]);

        result.State.Should().Be(LedgerMatchState.PartiallyMatched);
    }

    [Fact]
    public async Task Unmatch_ResetsGroupToUnmatched()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m),
            Entry(2, credit: 100m)
        ]);
        var result = await provider.MatchAsync(["L001", "L002"]);

        await provider.UnmatchAsync(result.MatchGroupId!);

        var page = await provider.QueryAsync(new LedgerQuery());
        page.Items.Should().OnlyContain(e => e.MatchState == LedgerMatchState.Unmatched && e.MatchGroupId == null);
    }
}
