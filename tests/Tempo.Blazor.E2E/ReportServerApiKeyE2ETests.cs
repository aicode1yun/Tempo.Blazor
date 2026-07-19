using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.Blazor.E2E;

/// <summary>
/// Full-stack (Fáze 13 PASS B): API key lifecycle against the real Keycloak-protected Api on SQL
/// Server with the EF security stores. Creates a key as an admin, renders via <c>X-Api-Key</c>
/// (→ 200 + a persisted <c>RenderReport</c> audit row), revokes it (→ a <c>RevokedAt</c> row) and
/// proves the same key then yields 401. DB state is asserted through the Api's own admin endpoints
/// (<c>/api/audit</c>, <c>/api/apikeys</c>), which read the very EF tables the render path writes.
/// </summary>
[TestClass]
[TestCategory("ReportServerFullStack")]
[DoNotParallelize]
public sealed class ReportServerApiKeyE2ETests : ReportServerFullStackE2ETestBase
{
    [TestMethod]
    public async Task ApiKey_Render_Audits_ThenRevoke_Yields401()
    {
        var applicationId = $"e2e-app-{UniqueTag()}";

        // Seed a real report to render (admin bearer, tenant "default").
        var (folderId, _) = await SeedFolderAsync($"ApiKey {UniqueTag()}").ConfigureAwait(false);
        var reportId = await SeedReportAsync(folderId, $"E2E ApiKey Report {UniqueTag()}").ConfigureAwait(false);

        using var admin = await CreateBearerApiClientAsync("admin1").ConfigureAwait(false);

        // 1) Create an API key (View|Render).
        var createResponse = await admin.PostAsJsonAsync("/api/apikeys", new CreateReportApiKeyRequestDto
        {
            TenantId = TenantId,
            ApplicationId = applicationId,
            Permissions = ReportPermissionsDto.View | ReportPermissionsDto.Render,
        }).ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode, "API key creation should return 201.");
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportApiKeyResultDto>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Key creation returned no body.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(created.PlainTextKey), "The one-time plain-text key must be returned.");
        StringAssert.StartsWith(created.PlainTextKey, "tmr_", "The API key uses the tmr_ prefix.");
        var keyId = created.KeyId;

        // 2) Render via X-Api-Key → 200.
        using (var render200 = await RenderWithApiKeyAsync(created.PlainTextKey, reportId).ConfigureAwait(false))
        {
            Assert.AreEqual(HttpStatusCode.OK, render200.StatusCode,
                $"Render with a valid X-Api-Key should return 200. Body: {await render200.Content.ReadAsStringAsync().ConfigureAwait(false)}");
        }

        // 3) Audit row: a RenderReport event by actor "api:{applicationId}" is persisted (best-effort write,
        //    so poll the admin audit endpoint which reads the AuditEvents EF table).
        await PollAsync(async () =>
        {
            var events = await admin.GetFromJsonAsync<List<ReportAuditEventDto>>(
                $"/api/audit?tenantId={TenantId}&action={ReportAuditActionDto.RenderReport}&take=200").ConfigureAwait(false)
                ?? [];
            return events.Any(e =>
                e.Action == ReportAuditActionDto.RenderReport &&
                e.ActorId == $"api:{applicationId}" &&
                e.Outcome == ReportAuditOutcomeDto.Allowed);
        }, $"A RenderReport audit row for actor 'api:{applicationId}' should be persisted.").ConfigureAwait(false);

        // 4) Revoke the key → 204.
        var revokeResponse = await admin.PostAsJsonAsync($"/api/apikeys/{keyId}/revoke", new RevokeReportApiKeyRequestDto
        {
            TenantId = TenantId,
        }).ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.NoContent, revokeResponse.StatusCode, "Revoke should return 204.");

        // 5) The RevokedAt row is visible via the admin listing (reads the ApiKeys EF table).
        await PollAsync(async () =>
        {
            var keys = await admin.GetFromJsonAsync<List<ReportApiKeyDto>>(
                $"/api/apikeys?tenantId={TenantId}").ConfigureAwait(false) ?? [];
            var key = keys.FirstOrDefault(k => k.KeyId == keyId);
            return key is { RevokedAt: not null, IsActive: false };
        }, "The revoked key should have a RevokedAt timestamp and be inactive.").ConfigureAwait(false);

        // 6) Render with the revoked key → 401.
        using var render401 = await RenderWithApiKeyAsync(created.PlainTextKey, reportId).ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.Unauthorized, render401.StatusCode,
            "Render with a revoked X-Api-Key must return 401.");
    }

    private static async Task<HttpResponseMessage> RenderWithApiKeyAsync(string plainTextKey, string reportId)
    {
        var client = CreateAnonymousApiClient();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/render")
            {
                Content = JsonContent.Create(new RenderReportRequestDto
                {
                    TenantId = TenantId,
                    ReportId = reportId,
                    Format = ReportRenderFormat.Snapshot,
                }),
            };
            request.Headers.Add("X-Api-Key", plainTextKey);
            return await client.SendAsync(request).ConfigureAwait(false);
        }
        finally
        {
            client.Dispose();
        }
    }
}
