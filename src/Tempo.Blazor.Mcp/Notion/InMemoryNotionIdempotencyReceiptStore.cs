using System.Collections.Concurrent;

namespace Tempo.Blazor.Mcp.Notion;

internal enum NotionReceiptAcquireStatus
{
    Acquired,
    Replay,
    Collision
}

internal sealed record NotionReceiptLease(
    string Key,
    string RequestHash,
    Guid LeaseId);

internal sealed record NotionReceiptAcquireResult(
    NotionReceiptAcquireStatus Status,
    NotionReceiptLease? Lease = null,
    NotionAtomicAuthoringResult? Result = null);

internal sealed class InMemoryNotionIdempotencyReceiptStore(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _acquisitions;

    public async ValueTask<NotionReceiptAcquireResult> AcquireAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if ((Interlocked.Increment(ref _acquisitions) & 63) == 0)
        {
            PruneExpired();
        }
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.IsExpired(_timeProvider.GetUtcNow()) &&
                    TryRemove(key, existing))
                {
                    existing.Abandon();
                    continue;
                }

                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return new NotionReceiptAcquireResult(NotionReceiptAcquireStatus.Collision);
                }

                var completed = await existing.Completion.Task.WaitAsync(cancellationToken);
                if (completed is null)
                {
                    continue;
                }

                return new NotionReceiptAcquireResult(
                    NotionReceiptAcquireStatus.Replay,
                    Result: completed with { Replayed = true });
            }

            var created = new Entry(requestHash);
            if (_entries.TryAdd(key, created))
            {
                return new NotionReceiptAcquireResult(
                    NotionReceiptAcquireStatus.Acquired,
                    new NotionReceiptLease(key, requestHash, created.LeaseId));
            }
        }
    }

    public ValueTask CompleteAsync(
        NotionReceiptLease lease,
        NotionAtomicAuthoringResult result,
        TimeSpan retention)
    {
        if (_entries.TryGetValue(lease.Key, out var entry) &&
            entry.LeaseId == lease.LeaseId &&
            string.Equals(entry.RequestHash, lease.RequestHash, StringComparison.Ordinal))
        {
            entry.Complete(result, _timeProvider.GetUtcNow().Add(retention));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask AbandonAsync(NotionReceiptLease lease)
    {
        if (_entries.TryGetValue(lease.Key, out var entry) &&
            entry.LeaseId == lease.LeaseId &&
            TryRemove(lease.Key, entry))
        {
            entry.Abandon();
        }

        return ValueTask.CompletedTask;
    }

    private bool TryRemove(string key, Entry expected)
        => ((ICollection<KeyValuePair<string, Entry>>)_entries)
            .Remove(new KeyValuePair<string, Entry>(key, expected));

    private void PruneExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _entries)
        {
            if (pair.Value.IsExpired(now) && TryRemove(pair.Key, pair.Value))
            {
                pair.Value.Abandon();
            }
        }
    }

    private sealed class Entry(string requestHash)
    {
        private readonly object _gate = new();
        private DateTimeOffset _expiresAt = DateTimeOffset.MaxValue;

        public string RequestHash { get; } = requestHash;
        public Guid LeaseId { get; } = Guid.NewGuid();
        public TaskCompletionSource<NotionAtomicAuthoringResult?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsExpired(DateTimeOffset now)
        {
            lock (_gate)
            {
                return Completion.Task.IsCompleted && now >= _expiresAt;
            }
        }

        public void Complete(NotionAtomicAuthoringResult result, DateTimeOffset expiresAt)
        {
            lock (_gate)
            {
                _expiresAt = expiresAt;
                Completion.TrySetResult(result);
            }
        }

        public void Abandon()
            => Completion.TrySetResult(null);
    }
}
