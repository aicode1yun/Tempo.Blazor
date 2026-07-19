using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Tempo.ReportServer.Api.Rendering;
using Tempo.ReportServer.Api.Security;
using Tempo.ReportServer.Api.Storage;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api;

/// <summary>Service and endpoint extensions for Tempo Report Server API.</summary>
public static class ReportServerApiExtensions
{
    /// <summary>Adds the report server API using a SQLite development store by default.</summary>
    public static IServiceCollection AddTempoReportServerApi(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ReportServerRequestContext>();
        services.AddDbContext<ReportServerDbContext>(configureDatabase ?? (options => options.UseSqlite("Data Source=tempo-reportserver.db")));
        services.TryAddScoped<IReportServerStore, EfReportServerStore>();
        services.TryAddScoped<IReportServerRenderer, ReportServerRenderer>();
        services.TryAddScoped<IReportDataProvider, EmptyReportDataProvider>();
        services.TryAddSingleton<IReportRenderJobQueue, InMemoryReportRenderJobQueue>();
        services.TryAddSingleton<ReportRenderMetrics>();
        services.TryAddSingleton<IReportRenderExecutor, ReportRenderExecutor>();

        // The persistent schedule store and a system clock are always available so the scheduling
        // endpoints work in any host. The background worker and delivery channels are opt-in through
        // AddTempoReportServerScheduling so lightweight hosts and contract tests do not start a poller.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<Scheduling.IReportScheduleStore, Storage.EfReportScheduleStore>();
        services.Configure<ReportServerQuotaOptions>(_ => { });
        services.AddOptions<ReportServerApiOptions>();
        services.AddReportServerSecurity();
        services.AddHealthChecks();
        return services;
    }

    /// <summary>
    /// Replaces the default in-memory API key store and audit log with the EF Core persistent
    /// implementations (backed by <see cref="ReportServerDbContext"/>). Call after
    /// <see cref="AddTempoReportServerApi"/>. Selectable from configuration key
    /// <c>Security:Persistence = Ef</c>.
    /// </summary>
    public static IServiceCollection UseEfReportServerSecurityStores(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IReportApiKeyStore>();
        services.RemoveAll<IReportAuditLog>();
        services.RemoveAll<IReportPermissionStore>();
        services.RemoveAll<IReportServerUserProvisioner>();
        services.AddScoped<IReportApiKeyStore, EfReportApiKeyStore>();
        services.AddScoped<IReportAuditLog, EfReportAuditLog>();
        services.AddScoped<IReportPermissionStore, EfReportFolderPermissionStore>();
        services.AddScoped<EfReportFolderPermissionStore>();
        services.AddScoped<IReportServerUserProvisioner, EfReportServerUserProvisioner>();
        // The permission resolver is a singleton by default; re-register it as scoped so it can
        // depend on the scoped EF permission store.
        services.RemoveAll<IReportPermissionResolver>();
        services.AddScoped<IReportPermissionResolver, ReportPermissionResolver>();
        services.RemoveAll<IReportHttpSecurityContextFactory>();
        services.AddScoped<IReportHttpSecurityContextFactory, ReportHttpSecurityContextFactory>();
        return services;
    }

