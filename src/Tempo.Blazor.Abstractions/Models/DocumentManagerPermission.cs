namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Defines access permissions for a document or folder in <see cref="Components.Files.TmDocumentManager{TMetadata}"/>.
/// </summary>
public class DocumentManagerPermission
{
    /// <summary>Whether the item can be viewed.</summary>
    public bool CanRead { get; set; } = true;

    /// <summary>Whether the item can be modified (renamed, metadata edited).</summary>
    public bool CanWrite { get; set; } = true;

    /// <summary>Whether the item can be deleted.</summary>
    public bool CanDelete { get; set; } = true;

    /// <summary>Whether the item can be moved to another folder.</summary>
    public bool CanMove { get; set; } = true;

    /// <summary>Whether the item can be copied.</summary>
    public bool CanCopy { get; set; } = true;

    /// <summary>Whether the item can be shared with others.</summary>
    public bool CanShare { get; set; } = false;

    /// <summary>Whether the user can upload new files into this folder.</summary>
    public bool CanUpload { get; set; } = true;

    /// <summary>Whether the user can create sub-folders inside this folder.</summary>
    public bool CanCreateFolder { get; set; } = true;

    /// <summary>Whether the item can be renamed.</summary>
    public bool CanRename { get; set; } = true;

    /// <summary>Whether the item can be downloaded.</summary>
    public bool CanDownload { get; set; } = true;
}
