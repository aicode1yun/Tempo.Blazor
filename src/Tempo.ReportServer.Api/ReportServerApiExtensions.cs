using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            return Results.Created("/api/folders", await store.CreateFolderAsync(request, cancellationToken).ConfigureAwait(false));
        });

        group.MapPut("/folders/{folderId}", async (
            string folderId,
            string tenantId,
            UpdateReportFolderRequestDto request,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var folder = await store.UpdateFolderAsync(tenantId, folderId, request, cancellationToken).ConfigureAwait(false);
            return folder is null ? Results.NotFound() : Results.Ok(folder);
        });

        group.MapPost("/folders/{folderId}/move", async (
            string folderId,
            string tenantId,
            MoveReportFolderRequestDto request,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var folder = await store.MoveFolderAsync(tenantId, folderId, request, cancellationToken).ConfigureAwait(false);
            return folder is null ? Results.NotFound() : Results.Ok(folder);
        });

        group.MapDelete("/folders/{folderId}", async (
            string folderId,
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return await store.DeleteFolderAsync(tenantId, folderId, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
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
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            return Results.Created("/api/reports", await store.CreateReportAsync(request, context.ExecutionContext.UserId, cancellationToken).ConfigureAwait(false));
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
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return await store.DeleteReportAsync(tenantId, reportId, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapPost("/reports/{reportId}/move", async (
            string reportId,
            string tenantId,
            MoveReportRequestDto request,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var report = await store.MoveReportAsync(tenantId, reportId, request, cancellationToken).ConfigureAwait(false);
            return report is null ? Results.NotFound() : Results.Ok(report);
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
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            request = request with { ReportId = reportId };
            SetTenant(context, request.TenantId);
            var revision = await store.AddRevisionAsync(request, context.ExecutionContext.UserId, cancellationToken).ConfigureAwait(false);
            return revision is null ? Results.Conflict() : Results.Ok(revision);
        });

        group.MapPost("/reports/{reportId}/publish", async (
            string reportId,
            string tenantId,
            PublishReportRevisionRequestDto request,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var revision = await store.PublishRevisionAsync(tenantId, reportId, request, cancellationToken).ConfigureAwait(false);
            return revision is null ? Results.NotFound() : Results.Ok(revision);
        });

        group.MapPost("/reports/{reportId}/rollback", async (
            string reportId,
            string tenantId,
            RollbackReportRevisionRequestDto request,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            var revision = await store.RollbackAsync(tenantId, reportId, request, context.ExecutionContext.UserId, cancellationToken).ConfigureAwait(false);
            return revision is null ? Results.NotFound() : Results.Ok(revision);
        });
    }

    private static void MapRender(RouteGroupBuilder group)
    {
        group.MapPost("/render", async (
            RenderReportRequestDto request,
            IReportServerStore store,
            IReportServerRenderer renderer,
            IReportRenderExecutor executor,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            var report = await store.GetReportAsync(request.TenantId, request.ReportId, cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return Results.NotFound();
            }

            var execution = await executor
                .ExecuteAsync(renderer, report, request, context.ExecutionContext, cancellationToken)
                .ConfigureAwait(false);
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
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            return Results.Ok(await store.UpsertDataSourceAsync(request, cancellationToken).ConfigureAwait(false));
        });

        group.MapDelete("/datasources/{dataSourceId}", async (
            string dataSourceId,
            string tenantId,
            IReportServerStore store,
            ReportServerRequestContext context,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, tenantId);
            return await store.DeleteDataSourceAsync(tenantId, dataSourceId, cancellationToken).ConfigureAwait(false)
                ? Results.NoContent()
                : Results.NotFound();
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

    private static void SetTenant(ReportServerRequestContext context, string tenantId)
    {
        var current = context.ExecutionContext;
        context.Set(current with { TenantId = tenantId });
    }
}
