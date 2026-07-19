using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tempo.ReportServer.Api.Scheduling;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Host;

/// <summary>
/// Executable host entry point for the Tempo Report Server API.
/// </summary>
/// <remarks>
/// Declared as an explicit class in a dedicated namespace (not top-level statements) so the
/// generated <c>Program</c> type does not collide with the <c>Program</c> emitted by the
/// <c>Tempo.ReportServer.Web</c> host that references this assembly.
/// </remarks>
public sealed class Program
{
    private Program()
    {
    }

    /// <summary>Builds, configures and runs the report server API host.</summary>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddTempoReportServerApi(ConfigureDatabase(builder.Configuration));

        // Render concurrency, timeout, output-size and page quotas are deployment-tunable.
        builder.Services.Configure<Storage.ReportServerQuotaOptions>(builder.Configuration.GetSection("Rendering"));

        builder.Services.AddReportServerAuthentication(builder.Configuration, builder.Environment);

        // Fáze 6: the scheduling worker (background service), delivery channels and persistent
        // schedule store live in the API/worker tier.
        builder.Services.AddTempoReportServerScheduling(builder.Configuration);

        // Decision O1 / ADR-0001: persist API keys and audit events in the report server database.
        // In-memory stores remain the default so lightweight hosts and tests keep working.
        if (string.Equals(builder.Configuration["Security:Persistence"], "Ef", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.UseEfReportServerSecurityStores();
        }
        builder.Services.AddOpenApi();
        AddCors(builder);

        var app = builder.Build();

        await app.Services.EnsureTempoReportServerDatabaseAsync().ConfigureAwait(false);

        // Idempotent minimal-data seed (opt-in via Database:Seed:Enabled). Runs after the schema is
        // applied so a fresh deployment has a root folder (and optional owner grant) ready.
        var seedOptions = builder.Configuration.GetSection("Database:Seed").Get<Storage.ReportServerSeedOptions>()
            ?? new Storage.ReportServerSeedOptions();
        await Storage.ReportServerSeeder.SeedFromServicesAsync(app.Services, seedOptions).ConfigureAwait(false);

        var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"];
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            app.UseCors(CorsPolicyName);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseTempoReportServerTenantContext();

        app.MapOpenApi();

        // The catalog/render group requires an authenticated principal from any accepted scheme.
        // The anonymous /health and /version endpoints are mapped on the root inside this call
        // and are deliberately excluded from the authorization requirement.
        app.MapTempoReportServerApi()
            .RequireAuthorization(ReportServerAuthenticationDefaults.ApiPolicy);

        await app.RunAsync().ConfigureAwait(false);
    }

    private const string CorsPolicyName = "ReportServerFrontend";

    private static Action<DbContextOptionsBuilder>? ConfigureDatabase(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReportServer");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        // Decision O1 / ADR-0001: SQL Server is the production catalog store. SQLite remains a
        // zero-setup development fallback selectable through "Database:Provider".
        var provider = configuration["Database:Provider"];
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return options => options.UseSqlite(connectionString);
        }

        return options => options.UseSqlServer(
            connectionString,
            sql => sql.MigrationsAssembly(typeof(Storage.ReportServerDbContext).Assembly.GetName().Name));
    }

    private static void AddCors(WebApplicationBuilder builder)
    {
        var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"];
        if (string.IsNullOrWhiteSpace(frontendOrigin))
        {
            return;
        }

        // Per ADR-0002: exact FE origin, no AllowCredentials (bearer auth needs none).
        builder.Services.AddCors(options => options.AddPolicy(
            CorsPolicyName,
            policy => policy
                .WithOrigins(frontendOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()));
    }
}
