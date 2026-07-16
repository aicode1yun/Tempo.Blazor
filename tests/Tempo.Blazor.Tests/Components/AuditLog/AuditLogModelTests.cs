using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.AuditLog;

/// <summary>
/// Model and provider tests for the TmAuditLogViewer stack: filtered paged queries with
/// count aggregation, filter options, timeline bucketing, and hash-chain integrity.
/// </summary>
public class AuditLogModelTests
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AuditLogEntry Entry(
        int i,
        string actor = "alice",
        string action = "document.updated",
        string entityType = "document",
        int dayOffset = 0,
        AuditLogSeverity severity = AuditLogSeverity.Info)
        => new()
        {
            Id = $"e{i}",
            Timestamp = Base.AddDays(dayOffset).AddMinutes(i),
            ActorId = actor,
            ActorName = char.ToUpperInvariant(actor[0]) + actor[1..],
            Action = action,
            EntityType = entityType,
            EntityId = $"ent-{i % 5}",
            Description = $"Change number {i}",
            Severity = severity,
            Changes = [new TmChangeInfo("Title", $"Old {i}", $"New {i}")]
        };

    private static InMemoryAuditLogProvider Provider(params AuditLogEntry[] entries)
        => new(entries);

    // ── Query: paging + count ────────────────────────────────────────────────

    [Fact]
    public async Task Query_PagesAndReportsTotalCount()
    {
        var provider = Provider(Enumerable.Range(0, 25).Select(i => Entry(i)).ToArray());

        var page = await provider.QueryAsync(new AuditLogQuery { Skip = 10, Take = 5 });

        page.TotalCount.Should().Be(25);
        page.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Query_DefaultsToNewestFirst()
    {
        var provider = Provider(Entry(1, dayOffset: 0), Entry(2, dayOffset: 2), Entry(3, dayOffset: 1));

        var page = await provider.QueryAsync(new AuditLogQuery { Take = 10 });

        page.Items.Select(e => e.Id).Should().ContainInOrder("e2", "e3", "e1");
    }

    [Fact]
    public async Task Query_FiltersByActorActionEntityTypeAndPeriod()
    {
        var provider = Provider(
            Entry(1, actor: "alice", action: "document.created", entityType: "document", dayOffset: 0),
            Entry(2, actor: "bob", action: "document.updated", entityType: "document", dayOffset: 1),
            Entry(3, actor: "alice", action: "case.closed", entityType: "case", dayOffset: 2),
            Entry(4, actor: "alice", action: "document.created", entityType: "document", dayOffset: 5));

        (await provider.QueryAsync(new AuditLogQuery { ActorId = "alice" })).TotalCount.Should().Be(3);
        (await provider.QueryAsync(new AuditLogQuery { Action = "document.created" })).TotalCount.Should().Be(2);
        (await provider.QueryAsync(new AuditLogQuery { EntityType = "case" })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new AuditLogQuery
        {
            From = Base.AddDays(1),
            To = Base.AddDays(2).AddHours(23)
        })).TotalCount.Should().Be(2);
        (await provider.QueryAsync(new AuditLogQuery
        {
            ActorId = "alice",
            Action = "document.created",
            From = Base.AddDays(3)
        })).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Query_SearchTextMatchesActorActionEntityAndDescription()
    {
        var provider = Provider(
            Entry(1, actor: "alice"),
            Entry(2, actor: "bob", action: "case.closed"),
            Entry(3, actor: "carol"));

        (await provider.QueryAsync(new AuditLogQuery { SearchText = "ALICE" })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new AuditLogQuery { SearchText = "case.closed" })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new AuditLogQuery { SearchText = "Change number 3" })).TotalCount.Should().Be(1);
        (await provider.QueryAsync(new AuditLogQuery { SearchText = "nothing-matches" })).TotalCount.Should().Be(0);
    }

    // ── Filter options ───────────────────────────────────────────────────────

    [Fact]
    public async Task FilterOptions_ReturnDistinctSortedValues()
    {
        var provider = Provider(
            Entry(1, actor: "bob", action: "b.action", entityType: "case"),
            Entry(2, actor: "alice", action: "a.action", entityType: "document"),
            Entry(3, actor: "alice", action: "b.action", entityType: "document"));

        var options = await provider.GetFilterOptionsAsync();

        options.Actors.Select(a => a.ActorId).Should().ContainInOrder("alice", "bob");
        options.Actions.Should().ContainInOrder("a.action", "b.action");
        options.EntityTypes.Should().ContainInOrder("case", "document");
    }

    // ── Timeline ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timeline_BucketsFilteredEventsAcrossRange()
    {
        var entries = Enumerable.Range(0, 40).Select(i => Entry(i, dayOffset: i % 4)).ToArray();
        var provider = Provider(entries);

        var buckets = await provider.GetTimelineAsync(new AuditLogQuery(), bucketCount: 4);

        buckets.Should().HaveCount(4);
        buckets.Sum(b => b.Count).Should().Be(40);
        buckets.Should().OnlyContain(b => b.End > b.Start);
        buckets.Should().BeInAscendingOrder(b => b.Start);
    }

    [Fact]
    public async Task Timeline_RespectsFilters()
    {
        var provider = Provider(
            Entry(1, actor: "alice", dayOffset: 0),
            Entry(2, actor: "bob", dayOffset: 1),
            Entry(3, actor: "alice", dayOffset: 2));

        var buckets = await provider.GetTimelineAsync(new AuditLogQuery { ActorId = "alice" }, bucketCount: 3);

        buckets.Sum(b => b.Count).Should().Be(2);
    }

    [Fact]
    public async Task Timeline_EmptyResult_ReturnsEmptyList()
    {
        var provider = Provider(Entry(1));

        var buckets = await provider.GetTimelineAsync(new AuditLogQuery { ActorId = "nobody" }, bucketCount: 5);

        buckets.Should().BeEmpty();
    }

    // ── Hash chain integrity ─────────────────────────────────────────────────

    [Fact]
    public void HashChain_Seal_LinksEntriesInTimestampOrder()
    {
        var entries = new[] { Entry(1, dayOffset: 0), Entry(2, dayOffset: 1), Entry(3, dayOffset: 2) };

        AuditLogHashChain.Seal(entries);

        entries[0].PreviousHash.Should().BeNull();
        entries[0].Hash.Should().NotBeNullOrEmpty();
        entries[1].PreviousHash.Should().Be(entries[0].Hash);
        entries[2].PreviousHash.Should().Be(entries[1].Hash);
    }

    [Fact]
    public void HashChain_Verify_SealedChain_IsVerified()
    {
        var entries = Enumerable.Range(0, 10).Select(i => Entry(i, dayOffset: i)).ToArray();
        AuditLogHashChain.Seal(entries);

        var result = AuditLogHashChain.Verify(entries);

        result.Status.Should().Be(AuditLogIntegrityStatus.Verified);
        result.CheckedCount.Should().Be(10);
        result.FirstInvalidEntryId.Should().BeNull();
    }

    [Fact]
    public void HashChain_Verify_TamperedEntry_FailsAndNamesFirstInvalidEntry()
    {
        var entries = Enumerable.Range(0, 10).Select(i => Entry(i, dayOffset: i)).ToArray();
        AuditLogHashChain.Seal(entries);
        entries[4].Description = "tampered";

        var result = AuditLogHashChain.Verify(entries);

        result.Status.Should().Be(AuditLogIntegrityStatus.Failed);
        result.FirstInvalidEntryId.Should().Be(entries[4].Id);
    }

    [Fact]
    public void HashChain_Verify_UnsealedEntries_ReportsUnknown()
    {
        var entries = new[] { Entry(1), Entry(2) };

        var result = AuditLogHashChain.Verify(entries);

        result.Status.Should().Be(AuditLogIntegrityStatus.Unknown);
    }

    [Fact]
    public async Task Provider_VerifyIntegrityAsync_UsesHashChain()
    {
        var entries = Enumerable.Range(0, 5).Select(i => Entry(i, dayOffset: i)).ToArray();
        AuditLogHashChain.Seal(entries);
        var provider = Provider(entries);

        var result = await provider.VerifyIntegrityAsync();

        result.Status.Should().Be(AuditLogIntegrityStatus.Verified);
        result.CheckedCount.Should().Be(5);
    }
}
