namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Describes a chunked upload of a single file, including optional resume state.</summary>
public sealed class TmChunkedUploadRequest
{
    /// <summary>Default chunk size in bytes (256 KB).</summary>
    public const int DefaultChunkSizeBytes = 256 * 1024;

    /// <summary>Original or display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>Entity that will own or reference the uploaded asset.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional purpose, such as "document-manager".</summary>
    public string? Purpose { get; set; }

    /// <summary>Arbitrary metadata forwarded to each chunk for provider use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Chunk size in bytes. Defaults to <see cref="DefaultChunkSizeBytes"/>.</summary>
    public int ChunkSizeBytes { get; set; } = DefaultChunkSizeBytes;

    /// <summary>Zero-based index of the first chunk to send. Non-zero resumes an interrupted upload.</summary>
    public int ResumeFromChunkIndex { get; set; }

    /// <summary>Upload session id from a previous attempt, used when resuming.</summary>
    public string? UploadSessionId { get; set; }
}
