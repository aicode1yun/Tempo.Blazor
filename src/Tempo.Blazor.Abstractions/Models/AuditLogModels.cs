using System.Security.Cryptography;
using System.Text;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Severity of an audit log entry.</summary>
public enum AuditLogSeverity
{
    /// <summary>Routine change.</summary>
    Info = 0,

    /// <summary>Noteworthy change that may need review.</summary>
    Warning = 1,

    /// <summary>Security-relevant or destructive change.</summary>
    Critical = 2
}

/// <summary>Single immutable audit event. Domain-neutral: actions, entity types and
/// metadata are free-form strings owned by the host application.</summary>
public sealed class AuditLogEntry
{
    /// <summary>Stable entry identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>When the event happened.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Stable identifier of the acting user or system.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Displayed actor name.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Optional actor role at the time of the event.</summary>
    public string? ActorRole { get; set; }

    /// <summary>Stable action key (e.g. "document.updated").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Optional display label for the action. Falls back to <see cref="Action"/>.</summary>
    public string? ActionLabel { get; set; }

    /// <summary>Type of the affected entity (e.g. "document").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identifier of the affected entity, when any.</summary>
    public string? EntityId { get; set; }

    /// <summary>Optional display label of the affected entity.</summary>
    public string? EntityLabel { get; set; }

    /// <summary>Optional human-readable summary of the event.</summary>
    public string? Description { get; set; }

    /// <summary>Severity. Default is <see cref="AuditLogSeverity.Info"/>.</summary>
    public AuditLogSeverity Severity { get; set; } = AuditLogSeverity.Info;

    /// <summary>Property-level changes carried by the event, rendered with TmChangeDiff.</summary>
    public List<TmChangeInfo> Changes { get; set; } = [];

    /// <summary>Free-form metadata (correlation id, request id, …).</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>Optional source IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Hash of this entry in a tamper-evident chain, when the log is sealed.</summary>
    public string? Hash { get; set; }

    /// <summary>Hash of the previous entry in the chain, when the log is sealed.</summary>
    public string? PreviousHash { get; set; }
}

/// <summary>Filtered, paged audit log query.</summary>
public sealed class AuditLogQuery
{
    /// <summary>Number of matching entries to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Maximum number of entries to return. Default is 100.</summary>
    public int Take { get; set; } = 100;

    /// <summary>Restrict to a single actor id.</summary>
    public string? ActorId { get; set; }

    /// <summary>Restrict to a single action key.</summary>
    public string? Action { get; set; }

    /// <summary>Restrict to a single entity type.</summary>
    public string? EntityType { get; set; }

    /// <summary>Restrict to a single entity id.</summary>
    public string? EntityId { get; set; }

    /// <summary>Inclusive lower bound of the period.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Inclusive upper bound of the period.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Case-insensitive text matched against actor, action, entity, and description.</summary>
    public string? SearchText { get; set; }

    /// <summary>Whether results are ordered newest first. Default is true.</summary>
    public bool Descending { get; set; } = true;
}

/// <summary>One page of audit log results with the total count of the filtered set.</summary>
public sealed class AuditLogPage
{
    /// <summary>Entries of the requested page.</summary>
    public IReadOnlyList<AuditLogEntry> Items { get; set; } = [];

    /// <summary>Total number of entries matching the query filter.</summary>
    public long TotalCount { get; set; }
}

/// <summary>Actor available in the audit log filter.</summary>
public sealed class AuditLogActorOption
{
    /// <summary>Stable actor identifier.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Displayed actor name.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>Distinct values available for the audit log filters.</summary>
public sealed class AuditLogFilterOptions
{
    /// <summary>Known actors, sorted by id.</summary>
    public IReadOnlyList<AuditLogActorOption> Actors { get; set; } = [];

    /// <summary>Known action keys, sorted.</summary>
    public IReadOnlyList<string> Actions { get; set; } = [];

    /// <summary>Known entity types, sorted.</summary>
    public IReadOnlyList<string> EntityTypes { get; set; } = [];
}

/// <summary>Event count aggregated over one slice of the queried period.</summary>
public sealed class AuditLogTimelineBucket
{
    /// <summary>Inclusive bucket start.</summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>Exclusive bucket end.</summary>
    public DateTimeOffset End { get; set; }

    /// <summary>Number of events in the bucket.</summary>
    public long Count { get; set; }
}

/// <summary>Result of a hash-chain integrity verification.</summary>
public enum AuditLogIntegrityStatus
{
    /// <summary>The log carries no hash chain to verify.</summary>
    Unknown = 0,

    /// <summary>Every entry links correctly to its predecessor.</summary>
    Verified = 1,

    /// <summary>At least one entry does not match the chain.</summary>
    Failed = 2
}

/// <summary>Outcome of an audit log integrity verification.</summary>
public sealed class AuditLogIntegrityResult
{
    /// <summary>Verification status.</summary>
    public AuditLogIntegrityStatus Status { get; set; }

