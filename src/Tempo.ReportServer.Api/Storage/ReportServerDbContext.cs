using Microsoft.EntityFrameworkCore;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>EF Core context for the report server catalog store.</summary>
public sealed class ReportServerDbContext : DbContext
{
    private const int TenantIdMaxLength = 128;
    private const int IdMaxLength = 128;
    private const int NameMaxLength = 200;
    private const int PathMaxLength = 400;
    private const int ApplicationIdMaxLength = 256;
    private const int ActorIdMaxLength = 256;
    private const int HashMaxLength = 88;

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

    /// <summary>Embedding API key table.</summary>
    public DbSet<ReportApiKeyEntity> ApiKeys => Set<ReportApiKeyEntity>();

    /// <summary>Audit event table.</summary>
    public DbSet<ReportAuditEventEntity> AuditEvents => Set<ReportAuditEventEntity>();

    /// <summary>JIT-provisioned user table.</summary>
    public DbSet<ReportServerUserEntity> Users => Set<ReportServerUserEntity>();

    /// <summary>Per-folder permission grant table.</summary>
    public DbSet<ReportFolderPermissionEntity> FolderPermissions => Set<ReportFolderPermissionEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // String columns that participate in indexes MUST have an explicit maximum length:
        // on SQL Server an unbounded nvarchar(max) column cannot be used as an index key.
        // The composite index key sizes below stay within the SQL Server 1700-byte limit
        // (Path 400 + TenantId 128 => ~1056 bytes for the folder unique index).
        modelBuilder.Entity<ReportFolderEntity>(entity =>
        {
            entity.HasKey(folder => folder.FolderId);
            entity.HasIndex(folder => new { folder.TenantId, folder.Path }).IsUnique();
            entity.Property(folder => folder.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(folder => folder.FolderId).HasMaxLength(IdMaxLength);
            entity.Property(folder => folder.ParentFolderId).HasMaxLength(IdMaxLength);
            entity.Property(folder => folder.Name).HasMaxLength(NameMaxLength);
            entity.Property(folder => folder.Path).HasMaxLength(PathMaxLength);
            entity.HasQueryFilter(folder => folder.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        modelBuilder.Entity<ReportEntity>(entity =>
        {
            entity.HasKey(report => report.ReportId);
            entity.HasIndex(report => new { report.TenantId, report.FolderId, report.Name });
            entity.Property(report => report.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(report => report.ReportId).HasMaxLength(IdMaxLength);
            entity.Property(report => report.FolderId).HasMaxLength(IdMaxLength);
            entity.Property(report => report.LatestRevisionId).HasMaxLength(IdMaxLength);
            entity.Property(report => report.Name).HasMaxLength(NameMaxLength);
            entity.HasQueryFilter(report => report.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        modelBuilder.Entity<ReportRevisionEntity>(entity =>
        {
            entity.HasKey(revision => revision.RevisionId);
            entity.HasIndex(revision => new { revision.TenantId, revision.ReportId, revision.RevisionNumber }).IsUnique();
            entity.Property(revision => revision.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(revision => revision.RevisionId).HasMaxLength(IdMaxLength);
            entity.Property(revision => revision.ReportId).HasMaxLength(IdMaxLength);
            entity.Property(revision => revision.CreatedByUserId).HasMaxLength(IdMaxLength);
            entity.HasQueryFilter(revision => revision.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        modelBuilder.Entity<ReportDataSourceEntity>(entity =>
        {
            entity.HasKey(dataSource => dataSource.DataSourceId);
            entity.HasIndex(dataSource => new { dataSource.TenantId, dataSource.Name }).IsUnique();
            entity.Property(dataSource => dataSource.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(dataSource => dataSource.DataSourceId).HasMaxLength(IdMaxLength);
            entity.Property(dataSource => dataSource.Name).HasMaxLength(NameMaxLength);
            entity.Property(dataSource => dataSource.Kind).HasMaxLength(32);
            entity.HasQueryFilter(dataSource => dataSource.TenantId == _requestContext.ExecutionContext.TenantId);
        });

        // API keys and audit events are deliberately NOT tenant-query-filtered: key validation
        // resolves a key by its hash before the tenant is known, and audit queries pass the tenant
        // explicitly. The stores always constrain by tenant where a tenant scope applies.
        modelBuilder.Entity<ReportApiKeyEntity>(entity =>
        {
            entity.HasKey(key => key.KeyId);
            entity.Property(key => key.KeyId).HasMaxLength(IdMaxLength);
            entity.Property(key => key.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(key => key.ApplicationId).HasMaxLength(ApplicationIdMaxLength);
            entity.Property(key => key.KeyHash).HasMaxLength(HashMaxLength);
            entity.Property(key => key.RevokedByUserId).HasMaxLength(ActorIdMaxLength);
            entity.HasIndex(key => key.KeyHash).IsUnique();
            entity.HasIndex(key => new { key.TenantId, key.ApplicationId });
        });

        modelBuilder.Entity<ReportAuditEventEntity>(entity =>
        {
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.Id).ValueGeneratedOnAdd();
            entity.Property(auditEvent => auditEvent.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(auditEvent => auditEvent.ActorId).HasMaxLength(ActorIdMaxLength);
            entity.Property(auditEvent => auditEvent.ResourceId).HasMaxLength(NameMaxLength);
            entity.HasIndex(auditEvent => new { auditEvent.TenantId, auditEvent.Timestamp });
        });

        // JIT users and folder permissions are not tenant-query-filtered: provisioning resolves a
        // user by subject before the ambient tenant is established, and the stores constrain by
        // tenant explicitly where a tenant scope applies.
        modelBuilder.Entity<ReportServerUserEntity>(entity =>
        {
            entity.HasKey(user => user.Subject);
            entity.Property(user => user.Subject).HasMaxLength(ActorIdMaxLength);
            entity.Property(user => user.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(user => user.Email).HasMaxLength(NameMaxLength);
            entity.Property(user => user.DisplayName).HasMaxLength(NameMaxLength);
            entity.HasIndex(user => user.TenantId);
        });

        modelBuilder.Entity<ReportFolderPermissionEntity>(entity =>
        {
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Id).ValueGeneratedOnAdd();
            entity.Property(permission => permission.TenantId).HasMaxLength(TenantIdMaxLength);
            entity.Property(permission => permission.FolderId).HasMaxLength(IdMaxLength);
            entity.Property(permission => permission.Path).HasMaxLength(PathMaxLength);
            entity.Property(permission => permission.SubjectId).HasMaxLength(ActorIdMaxLength);
            entity.Property(permission => permission.Role).HasMaxLength(32);
            entity.HasIndex(permission => new { permission.TenantId, permission.FolderId });
            entity.HasIndex(permission => new { permission.TenantId, permission.SubjectId, permission.FolderId }).IsUnique();
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
