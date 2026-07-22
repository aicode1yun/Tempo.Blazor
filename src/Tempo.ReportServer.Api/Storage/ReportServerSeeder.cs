using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// Minimal-data seed options, bound from the <c>Database:Seed</c> section. Disabled by default so
/// tests and lightweight hosts start with an empty catalog; a deployment opts in to provision the
/// baseline a fresh tenant needs (a root folder and an optional owner grant).
/// </summary>
public sealed record ReportServerSeedOptions
{
    /// <summary>When <see langword="true"/>, the seeder runs at startup (idempotently).</summary>
    public bool Enabled { get; init; }

    /// <summary>Tenant the baseline is provisioned for.</summary>
    public string TenantId { get; init; } = "default";

    /// <summary>Display name of the seeded root folder (canonical path is always <c>/</c>).</summary>
    public string RootFolderName { get; init; } = "Root";

    /// <summary>Optional owner subject (OIDC <c>sub</c>) granted the owner role on the root folder.</summary>
    public string? OwnerSubject { get; init; }

    /// <summary>Role granted to <see cref="OwnerSubject"/> on the root folder (Admin, Author, or Viewer).</summary>
    public string OwnerRole { get; init; } = "Admin";
}

/// <summary>
/// Idempotently provisions the minimal catalog data a fresh tenant needs: a root folder at path
/// <c>/</c> and, when configured, an owner permission grant on it. Safe to run on every startup —
/// each element is inserted only when absent.
/// </summary>
public static class ReportServerSeeder
{
    private const string RootPath = "/";
    private const string RootFolderId = "root";

    /// <summary>Runs the seed against the supplied context under the seed tenant's ambient scope.</summary>
    public static async Task<bool> SeedAsync(
        ReportServerDbContext dbContext,
        ReportServerSeedOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            throw new ArgumentException("Seed TenantId is required.", nameof(options));
        }

        var seededSomething = false;

        var rootFolder = await dbContext.Folders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(folder => folder.TenantId == options.TenantId && folder.Path == RootPath, cancellationToken)
            .ConfigureAwait(false);
        if (rootFolder is null)
        {
            rootFolder = new ReportFolderEntity
            {
                TenantId = options.TenantId,
                FolderId = RootFolderId,
                ParentFolderId = null,
                Name = string.IsNullOrWhiteSpace(options.RootFolderName) ? "Root" : options.RootFolderName.Trim(),
                Path = RootPath,
            };
            dbContext.Folders.Add(rootFolder);
            seededSomething = true;
            logger?.LogInformation("Seeded root folder for tenant {TenantId}.", options.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(options.OwnerSubject))
        {
            var grantExists = await dbContext.FolderPermissions
                .AnyAsync(
                    permission => permission.TenantId == options.TenantId
                        && permission.SubjectId == options.OwnerSubject
                        && permission.FolderId == rootFolder.FolderId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!grantExists)
            {
                dbContext.FolderPermissions.Add(new ReportFolderPermissionEntity
                {
                    TenantId = options.TenantId,
                    FolderId = rootFolder.FolderId,
                    Path = RootPath,
                    SubjectId = options.OwnerSubject,
                    Role = string.IsNullOrWhiteSpace(options.OwnerRole) ? "Admin" : options.OwnerRole,
                });
                seededSomething = true;
                logger?.LogInformation(
                    "Seeded owner grant {Role} for subject {Subject} on the root folder of tenant {TenantId}.",
                    options.OwnerRole,
                    options.OwnerSubject,
                    options.TenantId);
            }
        }

        if (seededSomething)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return seededSomething;
    }

    /// <summary>
    /// Resolves the seed options and, when enabled, runs the seed inside a fresh DI scope whose ambient
    /// tenant is the seed tenant. A no-op when seeding is disabled.
    /// </summary>
    public static async Task SeedFromServicesAsync(IServiceProvider services, ReportServerSeedOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return;
        }

        using var scope = services.CreateScope();
        var requestContext = scope.ServiceProvider.GetRequiredService<ReportServerRequestContext>();
        requestContext.Set(new ReportExecutionContext(options.TenantId, "seed", "en-US"));
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportServerDbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Tempo.ReportServer.Api.Storage.Seeder");
        await SeedAsync(dbContext, options, logger, cancellationToken).ConfigureAwait(false);
    }
}