    /// <summary>Number of entries checked.</summary>
    public long CheckedCount { get; set; }

    /// <summary>Id of the first entry that broke the chain, when failed.</summary>
    public string? FirstInvalidEntryId { get; set; }

    /// <summary>Optional provider message.</summary>
    public string? Message { get; set; }
}

/// <summary>Data source contract of TmAuditLogViewer: filtered paged queries with count
/// aggregation, filter facets, and timeline bucketing. Implementations back the viewer with
/// a database, API, or an in-memory store.</summary>
public interface IAuditLogProvider
{
    /// <summary>Runs a filtered, paged query and reports the total count of the filtered set.</summary>
    /// <param name="query">Filter, paging, and ordering.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns the distinct values available for the filters.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Aggregates event counts of the filtered set into evenly sized time buckets.</summary>
    /// <param name="query">Filter restricting the aggregated set (paging is ignored).</param>
    /// <param name="bucketCount">Number of buckets to produce.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<AuditLogTimelineBucket>> GetTimelineAsync(
        AuditLogQuery query,
        int bucketCount,
        CancellationToken cancellationToken = default);
}

/// <summary>Marker capability interface: providers that can verify a tamper-evident
/// hash chain over the log. TmAuditLogViewer shows the integrity widget only when its
/// provider implements this interface.</summary>
public interface IAuditLogIntegrityProvider : IAuditLogProvider
{
    /// <summary>Verifies the integrity of the whole log.</summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<AuditLogIntegrityResult> VerifyIntegrityAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tamper-evident hash chain over audit log entries: each entry's hash covers its stable
/// fields plus the previous entry's hash, so any modification breaks every later link.
/// </summary>
public static class AuditLogHashChain
{
    /// <summary>Computes the chain hash of one entry given its predecessor's hash.</summary>
    /// <param name="entry">Entry to hash.</param>
    /// <param name="previousHash">Hash of the previous entry, or null for the first entry.</param>
    public static string ComputeHash(AuditLogEntry entry, string? previousHash)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Cover every stored field — a tamper-evident chain that skips e.g. Severity or
        // Metadata would verify successfully after those fields were modified.
        var builder = new StringBuilder();
        builder.Append(previousHash ?? string.Empty).Append('\n');
        builder.Append(entry.Id).Append('\n');
        builder.Append(entry.Timestamp.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        builder.Append(entry.ActorId).Append('\n');
        builder.Append(entry.ActorName).Append('\n');
        builder.Append(entry.ActorRole).Append('\n');
        builder.Append(entry.Action).Append('\n');
        builder.Append(entry.ActionLabel).Append('\n');
        builder.Append(entry.EntityType).Append('\n');
        builder.Append(entry.EntityId).Append('\n');
        builder.Append(entry.EntityLabel).Append('\n');
        builder.Append(entry.Description).Append('\n');
        builder.Append((int)entry.Severity).Append('\n');
        builder.Append(entry.IpAddress).Append('\n');
        foreach (var change in entry.Changes)
        {
            builder.Append(change.Property).Append('=').Append(change.OldValue).Append('>').Append(change.NewValue).Append('\n');
        }

        foreach (var pair in entry.Metadata.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Fills <see cref="AuditLogEntry.Hash"/> and <see cref="AuditLogEntry.PreviousHash"/>
    /// over the entries in timestamp order.</summary>
    /// <param name="entries">Entries to seal.</param>
    public static void Seal(IEnumerable<AuditLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string? previous = null;
        foreach (var entry in entries.OrderBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            entry.PreviousHash = previous;
            entry.Hash = ComputeHash(entry, previous);
            previous = entry.Hash;
        }
    }

    /// <summary>Verifies a sealed chain and names the first entry that breaks it.</summary>
    /// <param name="entries">Entries to verify.</param>
    public static AuditLogIntegrityResult Verify(IEnumerable<AuditLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = entries.OrderBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        if (ordered.Count == 0 || ordered.Any(e => string.IsNullOrEmpty(e.Hash)))
        {
            return new AuditLogIntegrityResult
            {
                Status = AuditLogIntegrityStatus.Unknown,
                CheckedCount = 0
            };
        }

        string? previous = null;
        foreach (var entry in ordered)
        {
            var expected = ComputeHash(entry, previous);
            if (!string.Equals(entry.PreviousHash, previous, StringComparison.Ordinal)
                || !string.Equals(entry.Hash, expected, StringComparison.Ordinal))
            {
                return new AuditLogIntegrityResult
                {
                    Status = AuditLogIntegrityStatus.Failed,
                    CheckedCount = ordered.Count,
                    FirstInvalidEntryId = entry.Id
                };
            }

            previous = entry.Hash;
        }

        return new AuditLogIntegrityResult
        {
            Status = AuditLogIntegrityStatus.Verified,
            CheckedCount = ordered.Count
        };
    }
}
