using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.AuditLog;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.AuditLog;

/// <summary>
/// bUnit tests for TmAuditLogViewer: provider-backed rows, filters (actor/action/entity/period),
/// search, timeline buckets, detail expansion with TmChangeDiff, CSV export dispatch, hash-chain
/// integrity widget, and the virtualized render path.
/// </summary>
public class TmAuditLogViewerTests : LocalizationTestBase
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AuditLogEntry Entry(
        int i,
        string actor = "alice",
        string action = "document.updated",
        string entityType = "document",
        int dayOffset = 0)
        => new()
        {
            Id = $"e{i}",
            Timestamp = Base.AddDays(dayOffset).AddMinutes(i),
            ActorId = actor,
            ActorName = char.ToUpperInvariant(actor[0]) + actor[1..],
            Action = action,
            EntityType = entityType,
            EntityId = $"ent-{i}",
            Description = $"Change number {i}",
            Changes = [new TmChangeInfo("Title", $"Old {i}", $"New {i}")]
        };

    /// <summary>Provider wrapper without the integrity marker interface, recording queries.</summary>
    private sealed class RecordingAuditLogProvider : IAuditLogProvider
    {
        private readonly InMemoryAuditLogProvider _inner;

        public RecordingAuditLogProvider(params AuditLogEntry[] entries) => _inner = new(entries);

        public List<AuditLogQuery> Queries { get; } = [];

        public Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return _inner.QueryAsync(query, cancellationToken);
        }

        public Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
            => _inner.GetFilterOptionsAsync(cancellationToken);

        public Task<IReadOnlyList<AuditLogTimelineBucket>> GetTimelineAsync(AuditLogQuery query, int bucketCount, CancellationToken cancellationToken = default)
            => _inner.GetTimelineAsync(query, bucketCount, cancellationToken);
    }

    private IRenderedComponent<TmAuditLogViewer> Render(
        IAuditLogProvider provider,
        Action<Bunit.ComponentParameterCollectionBuilder<TmAuditLogViewer>>? configure = null)
        => RenderComponent<TmAuditLogViewer>(p =>
        {
            p.Add(x => x.Provider, provider);
            p.Add(x => x.Virtualized, false);
            configure?.Invoke(p);
        });

    // ── Rows & count ─────────────────────────────────────────────────────────

    [Fact]
    public void RendersRowsAndTotalCountFromProvider()
    {
        var provider = new InMemoryAuditLogProvider([Entry(1), Entry(2), Entry(3)]);
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(3);
            cut.Find("[data-testid='audit-log-count']").TextContent.Should().Contain("3");
        });
    }

    [Fact]
    public void EmptyProvider_ShowsEmptyState()
    {
        var cut = Render(new InMemoryAuditLogProvider([]));

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='audit-log-empty']").Should().HaveCount(1));
    }

    // ── Filters ──────────────────────────────────────────────────────────────

    [Fact]
    public void ActionFilter_RestrictsRows()
    {
        var provider = new InMemoryAuditLogProvider(
        [
            Entry(1, action: "document.created"),
            Entry(2, action: "document.updated"),
            Entry(3, action: "document.created")
        ]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(3));

        cut.Find("[data-testid='audit-log-filter-action']").Change("document.created");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));
    }

    [Fact]
    public void ActorFilter_PassesActorIdToProviderQuery()
    {
        var provider = new RecordingAuditLogProvider(Entry(1, actor: "alice"), Entry(2, actor: "bob"));
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));

        cut.Find("[data-testid='audit-log-filter-actor']").Change("bob");

        cut.WaitForAssertion(() =>
        {
            provider.Queries.Last().ActorId.Should().Be("bob");
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1);
        });
    }

    [Fact]
    public void PeriodFilter_PassesFromAndToToProviderQuery()
    {
        var provider = new RecordingAuditLogProvider(Entry(1, dayOffset: 0), Entry(2, dayOffset: 10));
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));

        cut.Find("[data-testid='audit-log-from']").Change("2026-01-05");
        cut.Find("[data-testid='audit-log-to']").Change("2026-01-15");

        cut.WaitForAssertion(() =>
        {
            var query = provider.Queries.Last();
            query.From.Should().NotBeNull();
            query.From!.Value.Date.Should().Be(new DateTime(2026, 1, 5));
            query.To.Should().NotBeNull();
            query.To!.Value.Date.Should().Be(new DateTime(2026, 1, 15));
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1);
        });
    }

    [Fact]
    public void Search_FiltersRows()
    {
        var provider = new InMemoryAuditLogProvider([Entry(1, actor: "alice"), Entry(2, actor: "bob")]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));

        cut.Find("[data-testid='audit-log-search']").Change("bob");

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1));
    }

    [Fact]
    public void ClearFilters_RestoresFullResultSet()
    {
        var provider = new InMemoryAuditLogProvider([Entry(1, actor: "alice"), Entry(2, actor: "bob")]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));

        cut.Find("[data-testid='audit-log-search']").Change("bob");
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1));

        cut.Find("[data-testid='audit-log-clear-filters']").Click();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));
    }

    // ── Timeline ─────────────────────────────────────────────────────────────

    [Fact]
    public void Timeline_RendersBuckets_AndBucketClickAppliesPeriodFilter()
    {
        var entries = Enumerable.Range(0, 20).Select(i => Entry(i, dayOffset: i % 10)).ToArray();
        var provider = new RecordingAuditLogProvider(entries);
        var cut = Render(provider);

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='audit-log-timeline-bucket']").Count.Should().BeGreaterThan(1));

        cut.FindAll("[data-testid='audit-log-timeline-bucket']")[0].Click();

        cut.WaitForAssertion(() =>
        {
            var query = provider.Queries.Last();
            query.From.Should().NotBeNull();
            query.To.Should().NotBeNull();
        });
    }

    [Fact]
    public void Timeline_CanBeHidden()
    {
        var cut = Render(new InMemoryAuditLogProvider([Entry(1)]), p => p.Add(x => x.ShowTimeline, false));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1);
            cut.FindAll("[data-testid='audit-log-timeline']").Should().BeEmpty();
        });
    }

    // ── Detail with TmChangeDiff ─────────────────────────────────────────────

    [Fact]
    public void RowClick_ExpandsDetailWithChangeDiff()
    {
        var provider = new InMemoryAuditLogProvider([Entry(7)]);
        AuditLogEntry? selected = null;
        var cut = Render(provider, p => p
            .Add(x => x.OnEntrySelected, EventCallback.Factory.Create<AuditLogEntry>(this, e => selected = e)));

        cut.WaitForElement("[data-testid='audit-log-row']").Click();

        cut.WaitForAssertion(() =>
        {
            var detail = cut.Find("[data-testid='audit-log-detail']");
            detail.QuerySelectorAll(".tm-change-diff-row").Should().HaveCount(1);
            detail.TextContent.Should().Contain("Old 7").And.Contain("New 7");
            selected.Should().NotBeNull();
            selected!.Id.Should().Be("e7");
        });

        // Clicking again collapses.
        cut.Find("[data-testid='audit-log-row']").Click();
        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='audit-log-detail']").Should().BeEmpty());
    }

    // ── CSV export ───────────────────────────────────────────────────────────

    [Fact]
    public void Export_InvokesDownloadWithCsvContent()
    {
        var handler = JSInterop.SetupVoid("tmDataTable.downloadFile", _ => true).SetVoidResult();
        var provider = new InMemoryAuditLogProvider([Entry(1, actor: "alice"), Entry(2, actor: "bob")]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));

        cut.Find("[data-testid='audit-log-export']").Click();

        cut.WaitForAssertion(() =>
        {
            handler.Invocations.Should().NotBeEmpty();
            var args = handler.Invocations.Last().Arguments;
            args[0].Should().Be("audit-log.csv");
            args[1].Should().Be("text/csv");
            var csv = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String((string)args[2]!));
            csv.Should().Contain("Timestamp").And.Contain("Alice").And.Contain("Bob").And.Contain("document.updated");
        });
    }

    [Fact]
    public void Export_RespectsActiveFilter()
    {
        var handler = JSInterop.SetupVoid("tmDataTable.downloadFile", _ => true).SetVoidResult();
        var provider = new InMemoryAuditLogProvider([Entry(1, actor: "alice"), Entry(2, actor: "bob")]);
        var cut = Render(provider);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(2));

        cut.Find("[data-testid='audit-log-search']").Change("alice");
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1));

        cut.Find("[data-testid='audit-log-export']").Click();

        cut.WaitForAssertion(() =>
        {
            var csv = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String((string)handler.Invocations.Last().Arguments[2]!));
            csv.Should().Contain("Alice").And.NotContain("Bob");
        });
    }

    // ── Integrity widget ─────────────────────────────────────────────────────

    [Fact]
    public void Integrity_VerifiedChain_ShowsVerifiedBadge()
    {
        var entries = Enumerable.Range(0, 5).Select(i => Entry(i, dayOffset: i)).ToArray();
        AuditLogHashChain.Seal(entries);
        var cut = Render(new InMemoryAuditLogProvider(entries));

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("[data-testid='audit-log-integrity']");
            badge.ClassList.Should().Contain("tm-audit-log__integrity--verified");
        });
    }

    [Fact]
    public void Integrity_TamperedChain_ShowsFailedBadge()
    {
        var entries = Enumerable.Range(0, 5).Select(i => Entry(i, dayOffset: i)).ToArray();
        AuditLogHashChain.Seal(entries);
        entries[2].Description = "tampered";
        var cut = Render(new InMemoryAuditLogProvider(entries));

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("[data-testid='audit-log-integrity']");
            badge.ClassList.Should().Contain("tm-audit-log__integrity--failed");
        });
    }

    [Fact]
    public void Integrity_HiddenForProviderWithoutIntegritySupport()
    {
        var cut = Render(new RecordingAuditLogProvider(Entry(1)));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='audit-log-row']").Should().HaveCount(1);
            cut.FindAll("[data-testid='audit-log-integrity']").Should().BeEmpty();
        });
    }

    // ── Virtualized path ─────────────────────────────────────────────────────

    [Fact]
    public void Virtualized_RendersWithoutCrashing()
    {
        var provider = new InMemoryAuditLogProvider(
            Enumerable.Range(0, 500).Select(i => Entry(i, dayOffset: i % 30)).ToArray());

        var act = () => RenderComponent<TmAuditLogViewer>(p => p
            .Add(x => x.Provider, provider)
            .Add(x => x.Virtualized, true));

        act.Should().NotThrow();
    }
}
