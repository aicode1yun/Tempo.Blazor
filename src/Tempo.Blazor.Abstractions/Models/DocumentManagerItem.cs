namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Represents a file or folder in <see cref="Components.Files.TmDocumentManager{TMetadata}"/>.
/// </summary>
/// <typeparam name="TMetadata">Custom metadata type attached to the item.</typeparam>
public class DocumentManagerItem<TMetadata> where TMetadata : class
{
    /// <summary>Stable unique identifier. Used as the primary key for all operations.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name (file name or folder name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full path for display and breadcrumb purposes only. Not used as an identifier.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Whether this item is a folder.</summary>
    public bool IsDirectory { get; set; }

    /// <summary>File size in bytes. Null for folders.</summary>
    public long? Size { get; set; }

    /// <summary>File extension including the dot (e.g. ".pdf"). Empty for folders.</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Last modification date.</summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>Creation date.</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>User who created the item.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>User who last modified the item.</summary>
    public string? ModifiedBy { get; set; }

    /// <summary>Optional version identifier (e.g. "1.2").</summary>
    public string? Version { get; set; }

    /// <summary>Custom metadata payload.</summary>
    public TMetadata? Metadata { get; set; }

    /// <summary>Access permissions for the current user.</summary>
    public DocumentManagerPermission? Permissions { get; set; }

    /// <summary>Optional icon override (name from the icon registry).</summary>
    public string? IconName { get; set; }

    /// <summary>Optional tags or labels.</summary>
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>Physical file attachments linked to this item.</summary>
    public IReadOnlyList<FileAttachment> Attachments { get; set; } = [];
}
