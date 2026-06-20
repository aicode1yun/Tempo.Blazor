namespace Tempo.Blazor.DocumentLibrary;

/// <summary>Field a <see cref="DocumentLibraryQuery"/> orders its results by.</summary>
public enum DocumentLibrarySortField
{
    /// <summary>Order by document name.</summary>
    Name,

    /// <summary>Order by last-modified timestamp.</summary>
    Modified,

    /// <summary>Order by creation timestamp.</summary>
    Created
}