    /// <summary>Adds middleware that populates <see cref="ReportServerRequestContext"/>.</summary>
    public static IApplicationBuilder UseTempoReportServerTenantContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ReportServerTenantMiddleware>();
    }

    /// <summary>Maps Tempo Report Server API endpoints.</summary>
    /// <remarks>
    /// The returned <see cref="RouteGroupBuilder"/> covers only the <paramref name="prefix"/> group
    /// (default <c>/api</c>); a host can chain <c>.RequireAuthorization(...)</c> on it to protect the
    /// whole catalog. The anonymous host diagnostics endpoints (<c>/health</c>, <c>/version</c>) are
    /// mapped on the root and are intentionally excluded from that group so they stay unauthenticated.
    /// </remarks>
    public static RouteGroupBuilder MapTempoReportServerApi(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MapHostDiagnostics(endpoints);

        var group = endpoints.MapGroup(prefix);
        MapFolders(group);
        MapReports(group);
        MapRender(group);
        MapDataSources(group);
        MapSchedules(group);
        MapApiKeys(group);
        MapAudit(group);
        MapPermissions(group);
        MapResolve(group);
        return group;
    }

    private static void MapHostDiagnostics(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health").AllowAnonymous();

        endpoints.MapGet("/version", () => Results.Ok(GetVersion())).AllowAnonymous();
    }

    private static ReportServerVersionDto GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ReportServerApiExtensions).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var assemblyVersion = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        return new ReportServerVersionDto
        {
            Version = string.IsNullOrWhiteSpace(informational) ? assemblyVersion : informational,
            AssemblyVersion = assemblyVersion,
        };
    }

    /// <summary>
    /// Ensures the report server database is ready.
    /// On SQL Server (production/MSSQL tests) the schema is applied through the authored EF Core
    /// migrations; on other providers (SQLite development/tests) the schema is created directly.
    /// </summary>
    public static async Task EnsureTempoReportServerDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportServerDbContext>();
        if (dbContext.Database.IsSqlServer())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void MapFolders(RouteGroupBuilder group)
    {
        group.MapGet("/folders", async (
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return Results.Ok(await store.GetFoldersAsync(tenantId, cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/folders", async (
            CreateReportFolderRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                request.TenantId, request.ParentFolderId,
                ReportPermission.EditDefinition, ReportResourceKind.Folder, ReportAuditAction.ChangeDefinition,
                request.ParentFolderId ?? string.Empty, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var created = await store.CreateFolderAsync(request, cancellationToken).ConfigureAwait(false);
            await WriteAllowedAuditAsync(
                auditLog, principal, request.TenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.Folder, created.FolderId,
                cancellationToken).ConfigureAwait(false);
            return Results.Created("/api/folders", created);
        });

        group.MapPut("/folders/{folderId}", async (
            string folderId,
            string tenantId,
            UpdateReportFolderRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                tenantId, folderId,
                ReportPermission.EditDefinition, ReportResourceKind.Folder, ReportAuditAction.ChangeDefinition,
                folderId, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var folder = await store.UpdateFolderAsync(tenantId, folderId, request, cancellationToken).ConfigureAwait(false);
            if (folder is null)
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.Folder, folderId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(folder);
        });

        group.MapPost("/folders/{folderId}/move", async (
            string folderId,
            string tenantId,
            MoveReportFolderRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                tenantId, folderId,
                ReportPermission.EditDefinition, ReportResourceKind.Folder, ReportAuditAction.ChangeDefinition,
                folderId, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var folder = await store.MoveFolderAsync(tenantId, folderId, request, cancellationToken).ConfigureAwait(false);
            if (folder is null)
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.Folder, folderId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(folder);
        });

        group.MapDelete("/folders/{folderId}", async (
            string folderId,
            string tenantId,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                tenantId, folderId,
                ReportPermission.EditDefinition, ReportResourceKind.Folder, ReportAuditAction.ChangeDefinition,
                folderId, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            if (!await store.DeleteFolderAsync(tenantId, folderId, cancellationToken).ConfigureAwait(false))
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.Folder, folderId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });
    }

    private static void MapReports(RouteGroupBuilder group)
    {
        group.MapPost("/reports/search", async (
            ReportSearchRequestDto request,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            return Results.Ok(await store.SearchReportsAsync(request, cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/reports", async (
            CreateReportRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                request.TenantId, request.FolderId,
                ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, ReportAuditAction.ChangeDefinition,
                request.FolderId, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var created = await store.CreateReportAsync(request, context.ExecutionContext.UserId, cancellationToken).ConfigureAwait(false);
            await WriteAllowedAuditAsync(
                auditLog, principal, request.TenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, created.ReportId,
                cancellationToken).ConfigureAwait(false);
            return Results.Created("/api/reports", created);
        });

        group.MapGet("/reports/{reportId}", async (
            string reportId,
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var report = await store.GetReportAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false);
            return report is null ? Results.NotFound() : Results.Ok(report);
        });

        group.MapDelete("/reports/{reportId}", async (
            string reportId,
            string tenantId,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure, _) = await AuthorizeReportOperationAsync(
                http, contextFactory, resolver, auditLog, store,
                tenantId, reportId,
                ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, ReportAuditAction.ChangeDefinition,
                cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            if (!await store.DeleteReportAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false))
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, reportId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapPost("/reports/{reportId}/move", async (
            string reportId,
            string tenantId,
            MoveReportRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure, _) = await AuthorizeReportOperationAsync(
                http, contextFactory, resolver, auditLog, store,
                tenantId, reportId,
                ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, ReportAuditAction.ChangeDefinition,
                cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var report = await store.MoveReportAsync(tenantId, reportId, request, cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, reportId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(report);
        });

        group.MapGet("/reports/{reportId}/parameters", async (
            string reportId,
            string tenantId,
            IReportServerStore store,
            IReportServerRenderer renderer,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var report = await store.GetReportAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false);
            return report is null
                ? Results.NotFound()
                : Results.Ok(await renderer.GetParametersAsync(report, cancellationToken).ConfigureAwait(false));
        });

        group.MapGet("/reports/{reportId}/revisions", async (
            string reportId,
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return Results.Ok(await store.GetRevisionsAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/reports/{reportId}/revisions", async (
            string reportId,
            UpdateReportDefinitionRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            request = request with { ReportId = reportId };
            SetTenant(context, request.TenantId);
            var (principal, failure, _) = await AuthorizeReportOperationAsync(
                http, contextFactory, resolver, auditLog, store,
                request.TenantId, reportId,
                ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, ReportAuditAction.ChangeDefinition,
                cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var revision = await store.AddRevisionAsync(request, context.ExecutionContext.UserId, cancellationToken).ConfigureAwait(false);
            if (revision is null)
            {
                return Results.Conflict();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, request.TenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, reportId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(revision);
        });

        group.MapPost("/reports/{reportId}/publish", async (
            string reportId,
            string tenantId,
            PublishReportRevisionRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure, _) = await AuthorizeReportOperationAsync(
                http, contextFactory, resolver, auditLog, store,
                tenantId, reportId,
                ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, ReportAuditAction.ChangeDefinition,
                cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var revision = await store.PublishRevisionAsync(tenantId, reportId, request, cancellationToken).ConfigureAwait(false);
            if (revision is null)
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, reportId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(revision);
        });

        group.MapPost("/reports/{reportId}/rollback", async (
            string reportId,
            string tenantId,
            RollbackReportRevisionRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure, _) = await AuthorizeReportOperationAsync(
                http, contextFactory, resolver, auditLog, store,
                tenantId, reportId,
                ReportPermission.EditDefinition, ReportResourceKind.ReportDefinition, ReportAuditAction.ChangeDefinition,
                cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var revision = await store.RollbackAsync(tenantId, reportId, request, context.ExecutionContext.UserId, cancellationToken).ConfigureAwait(false);
            if (revision is null)
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDefinition, ReportResourceKind.ReportDefinition, reportId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(revision);
        });
    }

    private static void MapRender(RouteGroupBuilder group)
    {
        group.MapPost("/render", async (
            RenderReportRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            IReportServerRenderer renderer,
            IReportRenderExecutor executor,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            var (principal, failure, report) = await AuthorizeReportOperationAsync(
                http, contextFactory, resolver, auditLog, store,
                request.TenantId, request.ReportId,
                ReportPermission.Render, ReportResourceKind.Render, ReportAuditAction.RenderReport,
                cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var execution = await executor
                .ExecuteAsync(renderer, report!, request, context.ExecutionContext, cancellationToken)
                .ConfigureAwait(false);
            if (execution.Outcome == ReportRenderOutcome.Succeeded)
            {
                await WriteAllowedAuditAsync(
                    auditLog, principal, request.TenantId,
                    ReportAuditAction.RenderReport, ReportResourceKind.Render, request.ReportId,
                    cancellationToken).ConfigureAwait(false);
            }

            return execution.Outcome switch
            {
                ReportRenderOutcome.Succeeded => Results.Ok(execution.Result),
                ReportRenderOutcome.PageQuotaExceeded => Results.Problem(execution.Message, statusCode: StatusCodes.Status413PayloadTooLarge),
                ReportRenderOutcome.OutputTooLarge => Results.Problem(execution.Message, statusCode: StatusCodes.Status413PayloadTooLarge),
                ReportRenderOutcome.TimedOut => Results.Problem(execution.Message, statusCode: StatusCodes.Status504GatewayTimeout),
                ReportRenderOutcome.Overloaded => Results.Problem(execution.Message, statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.Problem("Unknown render outcome.", statusCode: StatusCodes.Status500InternalServerError),
            };
        });

        group.MapPost("/render/jobs", async (
            RenderReportRequestDto request,
            IReportRenderJobQueue queue,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            return Results.Accepted($"/api/render/jobs", await queue.EnqueueAsync(request, cancellationToken).ConfigureAwait(false));
        });

        group.MapGet("/render/jobs/{jobId}", async (
            string jobId,
            string tenantId,
            IReportRenderJobQueue queue,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var job = await queue.GetAsync(tenantId, jobId, cancellationToken).ConfigureAwait(false);
            return job is null ? Results.NotFound() : Results.Ok(job);
        });
    }

    private static void MapDataSources(RouteGroupBuilder group)
    {
        group.MapGet("/datasources", async (
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return Results.Ok(await store.GetDataSourcesAsync(tenantId, cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/datasources", async (
            UpsertReportDataSourceRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                request.TenantId, folderId: null,
                ReportPermission.ManageDataSources, ReportResourceKind.DataSource, ReportAuditAction.ChangeDataSource,
                request.Name, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            var source = await store.UpsertDataSourceAsync(request, cancellationToken).ConfigureAwait(false);
            await WriteAllowedAuditAsync(
                auditLog, principal, request.TenantId,
                ReportAuditAction.ChangeDataSource, ReportResourceKind.DataSource, source.DataSourceId,
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(source);
        });

        group.MapDelete("/datasources/{dataSourceId}", async (
            string dataSourceId,
            string tenantId,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var (principal, failure) = await AuthorizeOperationAsync(
                http, contextFactory, resolver, auditLog,
                tenantId, folderId: null,
                ReportPermission.ManageDataSources, ReportResourceKind.DataSource, ReportAuditAction.ChangeDataSource,
                dataSourceId, cancellationToken, requiresAuthorRole: true).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            if (!await store.DeleteDataSourceAsync(tenantId, dataSourceId, cancellationToken).ConfigureAwait(false))
            {
                return Results.NotFound();
            }

            await WriteAllowedAuditAsync(
                auditLog, principal, tenantId,
                ReportAuditAction.ChangeDataSource, ReportResourceKind.DataSource, dataSourceId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapPost("/datasources/{dataSourceId}/test", async (
            string dataSourceId,
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var source = await store.GetDataSourceAsync(tenantId, dataSourceId, cancellationToken).ConfigureAwait(false);
            return source is null
                ? Results.NotFound()
                : Results.Ok(new ReportDataSourceConnectionTestResultDto
                {
                    Success = !string.IsNullOrWhiteSpace(source.Connection),
                    Message = string.IsNullOrWhiteSpace(source.Connection) ? "Connection is empty." : "Connection metadata is valid.",
                });
        });

        group.MapGet("/datasources/{dataSourceId}/schema", async (
            string dataSourceId,
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var source = await store.GetDataSourceAsync(tenantId, dataSourceId, cancellationToken).ConfigureAwait(false);
            return source is null
                ? Results.NotFound()
                : Results.Ok(new ReportDataSourceSchemaDto
                {
                    Columns =
                    [
                        new ReportDataSourceSchemaColumnDto { Name = "Name", DataType = "String" },
                        new ReportDataSourceSchemaColumnDto { Name = "Value", DataType = "Number" },
                    ],
                });
        });

        group.MapGet("/datasources/{dataSourceId}/preview", async (
            string dataSourceId,
            string tenantId,
            int? top,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var source = await store.GetDataSourceAsync(tenantId, dataSourceId, cancellationToken).ConfigureAwait(false);
            if (source is null)
            {
                return Results.NotFound();
            }

            var count = Math.Clamp(top ?? 5, 1, 50);
            return Results.Ok(new ReportDataSourcePreviewDto
            {
                Rows = Enumerable.Range(1, count)
                    .Select(index => new Dictionary<string, object?>
                    {
                        ["Name"] = $"{source.Name} row {index}",
                        ["Value"] = index,
                    })
                    .ToList(),
            });
        });
    }

    private static void MapSchedules(RouteGroupBuilder group)
    {
        group.MapGet("/schedules", async (
            string tenantId,
            Scheduling.IReportScheduleStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return Results.Ok(await store.ListAsync(tenantId, cancellationToken).ConfigureAwait(false));
        });

        group.MapGet("/schedules/{scheduleId}", async (
            string scheduleId,
            string tenantId,
            Scheduling.IReportScheduleStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var schedule = await store.GetAsync(tenantId, scheduleId, cancellationToken).ConfigureAwait(false);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        });

        group.MapPost("/schedules", async (
            UpsertReportScheduleRequestDto request,
            Scheduling.IReportScheduleStore store,
            TimeProvider timeProvider,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            try
            {
                var schedule = await store.UpsertAsync(request, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                return Results.Ok(schedule);
            }
            catch (FormatException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        group.MapPost("/schedules/{scheduleId}/enabled", async (
            string scheduleId,
            SetReportScheduleEnabledRequestDto request,
            Scheduling.IReportScheduleStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            return await store.SetEnabledAsync(request.TenantId, scheduleId, request.IsEnabled, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapDelete("/schedules/{scheduleId}", async (
            string scheduleId,
            string tenantId,
            Scheduling.IReportScheduleStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return await store.DeleteAsync(tenantId, scheduleId, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapGet("/schedules/{scheduleId}/runs", async (
            string scheduleId,
            string tenantId,
            int? max,
            Scheduling.IReportScheduleStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return Results.Ok(await store.GetRunsAsync(tenantId, scheduleId, max ?? 20, cancellationToken).ConfigureAwait(false));
        });
    }

    private static void MapApiKeys(RouteGroupBuilder group)
    {
        group.MapPost("/apikeys", async (
            CreateReportApiKeyRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportApiKeyStore keyStore,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, request.TenantId, ReportPermission.ManagePermissions, null, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, request.TenantId);
            var created = await keyStore.CreateAsync(
                request.TenantId,
                request.ApplicationId,
                (ReportPermission)request.Permissions,
                request.ExpiresAt,
                cancellationToken).ConfigureAwait(false);
            return Results.Created("/api/apikeys", ToApiKeyResultDto(created));
        });

        group.MapGet("/apikeys", async (
            string tenantId,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportApiKeyStore keyStore,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, tenantId, ReportPermission.ManagePermissions, null, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, tenantId);
            var descriptors = await keyStore.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(descriptors.Select(descriptor => ToApiKeyDto(descriptor, now)).ToArray());
        });

        group.MapPost("/apikeys/{keyId}/rotate", async (
            string keyId,
            RotateReportApiKeyRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportApiKeyStore keyStore,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, request.TenantId, ReportPermission.ManagePermissions, null, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, request.TenantId);
            var rotated = await keyStore.RotateAsync(
                keyId, request.TenantId, principal.ActorId, request.ExpiresAt, cancellationToken).ConfigureAwait(false);
            return rotated is null ? Results.NotFound() : Results.Ok(ToApiKeyResultDto(rotated));
        });

        group.MapPost("/apikeys/{keyId}/revoke", async (
            string keyId,
            RevokeReportApiKeyRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportApiKeyStore keyStore,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, request.TenantId, ReportPermission.ManagePermissions, null, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, request.TenantId);
            var existing = await keyStore.GetAsync(keyId, request.TenantId, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return Results.NotFound();
            }

            await keyStore.RevokeAsync(keyId, request.TenantId, principal.ActorId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });
    }

    private static void MapAudit(RouteGroupBuilder group)
    {
        group.MapGet("/audit", async (
            string tenantId,
            ReportAuditActionDto? action,
            ReportAuditOutcomeDto? outcome,
            string? actorId,
            string? resourceId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? take,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportAuditLog auditLog,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, tenantId, ReportPermission.ManagePermissions, null, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, tenantId);
            var query = new ReportAuditQuery
            {
                TenantId = tenantId,
                Action = action is { } a ? (ReportAuditAction)a : null,
                Outcome = outcome is { } o ? (ReportAuditOutcome)o : null,
                ActorId = actorId,
                ResourceId = resourceId,
                From = from,
                To = to,
                Take = take,
            };
            var events = await auditLog.QueryAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(events.Select(ToAuditEventDto).ToArray());
        });
    }

    private static void MapPermissions(RouteGroupBuilder group)
    {
        group.MapPost("/permissions", async (
            GrantReportPermissionRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportPermissionStore permissionStore,
            IReportAuditLog auditLog,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, request.TenantId, ReportPermission.ManagePermissions, request.FolderId, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, request.TenantId);
            var executionContext = new ReportExecutionContext(request.TenantId, principal.ActorId, "en-US", CancellationToken: cancellationToken);
            var entry = new ReportFolderAclEntry
            {
                TenantId = request.TenantId,
                FolderId = request.FolderId,
                SubjectKind = (ReportAclSubjectKind)request.SubjectKind,
                SubjectId = request.SubjectId,
                Effect = (ReportAclEffect)request.Effect,
                Permissions = (ReportPermission)request.Permissions,
            };
            await permissionStore.GrantAclEntryAsync(request.FolderId, entry, executionContext).ConfigureAwait(false);
            await auditLog.WriteAsync(
                ReportAuditEvent.Allowed(request.TenantId, principal.ActorId, ReportAuditAction.ChangeAcl, ReportResourceKind.Acl, request.FolderId),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(ToAclEntryDto(entry));
        });

        group.MapGet("/permissions", async (
            string tenantId,
            string folderId,
            string? subjectId,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportPermissionStore permissionStore,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, tenantId, ReportPermission.ManagePermissions, folderId, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, tenantId);
            var executionContext = new ReportExecutionContext(tenantId, principal.ActorId, "en-US", CancellationToken: cancellationToken);
            var entries = await permissionStore.ListFolderAclEntriesAsync(folderId, executionContext).ConfigureAwait(false);
            var filtered = string.IsNullOrWhiteSpace(subjectId)
                ? entries
                : entries.Where(entry => string.Equals(entry.SubjectId, subjectId, StringComparison.Ordinal)).ToArray();
            return Results.Ok(filtered.Select(ToAclEntryDto).ToArray());
        });

        group.MapPost("/permissions/revoke", async (
            RevokeReportPermissionRequestDto request,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportPermissionStore permissionStore,
            IReportAuditLog auditLog,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var (principal, failure) = await AuthorizeAdminAsync(
                http, contextFactory, resolver, request.TenantId, ReportPermission.ManagePermissions, request.FolderId, cancellationToken)
                .ConfigureAwait(false);
            if (failure is not null || principal is null)
            {
                return failure!;
            }

            SetTenant(context, request.TenantId);
            var executionContext = new ReportExecutionContext(request.TenantId, principal.ActorId, "en-US", CancellationToken: cancellationToken);
            await permissionStore.RevokeAclEntryAsync(
                request.FolderId,
                (ReportAclSubjectKind)request.SubjectKind,
                request.SubjectId,
                executionContext).ConfigureAwait(false);
            await auditLog.WriteAsync(
                ReportAuditEvent.Allowed(request.TenantId, principal.ActorId, ReportAuditAction.ChangeAcl, ReportResourceKind.Acl, request.FolderId),
                cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });
    }

    private static void MapResolve(RouteGroupBuilder group)
    {
        group.MapGet("/resolve", async (
            string tenantId,
            string? reportId,
            string? path,
            HttpContext http,
            IReportHttpSecurityContextFactory contextFactory,
            IReportPermissionResolver resolver,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await contextFactory.CreateAsync(http, cancellationToken).ConfigureAwait(false);
            if (principal is null)
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(principal.TenantId, tenantId, StringComparison.Ordinal))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            SetTenant(context, tenantId);

            ReportDetailDto? report;
            if (!string.IsNullOrWhiteSpace(reportId))
            {
                report = await store.GetReportAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(path))
            {
                report = await ResolveByPathAsync(store, tenantId, path, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return Results.BadRequest("Either 'reportId' or 'path' must be supplied.");
            }

            if (report is null)
            {
                return Results.NotFound();
            }

            var authorization = await resolver.AuthorizeAsync(
                principal,
                new ReportPermissionRequirement(ReportPermission.View, ReportResourceKind.ReportDefinition),
                report.FolderId,
                cancellationToken).ConfigureAwait(false);
            if (!authorization.Allowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var revisions = await store.GetRevisionsAsync(tenantId, report.ReportId, cancellationToken).ConfigureAwait(false);
            var published = revisions.FirstOrDefault(revision => revision.IsPublished);
            var resolved = published ?? revisions.FirstOrDefault(revision => revision.RevisionId == report.LatestRevisionId);
            return Results.Ok(new ReportResolveResultDto
            {
                TenantId = tenantId,
                ReportId = report.ReportId,
                FolderId = report.FolderId,
                Name = report.Name,
                Description = report.Description,
                LatestRevisionId = report.LatestRevisionId,
                PublishedRevisionId = published?.RevisionId,
                RevisionNumber = resolved?.RevisionNumber ?? 0,
                DefinitionJson = resolved?.DefinitionJson ?? report.DefinitionJson,
                RenderPath = "api/render",
            });
        });
    }

    private static async Task<ReportDetailDto?> ResolveByPathAsync(
        IReportServerStore store,
        string tenantId,
        string path,
        CancellationToken cancellationToken)
    {
        var trimmed = path.Trim().Trim('/');
        var separator = trimmed.LastIndexOf('/');
        if (separator < 0)
        {
            return null;
        }

        var folderPath = "/" + trimmed[..separator];
        var reportName = trimmed[(separator + 1)..];
        var folders = await store.GetFoldersAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var folder = folders.FirstOrDefault(candidate =>
            string.Equals(candidate.Path, folderPath, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
        {
            return null;
        }

        var matches = await store.SearchReportsAsync(
            new ReportSearchRequestDto { TenantId = tenantId, FolderId = folder.FolderId, Query = reportName },
            cancellationToken).ConfigureAwait(false);
        var summary = matches.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, reportName, StringComparison.OrdinalIgnoreCase));
        return summary is null
            ? null
            : await store.GetReportAsync(tenantId, summary.ReportId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the report security principal for an admin/management endpoint and enforces a
    /// tenant-scoped permission. Returns a non-null failure result (401/403) when the caller is not
    /// authenticated, requests a different tenant than the one it is scoped to, or lacks the permission.
    /// </summary>
    private static async Task<(ReportSecurityContext? Principal, IResult? Failure)> AuthorizeAdminAsync(
        HttpContext http,
        IReportHttpSecurityContextFactory contextFactory,
        IReportPermissionResolver resolver,
        string tenantId,
        ReportPermission permission,
        string? folderId,
        CancellationToken cancellationToken)
    {
        var principal = await contextFactory.CreateAsync(http, cancellationToken).ConfigureAwait(false);
        if (principal is null)
        {
            return (null, Results.Unauthorized());
        }

        if (!string.Equals(principal.TenantId, tenantId, StringComparison.Ordinal))
        {
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        var authorization = await resolver.AuthorizeAsync(
            principal,
            new ReportPermissionRequirement(permission, ReportResourceKind.Acl),
            folderId,
            cancellationToken).ConfigureAwait(false);
        return authorization.Allowed
            ? (principal, null)
            : (null, Results.StatusCode(StatusCodes.Status403Forbidden));
    }

    /// <summary>
    /// Enforces a folder-scoped permission on a live catalog/render/data-source operation and audits a
    /// denial. Returns the resolved principal on success (the caller writes the <c>Allowed</c> audit
    /// after the operation succeeds), a non-null failure result (403) on tenant mismatch or denial, or
    /// <c>(null, null)</c> when no principal is present. The latter is the open/development-host case:
    /// production hosts front the API group with <c>RequireAuthorization</c>, so a request that reaches
    /// a handler there always carries a resolved principal and is fully enforced and audited.
    /// </summary>
    private static async Task<(ReportSecurityContext? Principal, IResult? Failure)> AuthorizeOperationAsync(
        HttpContext http,
        IReportHttpSecurityContextFactory contextFactory,
        IReportPermissionResolver resolver,
        IReportAuditLog auditLog,
        string tenantId,
        string? folderId,
        ReportPermission permission,
        ReportResourceKind resourceKind,
        ReportAuditAction auditAction,
        string resourceId,
        CancellationToken cancellationToken,
        bool requiresAuthorRole = false)
    {
        var principal = await contextFactory.CreateAsync(http, cancellationToken).ConfigureAwait(false);
        if (principal is null)
        {
            // Fail closed: a missing principal (e.g. a valid bearer paired with a bogus X-Api-Key header,
            // which the factory rejects) must be a 401, not a silent bypass. Only an explicit dev/test
            // opt-in lets an anonymous request through, and then without any audit trail.
            return AllowAnonymous(http)
                ? (null, null)
                : (null, Results.StatusCode(StatusCodes.Status401Unauthorized));
        }

        if (!string.Equals(principal.TenantId, tenantId, StringComparison.Ordinal))
        {
            // A tenant mismatch is deliberately NOT audited: the request carries no trustworthy tenant
            // scope to attribute the event to (the body/route tenant differs from the principal's tenant).
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        var authorization = await resolver.AuthorizeAsync(
            principal,
            new ReportPermissionRequirement(permission, resourceKind, requiresAuthorRole),
            folderId,
            cancellationToken).ConfigureAwait(false);
        if (!authorization.Allowed)
        {
            await auditLog.WriteAsync(
                ReportAuditEvent.Denied(tenantId, principal.ActorId, auditAction, resourceKind, resourceId),
                cancellationToken).ConfigureAwait(false);
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        return (principal, null);
    }

    private static bool AllowAnonymous(HttpContext http)
        => http.RequestServices.GetRequiredService<IOptions<ReportServerApiOptions>>().Value.AllowAnonymousOperations;

    /// <summary>
    /// Enforces a permission on a report-scoped operation, resolving the target report's folder for
    /// folder-aware authorization. Order avoids cross-tenant existence leaks: a tenant mismatch is
    /// rejected before the report is looked up, and an unknown report returns 404 for every caller.
    /// </summary>
    private static async Task<(ReportSecurityContext? Principal, IResult? Failure, ReportDetailDto? Report)> AuthorizeReportOperationAsync(
        HttpContext http,
        IReportHttpSecurityContextFactory contextFactory,
        IReportPermissionResolver resolver,
        IReportAuditLog auditLog,
        IReportServerStore store,
        string tenantId,
        string reportId,
        ReportPermission permission,
        ReportResourceKind resourceKind,
        ReportAuditAction auditAction,
        CancellationToken cancellationToken,
        bool requiresAuthorRole = false)
    {
        var principal = await contextFactory.CreateAsync(http, cancellationToken).ConfigureAwait(false);
        if (principal is null && !AllowAnonymous(http))
        {
            // Fail closed on a missing principal unless a dev/test host explicitly opted in (see
            // AuthorizeOperationAsync for why a null principal is a rejection, not a bypass).
            return (null, Results.StatusCode(StatusCodes.Status401Unauthorized), null);
        }

        if (principal is not null && !string.Equals(principal.TenantId, tenantId, StringComparison.Ordinal))
        {
            // Tenant mismatch is intentionally NOT audited: there is no trustworthy tenant scope to
            // attribute the event to. Rejecting before the lookup also avoids leaking report existence
            // across tenants.
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden), null);
        }

        var report = await store.GetReportAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false);
        if (report is null)
        {
            return (null, Results.NotFound(), null);
        }

        if (principal is null)
        {
            return (null, null, report);
        }

        var authorization = await resolver.AuthorizeAsync(
            principal,
            new ReportPermissionRequirement(permission, resourceKind, requiresAuthorRole),
            report.FolderId,
            cancellationToken).ConfigureAwait(false);
        if (!authorization.Allowed)
        {
            await auditLog.WriteAsync(
                ReportAuditEvent.Denied(tenantId, principal.ActorId, auditAction, resourceKind, reportId),
                cancellationToken).ConfigureAwait(false);
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden), report);
        }

        return (principal, null, report);
    }

    /// <summary>Writes an <c>Allowed</c> audit event for a principal-backed operation (no-op for open hosts).</summary>
    private static Task WriteAllowedAuditAsync(
        IReportAuditLog auditLog,
        ReportSecurityContext? principal,
        string tenantId,
        ReportAuditAction auditAction,
        ReportResourceKind resourceKind,
        string resourceId,
        CancellationToken cancellationToken)
        => principal is null
            ? Task.CompletedTask
            : auditLog.WriteAsync(
                ReportAuditEvent.Allowed(tenantId, principal.ActorId, auditAction, resourceKind, resourceId),
                cancellationToken);

    private static ReportApiKeyDto ToApiKeyDto(ReportApiKeyDescriptor descriptor, DateTimeOffset now)
        => new()
        {
            KeyId = descriptor.KeyId,
            TenantId = descriptor.TenantId,
            ApplicationId = descriptor.ApplicationId,
            Permissions = (ReportPermissionsDto)descriptor.Permissions,
            CreatedAt = descriptor.CreatedAt,
            ExpiresAt = descriptor.ExpiresAt,
            RevokedAt = descriptor.RevokedAt,
            RevokedByUserId = descriptor.RevokedByUserId,
            IsActive = descriptor.IsActive(now),
        };

    private static CreateReportApiKeyResultDto ToApiKeyResultDto(ReportApiKeyCreationResult result)
        => new()
        {
            KeyId = result.KeyId,
            PlainTextKey = result.PlainTextKey,
            Key = ToApiKeyDto(result.Descriptor, result.Descriptor.CreatedAt),
        };

    private static ReportAuditEventDto ToAuditEventDto(ReportAuditEvent auditEvent)
        => new()
        {
            TenantId = auditEvent.TenantId,
            ActorId = auditEvent.ActorId,
            Action = (ReportAuditActionDto)auditEvent.Action,
            ResourceKind = (ReportResourceKindDto)auditEvent.ResourceKind,
            ResourceId = auditEvent.ResourceId,
            Outcome = (ReportAuditOutcomeDto)auditEvent.Outcome,
            Timestamp = auditEvent.Timestamp,
            Details = auditEvent.Details,
        };

    private static ReportFolderAclEntryDto ToAclEntryDto(ReportFolderAclEntry entry)
        => new()
        {
            TenantId = entry.TenantId,
            FolderId = entry.FolderId,
            SubjectKind = (ReportAclSubjectKindDto)entry.SubjectKind,
            SubjectId = entry.SubjectId,
            Effect = (ReportAclEffectDto)entry.Effect,
            Permissions = (ReportPermissionsDto)entry.Permissions,
        };

    private static void SetTenant(ReportServerRequestContext context, string tenantId)
    {
        var current = context.ExecutionContext;
        context.Set(current with { TenantId = tenantId });
    }
}
