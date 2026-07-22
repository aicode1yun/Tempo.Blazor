#pragma warning disable MA0048

using Tempo.Blazor.Reporting.Models;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Web.Services;

/// <summary>
/// Maps Report Server API DTOs (<see cref="ITempoReportServerClient"/>) to the UI models consumed by
/// the catalog pages (<c>TmReportExplorer</c>, the revision browser and the data-source table).
/// The API returns flat folder rows; this mapper reshapes them into the explorer's folder tree and
/// resolves report/revision/data-source rows into the display models.
/// </summary>
internal static class ReportServerCatalogMapper
{
    /// <summary>Synthetic root folder path used by the explorer when the API has no explicit root.</summary>
    public const string RootFolderPath = "/";

    /// <summary>Builds the explorer folder tree (with a synthetic root) from the flat folder DTO list.</summary>
    public static ReportExplorerFolder BuildFolderTree(
        IReadOnlyList<ReportFolderDto> folders,
        IReadOnlyDictionary<string, int>? reportCountByFolderId = null)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var childrenByParent = folders
            .GroupBy(folder => folder.ParentFolderId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ReportFolderDto>)[.. group], StringComparer.Ordinal);

        ReportExplorerFolder Build(ReportFolderDto folder)
            => new(folder.Path, folder.Name, ChildrenOf(folder.FolderId))
            {
                ReportCount = reportCountByFolderId is not null && reportCountByFolderId.TryGetValue(folder.FolderId, out var count)
                    ? count
                    : 0,
            };

        IReadOnlyList<ReportExplorerFolder> ChildrenOf(string parentFolderId)
            => childrenByParent.TryGetValue(parentFolderId, out var children)
                ? [.. children.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase).Select(Build)]
                : [];

        var roots = childrenByParent.TryGetValue(string.Empty, out var topLevel)
            ? topLevel.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase).Select(Build).ToArray()
            : [];

        return new ReportExplorerFolder(RootFolderPath, "Reports", roots);
    }

    /// <summary>Builds a folder-id to canonical-path lookup used to place reports and resolve move targets.</summary>
    public static IReadOnlyDictionary<string, string> FolderPathsById(IReadOnlyList<ReportFolderDto> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return folders.ToDictionary(folder => folder.FolderId, folder => folder.Path, StringComparer.Ordinal);
    }

    /// <summary>Builds a canonical-path to folder-id lookup so path-based UI actions can call the id-based API.</summary>
    public static IReadOnlyDictionary<string, string> FolderIdsByPath(IReadOnlyList<ReportFolderDto> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            map[folder.Path] = folder.FolderId;
        }

        return map;
    }

    /// <summary>Maps a report summary DTO to the explorer report item, placing it under its folder path.</summary>
    public static ReportExplorerReportItem ToReportItem(
        ReportSummaryDto report,
        IReadOnlyDictionary<string, string> folderPathByFolderId)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(folderPathByFolderId);

        var folderPath = folderPathByFolderId.TryGetValue(report.FolderId, out var path) ? path : RootFolderPath;
        return new ReportExplorerReportItem(
            report.ReportId,
            report.Name,
            BuildDeepLink(folderPath, report.ReportId),
            folderPath,
            report.Description ?? string.Empty,
            ownerName: string.Empty,
            report.UpdatedAt,
            revision: 0,
            thumbnailUrl: null,
            tags: []);
    }

    /// <summary>Maps a revision DTO to the revision browser row. The report's latest revision is the current one.</summary>
    public static ReportServerRevision ToRevision(ReportRevisionDto revision, string? latestRevisionId)
    {
        ArgumentNullException.ThrowIfNull(revision);
        return new ReportServerRevision
        {
            Id = revision.RevisionId,
            ReportId = revision.ReportId,
            Version = revision.RevisionNumber,
            Author = revision.CreatedByUserId,
            CreatedAt = revision.CreatedAt,
            MetadataDiff = revision.Comment ?? string.Empty,
            IsCurrent = string.Equals(revision.RevisionId, latestRevisionId, StringComparison.Ordinal),
        };
    }

    /// <summary>Maps a data-source DTO to the management table row.</summary>
    public static ReportServerDataSource ToDataSource(ReportDataSourceDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReportServerDataSource
        {
            Id = source.DataSourceId,
            Name = source.Name,
            Provider = source.Kind,
            Endpoint = source.Connection,
        };
    }

    private static string BuildDeepLink(string folderPath, string reportId)
    {
        var segment = folderPath.Trim('/');
        return string.IsNullOrEmpty(segment)
            ? $"/reports/{reportId}"
            : $"/reports/{segment}/{reportId}";
    }
}

#pragma warning restore MA0048
