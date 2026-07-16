using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Ledger;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Ledger;

/// <summary>
/// bUnit tests for TmLedgerGrid: money columns with running balances, filter-wide footer
/// aggregates, match-state filtering, server paging, and the row-matching workflow.
/// </summary>
public class TmLedgerGridTests : LocalizationTestBase
{
    private static LedgerEntry Entry(
        int i,
        decimal? debit = null,
        decimal? credit = null,
        LedgerMatchState state = LedgerMatchState.Unmatched,
        int dayOffset = 0)
        => new()
        {
            Id = $"L{i:D3}",
            Date = new DateOnly(2026, 1, 5).AddDays(dayOffset),
            DocumentNumber = $"DOC-{i:D3}",
            Description = $"Movement {i}",
            Debit = debit,
            Credit = credit,
            Currency = "CZK",
            MatchState = state
        };

    /// <summary>Paging/aggregation provider WITHOUT the matching capability.</summary>
    private sealed class PlainLedgerProvider : ILedgerDataProvider
    {
        private readonly InMemoryLedgerDataProvider _inner;

        public PlainLedgerProvider(params LedgerEntry[] entries) => _inner = new(entries);

        public List<LedgerQuery> Queries { get; } = [];

        public Task<LedgerPage> QueryAsync(LedgerQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return _inner.QueryAsync(query, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
            => _inner.GetCurrenciesAsync(cancellationToken);
    }

    private IRenderedComponent<TmLedgerGrid> Render(
        ILedgerDataProvider provider,
        Action<Bunit.ComponentParameterCollectionBuilder<TmLedgerGrid>>? configure = null)
        => RenderComponent<TmLedgerGrid>(p =>
        {
            p.Add(x => x.Provider, provider);
            configure?.Invoke(p);
        });

    // ── Rows, running balance, footer ────────────────────────────────────────

    [Fact]
    public void RendersRows_WithRunningBalances()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m, dayOffset: 1),
            Entry(2, credit: 30m, dayOffset: 2),
            Entry(3, debit: 50m, dayOffset: 3)
        ]);
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(3);
            var balances = cut.FindAll("[data-testid='ledger-balance'] .tm-money")
                .Select(e => e.GetAttribute("data-amount"))
                .ToList();
            balances.Should().ContainInOrder("100.00", "70.00", "120.00");
        });
    }

    [Fact]
    public void Footer_ShowsFilterWideAggregates()
    {
        var provider = new InMemoryLedgerDataProvider(
            Enumerable.Range(1, 10).Select(i => Entry(i, debit: i % 2 == 1 ? 100m : null, credit: i % 2 == 0 ? 40m : null, dayOffset: i)));
        var cut = Render(provider, p => p.Add(x => x.PageSize, 3));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='ledger-footer-debit'] .tm-money").GetAttribute("data-amount").Should().Be("500.00");
            cut.Find("[data-testid='ledger-footer-credit'] .tm-money").GetAttribute("data-amount").Should().Be("200.00");
            cut.Find("[data-testid='ledger-footer-balance'] .tm-money").GetAttribute("data-amount").Should().Be("300.00");
        });
    }

    [Fact]
    public void MixedCurrencies_RenderRunningBalancePlaceholders()
    {
        var czk = Entry(1, debit: 100m, dayOffset: 1);
        var eur = Entry(2, credit: 30m, dayOffset: 2);
        eur.Currency = "EUR";
        var cut = Render(new InMemoryLedgerDataProvider([czk, eur]));

        cut.WaitForAssertion(() =>
        {
            // A cross-currency running sum would be meaningless; both cells show the placeholder.
            var balances = cut.FindAll("[data-testid='ledger-balance'] .tm-money");
            balances.Should().HaveCount(2);
            balances.Should().OnlyContain(b => b.ClassList.Contains("tm-money--empty"));
        });
    }

    [Fact]
    public void EmptyProvider_ShowsEmptyState()
    {
        var cut = Render(new InMemoryLedgerDataProvider([]));

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-empty']").Should().HaveCount(1));
    }

    // ── Filters ──────────────────────────────────────────────────────────────

    [Fact]
    public void MatchStateFilter_QueriesProviderAndRestrictsRows()
    {
        var provider = new PlainLedgerProvider(
            Entry(1, debit: 10m, state: LedgerMatchState.Matched, dayOffset: 1),
            Entry(2, credit: 10m, state: LedgerMatchState.Unmatched, dayOffset: 2));
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(2));

        cut.Find("[data-testid='ledger-filter-state']").Change(nameof(LedgerMatchState.Matched));

        cut.WaitForAssertion(() =>
        {
            provider.Queries.Last().MatchState.Should().Be(LedgerMatchState.Matched);
            cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(1);
        });
    }

    [Fact]
    public void Search_RestrictsRows()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 10m, dayOffset: 1),
            Entry(2, credit: 10m, dayOffset: 2)
        ]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(2));

        cut.Find("[data-testid='ledger-search']").Change("Movement 1");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(1));
    }

    // ── Paging ───────────────────────────────────────────────────────────────

    [Fact]
    public void Paging_NextAndPrevious_MoveThroughThePages()
    {
        var provider = new InMemoryLedgerDataProvider(
            Enumerable.Range(1, 5).Select(i => Entry(i, debit: 1m, dayOffset: i)));
        var cut = Render(provider, p => p.Add(x => x.PageSize, 2));

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(2));

        cut.Find("[data-testid='ledger-next']").Click();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-row']")[0].TextContent.Should().Contain("DOC-003"));

        cut.Find("[data-testid='ledger-prev']").Click();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-row']")[0].TextContent.Should().Contain("DOC-001"));
    }

    // ── Matching workflow ────────────────────────────────────────────────────

    [Fact]
    public void Matching_SelectBalancedRows_AndMatch_UpdatesStates()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m, dayOffset: 1),
            Entry(2, credit: 100m, dayOffset: 2)
        ]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ledger-select']").Should().HaveCount(2));

        cut.FindAll("[data-testid='ledger-select']")[0].Change(true);
        cut.FindAll("[data-testid='ledger-select']")[1].Change(true);
        cut.Find("[data-testid='ledger-match']").Click();

        cut.WaitForAssertion(() =>
        {
            var badges = cut.FindAll("[data-testid='ledger-match-badge']");
            badges.Should().OnlyContain(b => b.ClassList.Contains("tm-ledger__badge--matched"));
        });
    }

    [Fact]
    public void Matching_UnbalancedRows_BecomePartiallyMatched()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m, dayOffset: 1),
            Entry(2, credit: 60m, dayOffset: 2)
        ]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ledger-select']").Should().HaveCount(2));

        cut.FindAll("[data-testid='ledger-select']")[0].Change(true);
        cut.FindAll("[data-testid='ledger-select']")[1].Change(true);
        cut.Find("[data-testid='ledger-match']").Click();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-match-badge']").Should()
                .OnlyContain(b => b.ClassList.Contains("tm-ledger__badge--partiallymatched")));
    }

    [Fact]
    public void MatchButton_RequiresAtLeastTwoSelectedRows()
    {
        var provider = new InMemoryLedgerDataProvider(
        [
            Entry(1, debit: 100m, dayOffset: 1),
            Entry(2, credit: 100m, dayOffset: 2)
        ]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ledger-select']").Should().HaveCount(2));

        cut.Find("[data-testid='ledger-match']").HasAttribute("disabled").Should().BeTrue();

        cut.FindAll("[data-testid='ledger-select']")[0].Change(true);
        cut.Find("[data-testid='ledger-match']").HasAttribute("disabled").Should().BeTrue();

        cut.FindAll("[data-testid='ledger-select']")[1].Change(true);
        cut.Find("[data-testid='ledger-match']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Unmatch_SelectedMatchedRows_ResetsTheirGroup()
    {
        var entries = new[]
        {
            Entry(1, debit: 100m, dayOffset: 1),
            Entry(2, credit: 100m, dayOffset: 2)
        };
        var provider = new InMemoryLedgerDataProvider(entries);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ledger-select']").Should().HaveCount(2));

        cut.FindAll("[data-testid='ledger-select']")[0].Change(true);
        cut.FindAll("[data-testid='ledger-select']")[1].Change(true);
        cut.Find("[data-testid='ledger-match']").Click();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-match-badge']").Should()
                .OnlyContain(b => b.ClassList.Contains("tm-ledger__badge--matched")));

        cut.FindAll("[data-testid='ledger-select']")[0].Change(true);
        cut.Find("[data-testid='ledger-unmatch']").Click();

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='ledger-match-badge']").Should()
                .OnlyContain(b => b.ClassList.Contains("tm-ledger__badge--unmatched")));
    }

    [Fact]
    public void MatchingUi_HiddenForProviderWithoutCapability()
    {
        var provider = new PlainLedgerProvider(Entry(1, debit: 10m));
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='ledger-row']").Should().HaveCount(1);
            cut.FindAll("[data-testid='ledger-select']").Should().BeEmpty();
            cut.FindAll("[data-testid='ledger-match']").Should().BeEmpty();
        });
    }

    [Fact]
    public void OnEntrySelected_FiresOnRowClick()
    {
        LedgerEntry? selected = null;
        var provider = new InMemoryLedgerDataProvider([Entry(7, debit: 10m)]);
        var cut = Render(provider, p => p
            .Add(x => x.OnEntrySelected, EventCallback.Factory.Create<LedgerEntry>(this, e => selected = e)));

        cut.WaitForElement("[data-testid='ledger-row']").Click();

        cut.WaitForAssertion(() =>
        {
            selected.Should().NotBeNull();
            selected!.Id.Should().Be("L007");
        });
    }
}
