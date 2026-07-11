using System.Collections.Concurrent;

namespace Tempo.Blazor.Components.DocumentEditor.Performance;

/// <summary>Phase C4 — bounded LRU cache for resolved image URLs. Capped both by entry
/// count (LRU eviction) and per-entry TTL. Thread-safe for concurrent reads/writes.</summary>
internal sealed class ImageResolveCache
{
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<(string DocumentId, string AssetId), Entry> _entries = new();
    private readonly LinkedList<(string DocumentId, string AssetId)> _lru = new();
    private readonly object _lruLock = new();

    public ImageResolveCache(int capacity, TimeSpan ttl)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _ttl = ttl;
    }

    public int Count => _entries.Count;

    /// <summary>Fáze 21 — testovací průhled: interní LRU list musí zůstat 1:1 s <see cref="_entries"/>.</summary>
    internal int LruCountForTests
    {
        get
        {
            lock (_lruLock)
            {
                return _lru.Count;
            }
        }
    }

    public bool TryGet(string documentId, string assetId, out string? url)
    {
        var key = (documentId, assetId);
        if (_entries.TryGetValue(key, out var entry) && (DateTimeOffset.UtcNow - entry.SetAt) < _ttl)
        {
            url = entry.Url;
            MarkRecent(key);
            return true;
        }
        if (_entries.TryRemove(key, out _))
        {
            lock (_lruLock)
            {
                if (entry?.Node is { } node && node.List is not null) _lru.Remove(node);
            }
        }
        url = null;
        return false;
    }

    public void Set(string documentId, string assetId, string? url)
    {
        var key = (documentId, assetId);
        LinkedListNode<(string, string)>? node;
        lock (_lruLock)
        {
            // Fáze 21 (code review): opakovaný Set téhož klíče musí odstranit starý node — jinak
            // LRU list roste bez omezení a evikce (řízená jen _entries.Count) může přes zastaralý
            // duplikát na tailu odstranit ČERSTVÝ záznam, zatímco skutečně nejstarší klíč přežije.
            if (_entries.TryGetValue(key, out var existing) && existing.Node?.List is not null)
            {
                _lru.Remove(existing.Node);
            }

            node = new LinkedListNode<(string, string)>(key);
            _lru.AddFirst(node);
        }
        var entry = new Entry(url, DateTimeOffset.UtcNow, node);
        _entries[key] = entry;
        EvictIfFull();
    }

    /// <summary>Invalidates all entries for the given document. Use when the document's
    /// asset list changes (insert/remove image).</summary>
    public void InvalidateDocument(string documentId)
    {
        var keys = _entries.Keys.Where(k => k.DocumentId == documentId).ToList();
        foreach (var key in keys)
        {
            if (_entries.TryRemove(key, out var removed))
            {
                lock (_lruLock)
                {
                    if (removed.Node?.List is not null) _lru.Remove(removed.Node);
                }
            }
        }
    }

    public void Clear()
    {
        _entries.Clear();
        lock (_lruLock) _lru.Clear();
    }

    private void MarkRecent((string, string) key)
    {
        lock (_lruLock)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.Node?.List is not null)
            {
                _lru.Remove(entry.Node);
                _lru.AddFirst(entry.Node);
            }
        }
    }

    private void EvictIfFull()
    {
        while (_entries.Count > _capacity)
        {
            (string, string) victim;
            lock (_lruLock)
            {
                if (_lru.Last is null) return;
                victim = _lru.Last.Value;
                _lru.RemoveLast();
            }
            _entries.TryRemove(victim, out _);
        }
    }

    private sealed record Entry(string? Url, DateTimeOffset SetAt, LinkedListNode<(string, string)>? Node);
}
