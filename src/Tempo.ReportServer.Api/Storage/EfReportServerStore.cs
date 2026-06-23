using Microsoft.EntityFrameworkCore;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>EF Core implementation of the report server catalog store.</summary>
public sealed class EfReportServerStore : IReportServerStore
{
    private readonly ReportServerDbContext _dbContext;

    /// <summary>Creates an EF store.</summary>
    public EfReportServerStore(ReportServerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportFolderDto>> GetFoldersAsync(string tenantId, CancellationToken cancellationToken = default)
        => await _dbContext.Folders
            .Where(folder => folder.TenantId == tenantId)
            .OrderBy(folder => folder.Path)
            .Select(folder => ToDto(folder))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportFolderDto> CreateFolderAsync(CreateReportFolderRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new ReportFolderEntity
        {
            TenantId = request.TenantId,
            FolderId = Id("folder"),
            ParentFolderId = string.IsNullOrWhiteSpace(request.ParentFolderId) ? null : request.ParentFolderId,
            Name = request.Name.Trim(),
        };
        entity.Path = await BuildFolderPathAsync(entity.TenantId, entity.ParentFolderId, entity.Name, cancellationToken).ConfigureAwait(false);
        _dbContext.Folders.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ReportFolderDto?> UpdateFolderAsync(
        string tenantId,
        string folderId,
        UpdateReportFolderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var folder = await _dbContext.Folders.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.FolderId == folderId, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        folder.Name = request.Name.Trim();
        folder.Path = await BuildFolderPathAsync(folder.TenantId, folder.ParentFolderId, folder.Name, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(folder);
    }

    /// <inheritdoc />
    public async Task<ReportFolderDto?> MoveFolderAsync(
        string tenantId,
        string folderId,
        MoveReportFolderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var folder = await _dbContext.Folders.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.FolderId == folderId, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        folder.ParentFolderId = string.IsNullOrWhiteSpace(request.ParentFolderId) ? null : request.ParentFolderId;
        folder.Path = await BuildFolderPathAsync(folder.TenantId, folder.ParentFolderId, folder.Name, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(folder);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFolderAsync(string tenantId, string folderId, CancellationToken cancellationToken = default)
    {
        var folder = await _dbContext.Folders.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.FolderId == folderId, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return false;
        }

        _dbContext.Folders.Remove(folder);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportSummaryDto>> SearchReportsAsync(ReportSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reports = _dbContext.Reports.Where(report => report.TenantId == request.TenantId);
        if (!string.IsNullOrWhiteSpace(request.FolderId))
        {
            reports = reports.Where(report => report.FolderId == request.FolderId);
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var query = request.Query.Trim();
            reports = reports.Where(report => report.Name.Contains(query) || (report.Description != null && report.Description.Contains(query)));
        }

        return await reports
            .OrderBy(report => report.Name)
            .Select(report => ToSummary(report))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ReportDetailDto?> GetReportAsync(string tenantId, string reportId, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.Reports.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ReportId == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        var revision = await LatestRevisionAsync(tenantId, report.ReportId, cancellationToken).ConfigureAwait(false);
        return ToDetail(report, revision);
    }

    /// <inheritdoc />
    public async Task<ReportDetailDto> CreateReportAsync(CreateReportRequestDto request, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var report = new ReportEntity
        {
            TenantId = request.TenantId,
            ReportId = Id("report"),
            FolderId = request.FolderId,
            Name = request.Name.Trim(),
            Description = request.Description,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var revision = CreateRevision(report.TenantId, report.ReportId, 1, request.DefinitionJson, userId, "Initial revision", isPublished: true);
        report.LatestRevisionId = revision.RevisionId;
        _dbContext.Reports.Add(report);
        _dbContext.Revisions.Add(revision);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDetail(report, revision);
    }

    /// <inheritdoc />
    public async Task<ReportDetailDto?> MoveReportAsync(
        string tenantId,
        string reportId,
        MoveReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await _dbContext.Reports.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ReportId == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        report.FolderId = request.FolderId;
        report.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var revision = await LatestRevisionAsync(tenantId, report.ReportId, cancellationToken).ConfigureAwait(false);
        return ToDetail(report, revision);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteReportAsync(string tenantId, string reportId, CancellationToken cancellationToken = default)
    {
        var report = await _dbContext.Reports.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ReportId == reportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
        {
            return false;
        }

        var revisions = await _dbContext.Revisions
            .Where(revision => revision.TenantId == tenantId && revision.ReportId == reportId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        _dbContext.Revisions.RemoveRange(revisions);
        _dbContext.Reports.Remove(report);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<ReportRevisionDto?> AddRevisionAsync(
        UpdateReportDefinitionRequestDto request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await _dbContext.Reports
            .FirstOrDefaultAsync(item => item.TenantId == request.TenantId && item.ReportId == request.ReportId, cancellationToken)
            .ConfigureAwait(false);
        if (report is null || (!string.IsNullOrWhiteSpace(request.ExpectedRevisionId) && report.LatestRevisionId != request.ExpectedRevisionId))
        {
            return null;
        }

        var nextNumber = await _dbContext.Revisions
            .Where(revision => revision.TenantId == request.TenantId && revision.ReportId == request.ReportId)
            .Select(revision => revision.RevisionNumber)
            .DefaultIfEmpty()
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) + 1;
        var revisionEntity = CreateRevision(
            request.TenantId,
            request.ReportId,
            nextNumber,
            request.DefinitionJson,
            userId,
            request.Comment,
            isPublished: false);
        report.LatestRevisionId = revisionEntity.RevisionId;
        report.UpdatedAt = DateTimeOffset.UtcNow;
        _dbContext.Revisions.Add(revisionEntity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(revisionEntity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportRevisionDto>> GetRevisionsAsync(string tenantId, string reportId, CancellationToken cancellationToken = default)
        => await _dbContext.Revisions
            .Where(revision => revision.TenantId == tenantId && revision.ReportId == reportId)
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => ToDto(revision))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportRevisionDto?> PublishRevisionAsync(
        string tenantId,
        string reportId,
        PublishReportRevisionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var revisions = await _dbContext.Revisions
            .Where(revision => revision.TenantId == tenantId && revision.ReportId == reportId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var selected = revisions.FirstOrDefault(revision => revision.RevisionId == request.RevisionId);
        if (selected is null)
        {
            return null;
        }

        foreach (var revision in revisions)
        {
            revision.IsPublished = revision.RevisionId == selected.RevisionId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(selected);
    }

    /// <inheritdoc />
    public async Task<ReportRevisionDto?> RollbackAsync(
        string tenantId,
        string reportId,
        RollbackReportRevisionRequestDto request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var revision = await _dbContext.Revisions
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ReportId == reportId && item.RevisionId == request.RevisionId, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            return null;
        }

        return await AddRevisionAsync(new UpdateReportDefinitionRequestDto
        {
            TenantId = tenantId,
            ReportId = reportId,
            DefinitionJson = revision.DefinitionJson,
            Comment = request.Comment ?? $"Rollback to revision {revision.RevisionNumber}",
        }, userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportDataSourceDto>> GetDataSourcesAsync(string tenantId, CancellationToken cancellationToken = default)
        => await _dbContext.DataSources
            .Where(source => source.TenantId == tenantId)
            .OrderBy(source => source.Name)
            .Select(source => ToDto(source))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ReportDataSourceDto> UpsertDataSourceAsync(
        UpsertReportDataSourceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await _dbContext.DataSources
            .FirstOrDefaultAsync(item => item.TenantId == request.TenantId && item.Name == request.Name, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            source = new ReportDataSourceEntity
            {
                TenantId = request.TenantId,
                DataSourceId = Id("ds"),
                Name = request.Name.Trim(),
            };
            _dbContext.DataSources.Add(source);
        }

        source.Kind = request.Kind.Trim();
        source.Connection = request.Connection;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(source);
    }

    /// <inheritdoc />
    public async Task<ReportDataSourceDto?> GetDataSourceAsync(string tenantId, string dataSourceId, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.DataSources
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DataSourceId == dataSourceId, cancellationToken)
            .ConfigureAwait(false);
        return source is null ? null : ToDto(source);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDataSourceAsync(string tenantId, string dataSourceId, CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.DataSources
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DataSourceId == dataSourceId, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            return false;
        }

        _dbContext.DataSources.Remove(source);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<string> BuildFolderPathAsync(
        string tenantId,
        string? parentFolderId,
        string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentFolderId))
        {
            return "/" + name.Trim('/');
        }

        var parent = await _dbContext.Folders.FirstOrDefaultAsync(
            folder => folder.TenantId == tenantId && folder.FolderId == parentFolderId,
            cancellationToken).ConfigureAwait(false);
        return parent is null ? "/" + name.Trim('/') : parent.Path.TrimEnd('/') + "/" + name.Trim('/');
    }

    private async Task<ReportRevisionEntity?> LatestRevisionAsync(string tenantId, string reportId, CancellationToken cancellationToken)
        => await _dbContext.Revisions
            .Where(revision => revision.TenantId == tenantId && revision.ReportId == reportId)
            .OrderByDescending(revision => revision.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private static ReportRevisionEntity CreateRevision(
        string tenantId,
        string reportId,
        int revisionNumber,
        string definitionJson,
        string userId,
        string? comment,
        bool isPublished)
        => new()
        {
            TenantId = tenantId,
            ReportId = reportId,
            RevisionId = Id("rev"),
            RevisionNumber = revisionNumber,
            DefinitionJson = definitionJson,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            Comment = comment,
            IsPublished = isPublished,
        };

    private static string Id(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}";

    private static ReportFolderDto ToDto(ReportFolderEntity folder)
        => new()
        {
            TenantId = folder.TenantId,
            FolderId = folder.FolderId,
            ParentFolderId = folder.ParentFolderId,
            Name = folder.Name,
            Path = folder.Path,
        };

    private static ReportSummaryDto ToSummary(ReportEntity report)
        => new()
        {
            TenantId = report.TenantId,
            ReportId = report.ReportId,
            FolderId = report.FolderId,
            Name = report.Name,
            Description = report.Description,
            LatestRevisionId = report.LatestRevisionId,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt,
        };

    private static ReportDetailDto ToDetail(ReportEntity report, ReportRevisionEntity? revision)
        => new()
        {
            TenantId = report.TenantId,
            ReportId = report.ReportId,
            FolderId = report.FolderId,
            Name = report.Name,
            Description = report.Description,
            LatestRevisionId = report.LatestRevisionId,
            DefinitionJson = revision?.DefinitionJson ?? string.Empty,
        };

    private static ReportRevisionDto ToDto(ReportRevisionEntity revision)
        => new()
        {
            TenantId = revision.TenantId,
            RevisionId = revision.RevisionId,
            ReportId = revision.ReportId,
            RevisionNumber = revision.RevisionNumber,
            DefinitionJson = revision.DefinitionJson,
            CreatedByUserId = revision.CreatedByUserId,
            CreatedAt = revision.CreatedAt,
            Comment = revision.Comment,
            IsPublished = revision.IsPublished,
        };

    private static ReportDataSourceDto ToDto(ReportDataSourceEntity source)
        => new()
        {
            TenantId = source.TenantId,
            DataSourceId = source.DataSourceId,
            Name = source.Name,
            Kind = source.Kind,
            Connection = source.Connection,
        };
}
