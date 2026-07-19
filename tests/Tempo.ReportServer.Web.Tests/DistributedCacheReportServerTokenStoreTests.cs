using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Options;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

/// <summary>
/// Round-trip specification for <see cref="DistributedCacheReportServerTokenStore"/>, the scale-out
/// (shared cache) backing of the server-side token store.
/// </summary>
public sealed class DistributedCacheReportServerTokenStoreTests
{
    private static IDistributedCache NewCache()
        => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public void SetThenGet_ReturnsStoredTokens()
    {
        var store = new DistributedCacheReportServerTokenStore(NewCache());
        var tokens = new ReportServerTokenSet("access-1", "refresh-1", DateTimeOffset.UtcNow.AddMinutes(5));

        store.Set("subject-1", tokens);
        var round = store.Get("subject-1");

        round.Should().NotBeNull();
        round!.AccessToken.Should().Be("access-1");
        round.RefreshToken.Should().Be("refresh-1");
        round.ExpiresUtc.Should().BeCloseTo(tokens.ExpiresUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Get_UnknownSubject_ReturnsNull()
    {
        var store = new DistributedCacheReportServerTokenStore(NewCache());

        store.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Remove_DeletesTokens()
    {
        var cache = NewCache();
        var store = new DistributedCacheReportServerTokenStore(cache);
        store.Set("subject-1", new ReportServerTokenSet("a", "r", DateTimeOffset.UtcNow.AddMinutes(5)));

        store.Remove("subject-1");

        store.Get("subject-1").Should().BeNull();
    }

    [Fact]
    public void TwoStoresOverSharedCache_SeeSameTokens()
    {
        // Simulates two host instances sharing one distributed cache.
        var cache = NewCache();
        var instanceA = new DistributedCacheReportServerTokenStore(cache);
        var instanceB = new DistributedCacheReportServerTokenStore(cache);

        instanceA.Set("subject-1", new ReportServerTokenSet("access-shared", "refresh-shared", DateTimeOffset.UtcNow.AddMinutes(5)));

        instanceB.Get("subject-1")!.AccessToken.Should().Be("access-shared");
    }

    [Fact]
    public void TwoStoresOverSharedCache_RemoveOnA_IsSeenByB()
    {
        // A sign-out on one instance must be visible to every other instance sharing the cache.
        var cache = NewCache();
        var instanceA = new DistributedCacheReportServerTokenStore(cache);
        var instanceB = new DistributedCacheReportServerTokenStore(cache);
        instanceA.Set("subject-1", new ReportServerTokenSet("access-shared", "refresh-shared", DateTimeOffset.UtcNow.AddMinutes(5)));
        instanceB.Get("subject-1").Should().NotBeNull();

        instanceA.Remove("subject-1");

        instanceB.Get("subject-1").Should().BeNull("a remove on one instance is seen by the others");
    }

    /// <summary>
    /// Cross-instance sharing through a real SQL-Server-backed <see cref="IDistributedCache"/>
    /// (<c>AddDistributedSqlServerCache</c>), the production scale-out backing. Mirrors the MSSQL
    /// integration approach used elsewhere in the report server: it creates its own database and the
    /// cache table (as <c>dotnet sql-cache create</c> would), then proves that a token saved through one
    /// <see cref="SqlServerCache"/> instance is read back — and its removal seen — through a second,
    /// independent instance over the same table. Skipped (returns) when no SQL Server is reachable.
    /// </summary>
    [Fact]
    public async Task TwoStoresOverSqlServerCache_ShareTokens_AcrossInstances()
    {
        const string database = "TempoReportServerWebTests";
        var masterConnection = Environment.GetEnvironmentVariable("REPORTSERVER_TEST_CONNECTION") is { Length: > 0 } fromEnv
            ? fromEnv
            : $"Server=localhost\\SQLEXPRESS;Database=master;Integrated Security=true;TrustServerCertificate=true;";

        var (ready, cacheConnectionString) = await TryPrepareSqlCacheTableAsync(masterConnection, database);
        if (!ready)
        {
            // No SQL Server available in this environment — gate out (the in-memory tests above still
            // prove the sharing contract deterministically).
            return;
        }

        static IDistributedCache NewSqlCache(string connectionString)
            => new SqlServerCache(Options.Create(new SqlServerCacheOptions
            {
                ConnectionString = connectionString,
                SchemaName = "dbo",
                TableName = "TokenCache",
            }));

        var instanceA = new DistributedCacheReportServerTokenStore(NewSqlCache(cacheConnectionString));
        var instanceB = new DistributedCacheReportServerTokenStore(NewSqlCache(cacheConnectionString));
        var subject = $"subject-{Guid.NewGuid():N}";

        instanceA.Set(subject, new ReportServerTokenSet("sql-access", "sql-refresh", DateTimeOffset.UtcNow.AddMinutes(5)));

        var seenByB = instanceB.Get(subject);
        seenByB.Should().NotBeNull("a second instance shares the SQL Server cache table");
        seenByB!.AccessToken.Should().Be("sql-access");
        seenByB.RefreshToken.Should().Be("sql-refresh");

        instanceA.Remove(subject);
        instanceB.Get(subject).Should().BeNull("a remove on instance A is seen by instance B through SQL Server");
    }

    private static async Task<(bool Ready, string CacheConnectionString)> TryPrepareSqlCacheTableAsync(string masterConnectionString, string database)
    {
        var cacheConnectionString = new SqlConnectionStringBuilder(masterConnectionString) { InitialCatalog = database }.ConnectionString;
        try
        {
            await using (var master = new SqlConnection(masterConnectionString))
            {
                await master.OpenAsync();
                await using var createDb = master.CreateCommand();
                createDb.CommandText = $"IF DB_ID('{database}') IS NULL CREATE DATABASE [{database}];";
                await createDb.ExecuteNonQueryAsync();
            }

            await using var db = new SqlConnection(cacheConnectionString);
            await db.OpenAsync();
            await using var createTable = db.CreateCommand();
            // The schema `dotnet sql-cache create` produces for a Microsoft.Extensions.Caching.SqlServer table.
            createTable.CommandText = """
                IF OBJECT_ID('dbo.TokenCache', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[TokenCache](
                        [Id] [nvarchar](449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
                        [Value] [varbinary](max) NOT NULL,
                        [ExpiresAtTime] [datetimeoffset](7) NOT NULL,
                        [SlidingExpirationInSeconds] [bigint] NULL,
                        [AbsoluteExpiration] [datetimeoffset](7) NULL,
                        CONSTRAINT [pk_TokenCache_Id] PRIMARY KEY CLUSTERED ([Id] ASC));
                    CREATE NONCLUSTERED INDEX [Index_TokenCache_ExpiresAtTime] ON [dbo].[TokenCache]([ExpiresAtTime] ASC);
                END
                """;
            await createTable.ExecuteNonQueryAsync();
            return (true, cacheConnectionString);
        }
        catch (SqlException)
        {
            return (false, cacheConnectionString);
        }
    }
}
