namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Represents a file or folder item in the file manager.
/// </summary>
public sealed class FileManagerItem
{
    /// <summary>Unique identifier of the item (full path or GUID).</summary>
    public required string Id { get; set; }

    /// <summary>Display name of the file or folder.</summary>
    public required string Name { get; set; }

    /// <summary>Full path of the item.</summary>
    public required string Path { get; set; }

    /// <summary>Whether this item is a directory.</summary>
    public bool IsDirectory { get; set; }

    /// <summary>File size in bytes. Null for directories.</summary>
    public long? Size { get; set; }

    /// <summary>Last modification date.</summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>File extension (e.g. ".pdf"). Empty for directories.</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Whether the item is currently selected.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Icon name override. When null, the file manager chooses based on extension.</summary>
    public string? IconName { get; set; }
}
