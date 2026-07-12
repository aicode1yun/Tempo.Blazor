namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>A single historical version of a file managed by an <see cref="Interfaces.IFileVersioningHook"/>.</summary>
public sealed class TmFileVersion
{
    /// <summary>Stable version identifier.</summary>
    public string VersionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Identifier of the logical item this version belongs to.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Monotonic version number (1-based); higher is newer.</summary>
    public int VersionNumber { get; set; }

    /// <summary>File name captured for this version.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes for this version.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Provider-managed asset id for this version's content, when available.</summary>
    public string? AssetId { get; set; }

    /// <summary>Timestamp when this version was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>User who created this version, when known.</summary>
    public TmUserRef? CreatedBy { get; set; }

    /// <summary>Optional change comment.</summary>
    public string? Comment { get; set; }

    /// <summary>True when this is the current (latest) version of the item.</summary>
    public bool IsCurrent { get; set; }
}
