using System.Buffers;
using System.IO;
using System.Text.Json;

namespace Tempo.Blazor.Components.DocumentEditor.Performance;

/// <summary>Phase C2 — pooled UTF-8 writer for WYSIWYG document snapshots. Reuses a
/// <see cref="PooledByteBufferWriter"/> backed by <see cref="ArrayPool{T}.Shared"/> so
/// large snapshots don't allocate fresh megabyte-sized strings on every send.</summary>
internal static class PooledSnapshotSerializer
{
    /// <summary>Serializes <paramref name="value"/> into UTF-8 bytes from a pooled buffer.
    /// The returned <see cref="PooledByteBuffer"/> owns the rent — dispose it to return memory.</summary>
    public static PooledByteBuffer SerializeUtf8<T>(T value, JsonSerializerOptions options)
    {
        var writer = new PooledByteBufferWriter(initialCapacity: 4096);
        try
        {
            using (var jsonWriter = new Utf8JsonWriter(writer))
            {
                JsonSerializer.Serialize(jsonWriter, value, options);
            }
            return new PooledByteBuffer(writer);
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }
}

/// <summary>Wraps the rented buffer in a disposable handle. Callers take a span/array
/// (<see cref="WrittenSpan"/> / <see cref="WrittenMemory"/>) before disposing.</summary>
internal readonly struct PooledByteBuffer : IDisposable
{
    private readonly PooledByteBufferWriter _writer;
    internal PooledByteBuffer(PooledByteBufferWriter writer) => _writer = writer;
    public ReadOnlySpan<byte> WrittenSpan => _writer.WrittenSpan;
    public ReadOnlyMemory<byte> WrittenMemory => _writer.WrittenMemory;
    public int Length => _writer.WrittenCount;
    public void Dispose() => _writer.Dispose();
}

/// <summary>Minimal pooled <see cref="IBufferWriter{T}"/> over <see cref="ArrayPool{T}.Shared"/>.
/// Trimmed-down implementation of the same pattern used by <see cref="MemoryStream"/>-pooled
/// approaches; standalone so we don't depend on Microsoft.IO.RecyclableMemoryStream.</summary>
internal sealed class PooledByteBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[] _rented;
    private int _written;
    private bool _disposed;

    public PooledByteBufferWriter(int initialCapacity)
    {
        if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        _rented = ArrayPool<byte>.Shared.Rent(initialCapacity);
    }

    public int WrittenCount => _written;
    public ReadOnlySpan<byte> WrittenSpan => _rented.AsSpan(0, _written);
    public ReadOnlyMemory<byte> WrittenMemory => _rented.AsMemory(0, _written);

    public void Advance(int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PooledByteBufferWriter));
        if (count < 0 || _written + count > _rented.Length) throw new ArgumentOutOfRangeException(nameof(count));
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _rented.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _rented.AsSpan(_written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PooledByteBufferWriter));
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        var hint = sizeHint == 0 ? 1 : sizeHint;
        var available = _rented.Length - _written;
        if (available >= hint) return;
        var newSize = Math.Max(_rented.Length * 2, _written + hint);
        var next = ArrayPool<byte>.Shared.Rent(newSize);
        Buffer.BlockCopy(_rented, 0, next, 0, _written);
        ArrayPool<byte>.Shared.Return(_rented);
        _rented = next;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<byte>.Shared.Return(_rented);
        _rented = Array.Empty<byte>();
        _written = 0;
    }
}
