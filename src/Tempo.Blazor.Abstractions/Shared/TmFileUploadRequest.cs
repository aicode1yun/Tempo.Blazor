namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Request for uploading a file stream to a provider-managed asset store.</summary>
public sealed class TmFileUploadRequest
{
    /// <summary>Original or display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes, when known.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>Entity that will own or reference this asset, when known at upload time.</summary>
    public TmEntityRef? EntityRef { get; set; }

    /// <summary>Optional purpose, such as "document-image" or "notion-media".</summary>
    public string? Purpose { get; set; }

    /// <summary>Optional caller-selected asset id for draft workflows.</summary>
    public string? LocalAssetId { get; set; }

    /// <summary>True when the uploaded asset is a draft until committed.</summary>
    public bool IsDraft { get; set; }

    /// <summary>Arbitrary metadata for provider use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
