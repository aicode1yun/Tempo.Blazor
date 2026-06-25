namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Single chunk in a chunked upload session.</summary>
public sealed class TmFileChunk
{
    /// <summary>Original or display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>Zero-based chunk index.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Total number of chunks.</summary>
    public int TotalChunks { get; set; }

    /// <summary>Bytes carried by this chunk.</summary>
    public byte[] Data { get; set; } = [];

    /// <summary>Upload session id from a previous chunk, when the provider requires one.</summary>
    public string? UploadSessionId { get; set; }

    /// <summary>Entity that will own or reference the uploaded asset.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional purpose, such as "activity-attachment".</summary>
    public string? Purpose { get; set; }

    /// <summary>Arbitrary metadata for provider use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>True when this is the final chunk.</summary>
    public bool IsLast => TotalChunks > 0 && ChunkIndex == TotalChunks - 1;
}
