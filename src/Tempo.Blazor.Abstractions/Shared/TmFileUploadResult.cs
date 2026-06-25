namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Result returned after a file upload or chunk upload.</summary>
public sealed class TmFileUploadResult
{
    /// <summary>Whether the upload operation succeeded.</summary>
    public bool Success { get; set; } = true;

    /// <summary>True when the asset is complete. Chunk uploads may return false before the final chunk.</summary>
    public bool IsComplete { get; set; } = true;

    /// <summary>Provider-managed asset identifier.</summary>
    public string? AssetId { get; set; }

    /// <summary>Upload session id for chunked uploads, when needed.</summary>
    public string? UploadSessionId { get; set; }

    /// <summary>Download or preview URL, when immediately available.</summary>
    public string? Url { get; set; }

    /// <summary>Original or display file name.</summary>
    public string? FileName { get; set; }

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes, when known.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>URL expiration timestamp, when the provider returns short-lived URLs.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Error message when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
