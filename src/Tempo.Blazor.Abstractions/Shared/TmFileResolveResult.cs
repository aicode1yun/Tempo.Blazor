namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Result returned after resolving a provider-managed asset.</summary>
public sealed class TmFileResolveResult
{
    /// <summary>Whether the asset was resolved successfully.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Provider-managed asset identifier.</summary>
    public string? AssetId { get; set; }

    /// <summary>Resolved download or preview URL.</summary>
    public string? Url { get; set; }

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes, when known.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Original or display file name, when known.</summary>
    public string? FileName { get; set; }

    /// <summary>URL expiration timestamp, when the provider returns short-lived URLs.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Error message when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
