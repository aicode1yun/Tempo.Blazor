using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tempo.ReportServer.Api.Security;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Dtos;
using Tempo.Reporting.Abstractions.Serialization;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Integration specification for the Fáze 5b admin/viewer HTTP surface: API key management, audit
/// querying, folder-permission grants, and viewer report resolution. Verified through an in-process
/// host mirroring the production authentication (API key) and per-endpoint authorization wiring.
/// </summary>
public sealed class ReportServerF5bAdminApiTests
{
    private const string TenantId = "tenant-a";

    [Fact]
    public async Task ApiKeys_CreateListRotateRevoke_RoundTripsThroughHttp()
    {
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());

        var created = await api.CreateApiKeyAsync(new CreateReportApiKeyRequestDto
        {
            TenantId = TenantId,
            ApplicationId = "embedding-app",
            Permissions = ReportPermissionsDto.View | ReportPermissionsDto.Render,
        });
        created.PlainTextKey.Should().NotBeNullOrWhiteSpace();
        created.Key.Permissions.Should().Be(ReportPermissionsDto.View | ReportPermissionsDto.Render);
        created.Key.IsActive.Should().BeTrue();

        var keys = await api.GetApiKeysAsync(TenantId);
        keys.Should().Contain(key => key.KeyId == created.KeyId);

        var rotated = await api.RotateApiKeyAsync(created.KeyId, new RotateReportApiKeyRequestDto { TenantId = TenantId });
        rotated.KeyId.Should().NotBe(created.KeyId);
        rotated.PlainTextKey.Should().NotBe(created.PlainTextKey);

        var afterRotate = await api.GetApiKeysAsync(TenantId);
        afterRotate.Single(key => key.KeyId == created.KeyId).IsActive.Should().BeFalse("rotation revokes the source key");
        afterRotate.Single(key => key.KeyId == rotated.KeyId).IsActive.Should().BeTrue();

