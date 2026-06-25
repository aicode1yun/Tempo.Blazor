using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Tests.Security;

public sealed class ReportEndpointAuthorizationTests
{
    [Fact]
    public async Task Endpoints_EnforceCrudRenderAndDataSourcePermissions()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var viewerRender = await PostJsonAsync(
            client,
            "/folders/finance/reports/orders/render",
            UserHeaders("tenant-a", "viewer-1", "Viewer"));
        var viewerCrud = await PostJsonAsync(
            client,
            "/folders/finance/reports",
            UserHeaders("tenant-a", "viewer-1", "Viewer"));
        var authorDataSourceWithoutGrant = await PostJsonAsync(
            client,
            "/folders/finance/datasources",
            UserHeaders("tenant-a", "author-2", "Author"));
        var authorDataSourceWithGrant = await PostJsonAsync(
            client,
            "/folders/finance/datasources",
            UserHeaders("tenant-a", "author-1", "Author"));
        var tenantB = await PostJsonAsync(
            client,
            "/folders/finance/datasources",
            UserHeaders("tenant-b", "author-1", "Author"));

        viewerRender.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        viewerCrud.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        authorDataSourceWithoutGrant.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        authorDataSourceWithGrant.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        tenantB.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoints_ApplyApiKeyScopesAndRevocation()
    {
        await using var app = await CreateAppAsync();
        var keyStore = app.Services.GetRequiredService<IReportApiKeyStore>();
        var renderKey = await keyStore.CreateAsync(
            "tenant-a",
            "embedded-app",
            ReportPermission.Render | ReportPermission.Export);
        var revokedKey = await keyStore.CreateAsync("tenant-a", "old-app", ReportPermission.Render);
        await keyStore.RevokeAsync(revokedKey.KeyId, "tenant-a", "admin");
        var client = app.GetTestClient();

        var render = await PostJsonAsync(
            client,
            "/folders/finance/reports/orders/render",
            ApiKeyHeaders(renderKey.PlainTextKey));
        var edit = await PostJsonAsync(
            client,
            "/folders/finance/reports",
            ApiKeyHeaders(renderKey.PlainTextKey));
        var revoked = await PostJsonAsync(
            client,
            "/folders/finance/reports/orders/render",
            ApiKeyHeaders(revokedKey.PlainTextKey));

        render.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        edit.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        revoked.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoints_WriteAuditEventsForAllowedAndDeniedOperations()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        await PostJsonAsync(
            client,
            "/folders/finance/reports/orders/render",
            UserHeaders("tenant-a", "viewer-1", "Viewer"));
        await PostJsonAsync(
            client,
            "/folders/finance/acl",
            UserHeaders("tenant-a", "viewer-1", "Viewer"));

        var audit = app.Services.GetRequiredService<IReportAuditLog>();
        var events = await audit.ListAsync("tenant-a");

        events.Should().Contain(e =>
            e.Action == ReportAuditAction.RenderReport &&
            e.ActorId == "viewer-1" &&
            e.Outcome == ReportAuditOutcome.Allowed);
        events.Should().Contain(e =>
            e.Action == ReportAuditAction.ChangeAcl &&
            e.ActorId == "viewer-1" &&
            e.Outcome == ReportAuditOutcome.Denied);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddReportServerSecurity();
        var app = builder.Build();

        var permissionStore = app.Services.GetRequiredService<IReportPermissionStore>();
        var context = new ReportExecutionContext("tenant-a", "admin", "en-US");
        await permissionStore.SaveFolderAsync(new ReportFolderPermissionNode("finance"), context);
        await permissionStore.SetAclEntriesAsync(
            "finance",
            [ReportFolderAclEntry.AllowUser("finance", "author-1", ReportPermission.ManageDataSources)],
            context);

        app.MapPost("/folders/{folderId}/reports", () => Results.Ok("changed"))
            .RequireReportPermission(
                ReportPermission.EditDefinition,
                ReportResourceKind.ReportDefinition,
                requiresAuthorRole: true)
            .WithReportAudit(ReportAuditAction.ChangeDefinition, "folderId");
        app.MapPost("/folders/{folderId}/reports/{reportId}/render", () => Results.Ok("rendered"))
            .RequireReportPermission(ReportPermission.Render, ReportResourceKind.Render)
            .WithReportAudit(ReportAuditAction.RenderReport, "reportId");
        app.MapPost("/folders/{folderId}/datasources", () => Results.Ok("datasource"))
            .RequireReportPermission(
                ReportPermission.ManageDataSources,
                ReportResourceKind.DataSource,
                requiresAuthorRole: true)
            .WithReportAudit(ReportAuditAction.ChangeDataSource, "folderId");
        app.MapPost("/folders/{folderId}/acl", () => Results.Ok("acl"))
            .RequireReportPermission(ReportPermission.ManagePermissions, ReportResourceKind.Acl)
            .WithReportAudit(ReportAuditAction.ChangeAcl, "folderId");

        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string uri,
        Action<HttpRequestMessage> configure)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        configure(request);
        return client.SendAsync(request);
    }

    private static Action<HttpRequestMessage> UserHeaders(string tenantId, string userId, string roles)
        => request =>
        {
            request.Headers.Add(ReportSecurityHeaders.TenantId, tenantId);
            request.Headers.Add(ReportSecurityHeaders.UserId, userId);
            request.Headers.Add(ReportSecurityHeaders.Roles, roles);
        };

    private static Action<HttpRequestMessage> ApiKeyHeaders(string key)
        => request => request.Headers.Add(ReportSecurityHeaders.ApiKey, key);
}
