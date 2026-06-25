using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api;

/// <summary>Scoped request context used by report server stores and render jobs.</summary>
public sealed class ReportServerRequestContext
{
    /// <summary>Current execution context.</summary>
    public ReportExecutionContext ExecutionContext { get; private set; } = new("default", "anonymous", "en-US");

    /// <summary>Sets the current execution context.</summary>
    public void Set(ReportExecutionContext context)
    {
        ExecutionContext = context ?? throw new ArgumentNullException(nameof(context));
    }
}

/// <summary>Middleware that builds a report execution context from claims or development headers.</summary>
public sealed class ReportServerTenantMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the tenant middleware.</summary>
    public ReportServerTenantMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>Builds the request context and invokes the next middleware.</summary>
    public async Task InvokeAsync(HttpContext httpContext, ReportServerRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(requestContext);

        var user = httpContext.User;
        var tenantId = FirstNonEmpty(
            user.FindFirst("tenant_id")?.Value,
            user.FindFirst("tenant")?.Value,
            user.FindFirst("tid")?.Value,
            httpContext.Request.Headers[ReportSecurityHeaders.TenantId].ToString(),
            httpContext.Request.Query["tenantId"].ToString(),
            "default");
        var userId = FirstNonEmpty(
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            user.FindFirst("sub")?.Value,
            httpContext.Request.Headers[ReportSecurityHeaders.UserId].ToString(),
            "anonymous");
        var culture = FirstNonEmpty(
            user.FindFirst("culture")?.Value,
            httpContext.Request.Headers.AcceptLanguage.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            "en-US");
        var claims = user.Claims
            .GroupBy(claim => claim.Type, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);

        requestContext.Set(new ReportExecutionContext(tenantId, userId, culture, claims, httpContext.RequestAborted));
        await _next(httpContext).ConfigureAwait(false);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
