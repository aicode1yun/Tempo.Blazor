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
        services.Configure<ReportServerQuotaOptions>(_ => { });
        services.AddReportServerSecurity();
        return services;
    }

    /// <summary>Adds middleware that populates <see cref="ReportServerRequestContext"/>.</summary>
    public static IApplicationBuilder UseTempoReportServerTenantContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ReportServerTenantMiddleware>();
    }

    /// <summary>Maps Tempo Report Server API endpoints.</summary>
    public static RouteGroupBuilder MapTempoReportServerApi(this IEndpointRouteBuilder endpoints, string prefix = "/api")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(prefix);
        MapFolders(group);
        MapReports(group);
        MapRender(group);
        MapDataSources(group);
        return group;
    }

    /// <summary>Ensures the development database exists.</summary>
    public static async Task EnsureTempoReportServerDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportServerDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
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
            ReportServerRequestContext context,
            Microsoft.Extensions.Options.IOptions<ReportServerQuotaOptions> quotas,
            CancellationToken cancellationToken) =>
        {
            SetTenant(context, request.TenantId);
            var report = await store.GetReportAsync(request.TenantId, request.ReportId, cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return Results.NotFound();
            }

            var result = await renderer.RenderAsync(report, request, context.ExecutionContext, cancellationToken).ConfigureAwait(false);
            return result.PageCount > quotas.Value.MaxSynchronousPages
                ? Results.Problem("The report exceeded the synchronous page quota.", statusCode: StatusCodes.Status413PayloadTooLarge)
                : Results.Ok(result);
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

    private static void SetTenant(ReportServerRequestContext context, string tenantId)
    {
        var current = context.ExecutionContext;
        context.Set(current with { TenantId = tenantId });
    }
}
