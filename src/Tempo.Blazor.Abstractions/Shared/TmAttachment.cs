namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Shared file attachment linked to a Tempo entity.</summary>
public sealed class TmAttachment
{
    /// <summary>Stable attachment identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Entity this attachment belongs to.</summary>
    public TmEntityRef EntityRef { get; set; } = new();

    /// <summary>Original or display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Download or preview URL, when the provider exposes one directly.</summary>
    public string? Url { get; set; }

    /// <summary>Provider-managed blob or asset identifier.</summary>
    public string? AssetId { get; set; }

    /// <summary>User that uploaded this attachment, when known.</summary>
    public TmUserRef? UploadedBy { get; set; }

    /// <summary>Timestamp when the attachment was uploaded or linked.</summary>
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional purpose, such as "work-item", "document-manager", "signature", or "notion-media".</summary>
    public string? Purpose { get; set; }

    /// <summary>Whether the current viewer may download this attachment.</summary>
    public bool CanDownload { get; set; } = true;

    /// <summary>Whether the current viewer may remove this attachment link.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Anti-virus / content scan state. When <see cref="FileScanStatus.Blocked"/>
    /// or <see cref="FileScanStatus.Pending"/>, the file should be treated as unavailable.</summary>
    public FileScanStatus ScanStatus { get; set; } = FileScanStatus.NotScanned;

    /// <summary>Optional human-readable detail about the scan outcome.</summary>
    public string? ScanMessage { get; set; }

    /// <summary>Arbitrary metadata for consumer use.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>True when the scan state permits the viewer to access the file content.</summary>
    public bool IsScanAvailable
        => ScanStatus is FileScanStatus.NotScanned or FileScanStatus.Clean;

    /// <summary>Returns true when the content type describes an image.</summary>
    public bool IsImage
        => ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Returns true when required identity fields are populated.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(Id)
        && EntityRef.IsValid
        && !string.IsNullOrWhiteSpace(FileName);
}
