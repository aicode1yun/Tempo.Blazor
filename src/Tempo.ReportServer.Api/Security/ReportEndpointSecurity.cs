#pragma warning disable MA0048

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Tempo.ReportServer.Api.Security;

/// <summary>Audit metadata attached to report server endpoints.</summary>
public sealed record ReportAuditMetadata(ReportAuditAction Action, string ResourceRouteKey = "reportId");

/// <summary>Resolves report security context from HTTP requests.</summary>
public interface IReportHttpSecurityContextFactory
{
    /// <summary>Creates a context from the request, or null when credentials are invalid.</summary>
    Task<ReportSecurityContext?> CreateAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

/// <summary>Header/API-key based security context factory for report server endpoints.</summary>
public sealed class ReportHttpSecurityContextFactory : IReportHttpSecurityContextFactory
{
    private readonly IReportApiKeyStore _apiKeyStore;

    /// <summary>Creates a context factory.</summary>
    public ReportHttpSecurityContextFactory(IReportApiKeyStore apiKeyStore)
    {
        _apiKeyStore = apiKeyStore ?? throw new ArgumentNullException(nameof(apiKeyStore));
    }

    /// <inheritdoc />
    public async Task<ReportSecurityContext?> CreateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (httpContext.Request.Headers.TryGetValue(ReportSecurityHeaders.ApiKey, out var apiKeyValues))
        {
            var descriptor = await _apiKeyStore.ValidateAsync(apiKeyValues.ToString(), cancellationToken).ConfigureAwait(false);
            return descriptor is null ? null : ReportSecurityContext.ForApiKey(descriptor);
        }

        var tenantId = httpContext.Request.Headers[ReportSecurityHeaders.TenantId].ToString();
        var userId = httpContext.Request.Headers[ReportSecurityHeaders.UserId].ToString();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var roles = ParseRoles(httpContext.Request.Headers[ReportSecurityHeaders.Roles].ToString());
        return ReportSecurityContext.ForUser(tenantId, userId, roles);
    }

    private static IReadOnlyList<ReportServerRole> ParseRoles(string value)
        => value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => Enum.TryParse<ReportServerRole>(role, ignoreCase: true, out var parsed) ? parsed : (ReportServerRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .Distinct()
            .ToArray();
}

/// <summary>Endpoint filter enforcing report server permissions.</summary>
public sealed class ReportAuthorizationEndpointFilter : IEndpointFilter
{
    private readonly IReportHttpSecurityContextFactory _contextFactory;
    private readonly IReportPermissionResolver _resolver;
    private readonly IReportAuditLog _auditLog;

    /// <summary>Creates an endpoint filter.</summary>
    public ReportAuthorizationEndpointFilter(
        IReportHttpSecurityContextFactory contextFactory,
        IReportPermissionResolver resolver,
        IReportAuditLog auditLog)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var requirement = httpContext.GetEndpoint()?.Metadata.GetMetadata<ReportPermissionRequirement>();
        if (requirement is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        var principal = await _contextFactory.CreateAsync(httpContext, httpContext.RequestAborted).ConfigureAwait(false);
        if (principal is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Results.Unauthorized();
        }

        var folderId = ResolveRouteValue(httpContext, requirement.FolderRouteKey);
        var authorization = await _resolver.AuthorizeAsync(
            principal,
            requirement,
            folderId,
            httpContext.RequestAborted).ConfigureAwait(false);
        if (!authorization.Allowed)
        {
            await AuditAsync(httpContext, principal, requirement, ReportAuditOutcome.Denied).ConfigureAwait(false);
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await next(context).ConfigureAwait(false);
        await AuditAsync(httpContext, principal, requirement, ReportAuditOutcome.Allowed).ConfigureAwait(false);
        return result;
    }

    private async Task AuditAsync(
        HttpContext httpContext,
        ReportSecurityContext principal,
        ReportPermissionRequirement requirement,
        ReportAuditOutcome outcome)
    {
        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<ReportAuditMetadata>();
        if (metadata is null)
        {
            return;
        }

        var resourceId = ResolveRouteValue(httpContext, metadata.ResourceRouteKey) ??
            ResolveRouteValue(httpContext, requirement.FolderRouteKey) ??
            string.Empty;
        var auditEvent = new ReportAuditEvent
        {
            TenantId = principal.TenantId,
            ActorId = principal.ActorId,
            Action = metadata.Action,
            ResourceKind = requirement.ResourceKind,
            ResourceId = resourceId,
            Outcome = outcome,
            Timestamp = DateTimeOffset.UtcNow,
        };
        await _auditLog.WriteAsync(auditEvent, httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static string? ResolveRouteValue(HttpContext httpContext, string key)
        => httpContext.Request.RouteValues.TryGetValue(key, out var value)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
}

/// <summary>Service and endpoint extension methods for report server security.</summary>
public static class ReportServerSecurityExtensions
{
    /// <summary>Adds report server security services.</summary>
    public static IServiceCollection AddReportServerSecurity(this IServiceCollection services)
    {
        services.TryAddSingleton<IReportPermissionStore, InMemoryReportPermissionStore>();
        services.TryAddSingleton<IReportPermissionResolver, ReportPermissionResolver>();
        services.TryAddSingleton<IReportApiKeyStore, InMemoryReportApiKeyStore>();
        services.TryAddSingleton<IReportAuditLog, InMemoryReportAuditLog>();
        services.TryAddSingleton<IReportHttpSecurityContextFactory, ReportHttpSecurityContextFactory>();
        services.TryAddTransient<ReportAuthorizationEndpointFilter>();
        return services;
    }

    /// <summary>Requires a report permission on an endpoint.</summary>
    public static RouteHandlerBuilder RequireReportPermission(
        this RouteHandlerBuilder builder,
        ReportPermission permission,
        ReportResourceKind resourceKind,
        bool requiresAuthorRole = false,
        string folderRouteKey = "folderId")
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .WithMetadata(new ReportPermissionRequirement(permission, resourceKind, requiresAuthorRole, folderRouteKey))
            .AddEndpointFilter<ReportAuthorizationEndpointFilter>();
    }

    /// <summary>Adds audit metadata to an endpoint.</summary>
    public static RouteHandlerBuilder WithReportAudit(
        this RouteHandlerBuilder builder,
        ReportAuditAction action,
        string resourceRouteKey = "reportId")
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new ReportAuditMetadata(action, resourceRouteKey));
    }
}

#pragma warning restore MA0048
