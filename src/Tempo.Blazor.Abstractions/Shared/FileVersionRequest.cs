namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Describes a new version to record via <see cref="Interfaces.IFileVersioningHook.CreateVersionAsync"/>.</summary>
public sealed class FileVersionRequest
{
    /// <summary>Identifier of the logical item receiving a new version.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>File name for the new version.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type, when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Provider-managed asset id for the new version's content, when available.</summary>
    public string? AssetId { get; set; }

    /// <summary>User creating the version, when known.</summary>
    public TmUserRef? CreatedBy { get; set; }

    /// <summary>Optional change comment.</summary>
    public string? Comment { get; set; }
}
