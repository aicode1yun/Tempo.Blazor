namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Optional management operations a <see cref="ITempoDocumentLibraryProvider"/> supports.
/// The open dialog reads these to decide which affordances to render — a read-only store
/// advertises <see cref="None"/> and shows only browse/open.
/// </summary>
[Flags]
public enum DocumentLibraryCapabilities
{
    /// <summary>Browse and open only; no management operations.</summary>
    None = 0,

    /// <summary>Supports creating new folders.</summary>
    CreateFolder = 1 << 0,

    /// <summary>Supports renaming documents and folders.</summary>
    Rename = 1 << 1,

    /// <summary>Supports deleting documents and folders.</summary>
    Delete = 1 << 2,

    /// <summary>Supports free-text search across document names.</summary>
    Search = 1 << 3,

    /// <summary>All management operations.</summary>
    All = CreateFolder | Rename | Delete | Search
}
