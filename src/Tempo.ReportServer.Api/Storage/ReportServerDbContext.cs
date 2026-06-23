using Microsoft.EntityFrameworkCore;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>EF Core context for the report server catalog store.</summary>
public sealed class ReportServerDbContext : DbContext
{
    private readonly ReportServerRequestContext _requestContext;

    /// <summary>Creates a report server EF context.</summary>
    public ReportServerDbContext(DbContextOptions<ReportServerDbContext> options, ReportServerRequestContext requestContext)
        : base(options)
    {
        _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
    }

    /// <summary>Folder table.</summary>
    public DbSet<ReportFolderEntity> Folders => Set<ReportFolderEntity>();

    /// <summary>Report table.</summary>
    public DbSet<ReportEntity> Reports => Set<ReportEntity>();

    /// <summary>Revision table.</summary>
    public DbSet<ReportRevisionEntity> Revisions => Set<ReportRevisionEntity>();

    /// <summary>Data source table.</summary>
    public DbSet<ReportDataSourceEntity> DataSources => Set<ReportDataSourceEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ReportFolderEntity>(entity =>
        {
            entity.HasKey(folder => folder.FolderId);
            entity.HasIndex(folder => new { folder.TenantId, folder.Path }).IsUnique();
            entity.Property(folder => folder.Name).HasMaxLength(200);
            entity.HasQueryFilter(folder => folder.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        modelBuilder.Entity<ReportEntity>(entity =>
        {
            entity.HasKey(report => report.ReportId);
            entity.HasIndex(report => new { report.TenantId, report.FolderId, report.Name });
            entity.Property(report => report.Name).HasMaxLength(200);
            entity.HasQueryFilter(report => report.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        modelBuilder.Entity<ReportRevisionEntity>(entity =>
        {
            entity.HasKey(revision => revision.RevisionId);
            entity.HasIndex(revision => new { revision.TenantId, revision.ReportId, revision.RevisionNumber }).IsUnique();
            entity.HasQueryFilter(revision => revision.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        modelBuilder.Entity<ReportDataSourceEntity>(entity =>
        {
            entity.HasKey(dataSource => dataSource.DataSourceId);
            entity.HasIndex(dataSource => new { dataSource.TenantId, dataSource.Name }).IsUnique();
            entity.Property(dataSource => dataSource.Name).HasMaxLength(200);
            entity.Property(dataSource => dataSource.Kind).HasMaxLength(32);
            entity.HasQueryFilter(dataSource => dataSource.TenantId == _requestContext.ExecutionContext.TenantId);
        });
    }
}

/// <summary>EF entity for report folders.</summary>
public sealed class ReportFolderEntity
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Folder identifier.</summary>
    public string FolderId { get; set; } = string.Empty;

    /// <summary>Parent folder identifier.</summary>
    public string? ParentFolderId { get; set; }

    /// <summary>Folder name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Canonical path.</summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>EF entity for report catalog entries.</summary>
public sealed class ReportEntity
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Folder identifier.</summary>
    public string FolderId { get; set; } = string.Empty;

    /// <summary>Report name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Report description.</summary>
    public string? Description { get; set; }

    /// <summary>Latest revision identifier.</summary>
    public string? LatestRevisionId { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>EF entity for immutable report revisions.</summary>
public sealed class ReportRevisionEntity
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Revision identifier.</summary>
    public string RevisionId { get; set; } = string.Empty;

    /// <summary>Report identifier.</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>Monotonic revision number.</summary>
    public int RevisionNumber { get; set; }

    /// <summary>Canonical definition JSON.</summary>
    public string DefinitionJson { get; set; } = string.Empty;

    /// <summary>Author user identifier.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Revision note.</summary>
    public string? Comment { get; set; }

    /// <summary>Whether the revision is published.</summary>
    public bool IsPublished { get; set; }
}

/// <summary>EF entity for named data sources.</summary>
public sealed class ReportDataSourceEntity
{
    /// <summary>Tenant identifier.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Data source identifier.</summary>
    public string DataSourceId { get; set; } = string.Empty;

    /// <summary>Data source name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Data source kind.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Connection string or URL.</summary>
    public string Connection { get; set; } = string.Empty;
}