        await api.RevokeApiKeyAsync(rotated.KeyId, new RevokeReportApiKeyRequestDto { TenantId = TenantId });
        var afterRevoke = await api.GetApiKeysAsync(TenantId);
        afterRevoke.Single(key => key.KeyId == rotated.KeyId).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Permissions_GrantListRevoke_AndAuditRecordsTheChange()
    {
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });

        var granted = await api.GrantPermissionAsync(new GrantReportPermissionRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            SubjectKind = ReportAclSubjectKindDto.User,
            SubjectId = "user-1",
            Effect = ReportAclEffectDto.Allow,
            Permissions = ReportPermissionsDto.View | ReportPermissionsDto.Render,
        });
        granted.SubjectId.Should().Be("user-1");

        var entries = await api.GetFolderPermissionsAsync(TenantId, folder.FolderId);
        entries.Should().ContainSingle(entry => entry.SubjectId == "user-1");

        var audit = await api.QueryAuditAsync(TenantId, action: ReportAuditActionDto.ChangeAcl);
        audit.Should().Contain(auditEvent => auditEvent.ResourceId == folder.FolderId && auditEvent.Action == ReportAuditActionDto.ChangeAcl);

        await api.RevokePermissionAsync(new RevokeReportPermissionRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            SubjectKind = ReportAclSubjectKindDto.User,
            SubjectId = "user-1",
        });
        var afterRevoke = await api.GetFolderPermissionsAsync(TenantId, folder.FolderId);
        afterRevoke.Should().NotContain(entry => entry.SubjectId == "user-1");
    }

    [Fact]
    public async Task Permissions_GrantDenyOnEfBackend_PersistsDenyEntry_AndAuditsTheChange()
    {
        await using var host = await AdminTestApp.CreateAsync(useEfSecurityStores: true);
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });

        using var raw = host.CreateApiKeyClient();
        var response = await raw.PostAsJsonAsync("/api/permissions", new GrantReportPermissionRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            SubjectKind = ReportAclSubjectKindDto.User,
            SubjectId = "user-1",
            Effect = ReportAclEffectDto.Deny,
            Permissions = ReportPermissionsDto.View,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the EF store now persists Deny entries with full fidelity");

        // The Deny entry is persisted and round-trips as a Deny, and the change is audited.
        var entries = await api.GetFolderPermissionsAsync(TenantId, folder.FolderId);
        entries.Should().ContainSingle(entry =>
            entry.SubjectId == "user-1" && entry.Effect == ReportAclEffectDto.Deny);
        (await host.CountChangeAclAuditEventsAsync(TenantId)).Should().Be(1, "a successful grant emits an 'Allowed' ChangeAcl audit event");
    }

    [Fact]
    public async Task Permissions_GrantRoleAllowOnInMemoryBackend_StillSucceeds()
    {
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });

        var granted = await api.GrantPermissionAsync(new GrantReportPermissionRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            SubjectKind = ReportAclSubjectKindDto.Role,
            SubjectId = "Author",
            Effect = ReportAclEffectDto.Allow,
            Permissions = ReportPermissionsDto.View,
        });

        granted.SubjectKind.Should().Be(ReportAclSubjectKindDto.Role);
        var entries = await api.GetFolderPermissionsAsync(TenantId, folder.FolderId);
        entries.Should().Contain(entry => entry.SubjectKind == ReportAclSubjectKindDto.Role && entry.SubjectId == "Author");
    }

    [Fact]
    public async Task Resolve_ByIdAndByPath_ReturnsReportWithCurrentRevision()
    {
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });
        var report = await api.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Sales Register",
            DefinitionJson = FixtureDefinitionJson(),
        });

        var byId = await api.ResolveReportAsync(TenantId, reportId: report.ReportId);
        byId.ReportId.Should().Be(report.ReportId);
        byId.FolderId.Should().Be(folder.FolderId);
        byId.DefinitionJson.Should().Contain("Sales Register");
        byId.RevisionNumber.Should().BeGreaterThan(0);
        byId.RenderPath.Should().Be("api/render");

        var byPath = await api.ResolveReportAsync(TenantId, path: $"{folder.Path.Trim('/')}/Sales Register");
        byPath.ReportId.Should().Be(report.ReportId);
    }

    [Fact]
    public async Task Resolve_ByFolderQualifiedIdPath_ResolvesReportCreatedViaApi()
    {
        // A report created via POST /reports gets a GENERATED id that differs from its name; the
        // explorer/favorite deep links (BuildDeepLink) put that id in the last path segment. Resolution
        // by path must therefore accept the report id (not just the name) within the folder.
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });
        var report = await api.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Sales Register",
            DefinitionJson = FixtureDefinitionJson(),
        });

        // Precondition for the regression: the generated id is not the same as the name.
        report.ReportId.Should().NotBe("Sales Register");

        var byIdPath = await api.ResolveReportAsync(TenantId, path: $"{folder.Path.Trim('/')}/{report.ReportId}");
        byIdPath.ReportId.Should().Be(report.ReportId);
        byIdPath.FolderId.Should().Be(folder.FolderId);
    }

    [Fact]
    public async Task Resolve_BySingleSegmentDeepLink_ResolvesRootStyleReport()
    {
        // A report at the ROOT folder gets a folderless deep link (BuildDeepLink emits /reports/{reportId},
        // no folder segment). ResolveByPathAsync must handle a single-segment path by resolving the report
        // id-or-name tenant-wide, so root-folder deep links round-trip instead of 404-ing.
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });
        var report = await api.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Sales Register",
            DefinitionJson = FixtureDefinitionJson(),
        });

        var byId = await api.ResolveReportAsync(TenantId, path: report.ReportId);
        byId.ReportId.Should().Be(report.ReportId);

        var byName = await api.ResolveReportAsync(TenantId, path: "Sales Register");
        byName.ReportId.Should().Be(report.ReportId);
    }

    [Fact]
    public async Task GetParameters_ForBlankReportCreatedViaCanonicalDefinition_Returns200()
    {
        // Regression: a BLANK report created by the portal must round-trip server-side. The portal builds
        // the blank definition with the CANONICAL serializer (ReportDefinitionJsonSerializer), so the
        // server can deserialize it in GET /reports/{id}/parameters. A plain System.Text.Json blob would
        // 500 here (e.g. ReportPageSize.unit could not be converted).
        await using var host = await AdminTestApp.CreateAsync();
        var api = new TempoReportServerClient(host.CreateApiKeyClient());
        var folder = await api.CreateFolderAsync(new CreateReportFolderRequestDto { TenantId = TenantId, Name = "Finance" });
        var blankDefinitionJson = ReportDefinitionJsonSerializer.Serialize(new ReportDefinition { Name = "Blank Ledger" });
        var report = await api.CreateReportAsync(new CreateReportRequestDto
        {
            TenantId = TenantId,
            FolderId = folder.FolderId,
            Name = "Blank Ledger",
            DefinitionJson = blankDefinitionJson,
        });

        // Throws on a non-success status (the 500 this test guards against); an empty list is the expected
        // 200 body for a parameterless blank report.
        var parameters = await api.GetParametersAsync(report.ReportId, TenantId);
        parameters.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminEndpoint_WithoutCredentials_Returns401()
    {
        await using var host = await AdminTestApp.CreateAsync();
        using var anonymous = host.CreateAnonymousClient();

        var response = await anonymous.GetAsync($"/api/apikeys?tenantId={TenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithoutManagePermissionsScope_Returns403()
    {
        await using var host = await AdminTestApp.CreateAsync();
        var viewerKey = await host.CreateKeyAsync(ReportPermission.View | ReportPermission.Render);
        using var client = host.CreateClientWithKey(viewerKey);

        var response = await client.GetAsync($"/api/apikeys?tenantId={TenantId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoint_ForDifferentTenant_Returns403()
    {
        await using var host = await AdminTestApp.CreateAsync();
        using var client = host.CreateApiKeyClient();

        var response = await client.GetAsync("/api/apikeys?tenantId=tenant-b");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string FixtureDefinitionJson()
        => ReportDefinitionJsonSerializer.Serialize(new ReportDefinition
        {
            Id = "sales-register",
            Name = "Sales Register",
            Bands = new ReportBandCollection
            {
                ReportHeader = new ReportBand
                {
                    Kind = ReportBandKind.ReportHeader,
                    Height = 60,
                    Elements =
                    [
                        new ReportTextBoxElement
                        {
                            Id = "title",
                            X = 24,
                            Y = 24,
                            Width = 320,
                            Height = 24,
                            Text = "Sales Register",
                        },
                    ],
                },
            },
        });

    private sealed class AdminTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly SqliteConnection _connection;
        private readonly string _apiKey;

        private AdminTestApp(WebApplication app, SqliteConnection connection, string apiKey)
        {
            _app = app;
            _connection = connection;
            _apiKey = apiKey;
        }

        public static async Task<AdminTestApp> CreateAsync(bool useEfSecurityStores = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync().ConfigureAwait(false);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddTempoReportServerApi(options => options.UseSqlite(connection));
            if (useEfSecurityStores)
            {
                builder.Services.UseEfReportServerSecurityStores();
            }

            builder.Services.AddReportServerAuthentication(builder.Configuration);

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseTempoReportServerTenantContext();
            app.MapTempoReportServerApi()
                .RequireAuthorization(ReportServerAuthenticationDefaults.ApiPolicy);

            await app.Services.EnsureTempoReportServerDatabaseAsync().ConfigureAwait(false);
            await app.StartAsync().ConfigureAwait(false);

            var keyStore = app.Services.GetRequiredService<IReportApiKeyStore>();
            var created = await keyStore.CreateAsync(TenantId, "integration-tests", ReportPermission.All).ConfigureAwait(false);

            return new AdminTestApp(app, connection, created.PlainTextKey);
        }

        public async Task<string> CreateKeyAsync(ReportPermission permissions)
        {
            var keyStore = _app.Services.GetRequiredService<IReportApiKeyStore>();
            var created = await keyStore.CreateAsync(TenantId, "scoped-app", permissions).ConfigureAwait(false);
            return created.PlainTextKey;
        }

        public async Task<int> CountChangeAclAuditEventsAsync(string tenantId)
        {
            // Read the audit table directly: the EF audit query endpoint orders by DateTimeOffset,
            // which SQLite cannot translate (that path is covered by the MsSql suite). A plain count
            // avoids the ORDER BY while still proving no ChangeAcl event was persisted.
            using var scope = _app.Services.CreateScope();
            var requestContext = scope.ServiceProvider.GetRequiredService<ReportServerRequestContext>();
            requestContext.Set(new Reporting.Abstractions.ReportExecutionContext(tenantId, "audit-probe", "en-US"));
            var dbContext = scope.ServiceProvider.GetRequiredService<Storage.ReportServerDbContext>();
            return await dbContext.AuditEvents
                .CountAsync(auditEvent => auditEvent.Action == (int)ReportAuditAction.ChangeAcl)
                .ConfigureAwait(false);
        }

        public HttpClient CreateApiKeyClient() => CreateClientWithKey(_apiKey);

        public HttpClient CreateClientWithKey(string apiKey)
        {
            var client = _app.GetTestClient();
            client.DefaultRequestHeaders.Add(ReportSecurityHeaders.ApiKey, apiKey);
            return client;
        }

        public HttpClient CreateAnonymousClient() => _app.GetTestClient();

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
