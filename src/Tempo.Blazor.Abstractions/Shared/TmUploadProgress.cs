namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Progress snapshot emitted while a chunked upload is in flight.</summary>
public sealed class TmUploadProgress
{
    /// <summary>Original or display file name being uploaded.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Bytes transferred so far (sum of completed chunks).</summary>
    public long BytesTransferred { get; init; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Zero-based index of the most recently sent chunk.</summary>
    public int ChunkIndex { get; init; }

    /// <summary>Total number of chunks for the file.</summary>
    public int TotalChunks { get; init; }

    /// <summary>Upload session id assigned by the provider, when one is used.</summary>
    public string? UploadSessionId { get; init; }

    /// <summary>True once the final chunk has been acknowledged.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Completion percentage in the range 0–100.</summary>
    public int Percent => TotalBytes <= 0
        ? (IsComplete ? 100 : 0)
        : (int)Math.Clamp(BytesTransferred * 100 / TotalBytes, 0, 100);
}
