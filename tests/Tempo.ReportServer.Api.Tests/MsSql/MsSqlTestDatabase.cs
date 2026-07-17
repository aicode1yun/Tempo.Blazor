using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Tempo.ReportServer.Api;
using Tempo.ReportServer.Api.Storage;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.Tests.MsSql;

/// <summary>
/// Shared real SQL Server test database for the report server catalog.
/// The database is created/migrated once through the authored EF Core migrations and reset
/// between tests with Respawn (the EF migrations-history table is preserved).
/// </summary>
public sealed class MsSqlTestDatabase : IAsyncLifetime
{
    /// <summary>Environment variable that overrides the SQL Server test connection string.</summary>
    public const string ConnectionEnvironmentVariable = "REPORTSERVER_TEST_CONNECTION";

    private const string DefaultConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=TempoReportServerTests;Integrated Security=true;TrustServerCertificate=true;";

    private Respawner? _respawner;

    /// <summary>The connection string for the SQL Server catalog test database.</summary>
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable) is { Length: > 0 } fromEnv
            ? fromEnv
            : DefaultConnectionString;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        // Create the database (if needed) and apply the catalog migrations.
        await using (var context = CreateDbContext("default"))
        {
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
            DbAdapter = DbAdapter.SqlServer,
        }).ConfigureAwait(false);
    }

    /// <summary>Resets all catalog tables to empty, keeping the schema and migration history.</summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await _respawner.ResetAsync(connection).ConfigureAwait(false);
    }

    /// <summary>Creates a fresh EF context whose ambient tenant is <paramref name="tenantId"/>.</summary>
    public ReportServerDbContext CreateDbContext(string tenantId)
    {
        var requestContext = new ReportServerRequestContext();
        requestContext.Set(new ReportExecutionContext(tenantId, "test-user", "en-US"));
        var options = new DbContextOptionsBuilder<ReportServerDbContext>()
            .UseSqlServer(
                ConnectionString,
                sql => sql.MigrationsAssembly(typeof(ReportServerDbContext).Assembly.GetName().Name))
            .Options;
        return new ReportServerDbContext(options, requestContext);
    }

    /// <summary>Creates an <see cref="EfReportServerStore"/> whose ambient tenant is <paramref name="tenantId"/>.</summary>
    public (ReportServerDbContext Context, EfReportServerStore Store) CreateStore(string tenantId)
    {
        var context = CreateDbContext(tenantId);
        return (context, new EfReportServerStore(context));
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>xUnit collection that shares a single migrated SQL Server test database.</summary>
[CollectionDefinition(Name)]
public sealed class MsSqlTestCollection : ICollectionFixture<MsSqlTestDatabase>
{
    /// <summary>Collection name.</summary>
    public const string Name = "mssql-report-catalog";
}
