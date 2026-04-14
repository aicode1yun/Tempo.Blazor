using Microsoft.EntityFrameworkCore;

namespace Tempo.Blazor.Demo.Api.Data;

/// <summary>EF Core database context for diagram history persistence.</summary>
public sealed class DemoDiagramDbContext : DbContext
{
    public DemoDiagramDbContext(DbContextOptions<DemoDiagramDbContext> options)
        : base(options)
    {
    }

    public DbSet<DiagramSnapshotEntity> DiagramSnapshots => Set<DiagramSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiagramSnapshotEntity>(entity =>
        {
            entity.ToTable("DiagramSnapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DiagramId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(256);
            entity.Property(e => e.Json).IsRequired();
            entity.HasIndex(e => new { e.DiagramId, e.Version }).IsUnique();
            entity.HasIndex(e => e.DiagramId);
        });
    }
}
