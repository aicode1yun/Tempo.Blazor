#pragma warning disable MA0048

namespace Tempo.Blazor.Reporting.Models;

/// <summary>Visual mode used by the report explorer.</summary>
public enum ReportExplorerViewMode
{
    /// <summary>Card grid optimized for thumbnails.</summary>
    Grid,

    /// <summary>Dense list optimized for scanning metadata.</summary>
    List,
}

/// <summary>Folder node displayed by <c>TmReportExplorer</c>.</summary>
public sealed class ReportExplorerFolder
{
    /// <summary>Creates a report explorer folder node.</summary>
    public ReportExplorerFolder(string path, string name, IReadOnlyList<ReportExplorerFolder>? children = null)
    {
        Path = path;
        Name = name;
        Children = children ?? [];
    }

    /// <summary>Stable folder path. The root folder uses <c>/</c>.</summary>
    public string Path { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; }

    /// <summary>Child folders.</summary>
    public IReadOnlyList<ReportExplorerFolder> Children { get; init; }

    /// <summary>Number of reports directly in this folder, when known.</summary>
    public int ReportCount { get; init; }
}

/// <summary>Report entry displayed by <c>TmReportExplorer</c>.</summary>
public sealed class ReportExplorerReportItem
{
    /// <summary>Creates a report explorer item.</summary>
    public ReportExplorerReportItem(
        string id,
        string name,
        string path,
        string folderPath,
        string description,
        string ownerName,
        DateTimeOffset modifiedAt,
        int revision,
        string? thumbnailUrl = null,
        IReadOnlyList<string>? tags = null)
    {
        Id = id;
        Name = name;
        Path = path;
        FolderPath = folderPath;
        Description = description;
        OwnerName = ownerName;
        ModifiedAt = modifiedAt;
        Revision = revision;
        ThumbnailUrl = thumbnailUrl;
        Tags = tags ?? [];
    }

    /// <summary>Stable report identifier.</summary>
    public string Id { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; }

    /// <summary>Application route or logical report path.</summary>
    public string Path { get; init; }

    /// <summary>Folder path containing this report.</summary>
    public string FolderPath { get; init; }

    /// <summary>Short report description.</summary>
    public string Description { get; init; }

    /// <summary>Owner or steward display name.</summary>
    public string OwnerName { get; init; }

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset ModifiedAt { get; init; }

    /// <summary>Current revision number.</summary>
    public int Revision { get; init; }

    /// <summary>Optional thumbnail URL or data URL.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Tags used for search and compact metadata display.</summary>
    public IReadOnlyList<string> Tags { get; init; }
}

/// <summary>Request raised when a folder should be created.</summary>
public sealed record ReportExplorerCreateFolderRequest(string ParentPath, string Name);

/// <summary>Request raised when a report should be moved to another folder.</summary>
public sealed record ReportExplorerMoveReportRequest(string ReportId, string TargetFolderPath);

#pragma warning restore MA0048
