namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// In-memory <see cref="IAuditLogProvider"/> over a fixed entry list. Supports filtered
/// paged queries with count aggregation, filter facets, timeline bucketing, and hash-chain
/// integrity verification. Suitable for demos, tests, and small logs.
/// </summary>
public sealed class InMemoryAuditLogProvider : IAuditLogProvider, IAuditLogIntegrityProvider
{
    private readonly List<AuditLogEntry> _entries;
    private readonly object _cacheGate = new();
    private string? _cachedFilterKey;
    private List<AuditLogEntry>? _cachedOrdered;

    /// <summary>Creates a provider over the given entries.</summary>
    /// <param name="entries">Audit log entries.</param>
    public InMemoryAuditLogProvider(IEnumerable<AuditLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
    }

    /// <inheritdoc />
    public Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = GetOrderedFiltered(query);
        var items = list
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Max(0, query.Take))
            .ToList();

        return Task.FromResult(new AuditLogPage
        {
            Items = items,
            TotalCount = list.Count
        });
    }

    /// <summary>
    /// Virtualized scrolling and CSV export request many pages of the SAME filter in a row;
    /// re-sorting the whole set per page would make each scroll chunk O(n log n). One cached
    /// ordered list per filter signature keeps a page request at O(take).
    /// </summary>
    private List<AuditLogEntry> GetOrderedFiltered(AuditLogQuery query)
    {
        var key = string.Join("\u001f",
            query.ActorId, query.Action, query.EntityType, query.EntityId,
            query.From?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            query.To?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            query.SearchText, query.Descending);

        lock (_cacheGate)
        {
            if (string.Equals(_cachedFilterKey, key, StringComparison.Ordinal) && _cachedOrdered is not null)
            {
                return _cachedOrdered;
            }
        }

        var filtered = Filter(query);
        var ordered = (query.Descending
            ? filtered.OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id, StringComparer.Ordinal)
            : filtered.OrderBy(e => e.Timestamp).ThenBy(e => e.Id, StringComparer.Ordinal))
            .ToList();

        lock (_cacheGate)
        {
            _cachedFilterKey = key;
            _cachedOrdered = ordered;
        }

        return ordered;
    }

    /// <inheritdoc />
    public Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var actors = _entries
            .GroupBy(e => e.ActorId, StringComparer.Ordinal)
            .Select(g => new AuditLogActorOption
            {
                ActorId = g.Key,
                DisplayName = g.Select(e => e.ActorName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? g.Key
            })
            .OrderBy(a => a.ActorId, StringComparer.Ordinal)
            .ToList();

        var actions = _entries
            .Select(e => e.Action)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();

        var entityTypes = _entries
            .Select(e => e.EntityType)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(new AuditLogFilterOptions
        {
            Actors = actors,
            Actions = actions,
            EntityTypes = entityTypes
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditLogTimelineBucket>> GetTimelineAsync(
        AuditLogQuery query,
        int bucketCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = Filter(query).ToList();
        if (filtered.Count == 0 || bucketCount < 1)
        {
            return Task.FromResult<IReadOnlyList<AuditLogTimelineBucket>>([]);
        }

        var from = query.From ?? filtered.Min(e => e.Timestamp);
        var to = query.To ?? filtered.Max(e => e.Timestamp);
        if (to <= from)
        {
            to = from.AddSeconds(1);
        }

        // Buckets use a consistently EXCLUSIVE End (the last bucket ends one tick past the
        // newest event), so [Start, End) tiles the period without boundary overlap.
        var toExclusive = to.AddTicks(1);
        var span = (toExclusive - from).Ticks;
        var bucketTicks = Math.Max(1, span / bucketCount);
        var buckets = new AuditLogTimelineBucket[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            buckets[i] = new AuditLogTimelineBucket
            {
                Start = from.AddTicks(bucketTicks * i),
                End = i == bucketCount - 1 ? toExclusive : from.AddTicks(bucketTicks * (i + 1))
            };
        }

        foreach (var entry in filtered)
        {
            var index = (int)((entry.Timestamp - from).Ticks / bucketTicks);
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= bucketCount)
            {
                index = bucketCount - 1;
            }

            buckets[index].Count++;
        }

        return Task.FromResult<IReadOnlyList<AuditLogTimelineBucket>>(buckets);
    }

    /// <inheritdoc />
    public Task<AuditLogIntegrityResult> VerifyIntegrityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AuditLogHashChain.Verify(_entries));

    private IEnumerable<AuditLogEntry> Filter(AuditLogQuery query)
    {
        IEnumerable<AuditLogEntry> result = _entries;

        if (!string.IsNullOrEmpty(query.ActorId))
        {
            result = result.Where(e => string.Equals(e.ActorId, query.ActorId, StringComparison.Ordinal));
        }

        if (!string.IsNullOrEmpty(query.Action))
        {
            result = result.Where(e => string.Equals(e.Action, query.Action, StringComparison.Ordinal));
        }

        if (!string.IsNullOrEmpty(query.EntityType))
        {
            result = result.Where(e => string.Equals(e.EntityType, query.EntityType, StringComparison.Ordinal));
        }

        if (!string.IsNullOrEmpty(query.EntityId))
        {
            result = result.Where(e => string.Equals(e.EntityId, query.EntityId, StringComparison.Ordinal));
        }

        if (query.From is not null)
        {
            result = result.Where(e => e.Timestamp >= query.From.Value);
        }

        if (query.To is not null)
        {
            result = result.Where(e => e.Timestamp <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var text = query.SearchText.Trim();
            result = result.Where(e =>
                Contains(e.ActorName, text)
                || Contains(e.ActorId, text)
                || Contains(e.Action, text)
                || Contains(e.ActionLabel, text)
                || Contains(e.EntityType, text)
                || Contains(e.EntityId, text)
                || Contains(e.EntityLabel, text)
                || Contains(e.Description, text));
        }

        return result;
    }

    private static bool Contains(string? value, string text)
        => value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
