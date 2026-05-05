namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Represents a physical file attachment linked to a <see cref="DocumentManagerItem{TMetadata}"/>.
/// </summary>
public class FileAttachment
{
    /// <summary>Stable unique identifier of the attachment.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name (file name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>MIME content type (e.g. "application/pdf").</summary>
    public string? ContentType { get; set; }

    /// <summary>Date when the attachment was created/uploaded.</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>Optional version identifier (e.g. "1.0").</summary>
    public string? Version { get; set; }
}
