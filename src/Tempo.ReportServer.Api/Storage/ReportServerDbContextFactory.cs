using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// Design-time factory used by the EF Core tooling (<c>dotnet ef migrations</c>) to build a
/// <see cref="ReportServerDbContext"/> against the SQL Server provider (decision O1 / ADR-0001).
/// The catalog migrations are authored for SQL Server; SQLite development/test databases are
/// created with <c>EnsureCreated</c> and do not use these migrations.
/// </summary>
public sealed class ReportServerDbContextFactory : IDesignTimeDbContextFactory<ReportServerDbContext>
{
    /// <summary>Design-time connection string; overridden by the <c>REPORTSERVER_DESIGN_CONNECTION</c> environment variable.</summary>
    public const string DefaultDesignConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=TempoReportServerDesign;Integrated Security=true;TrustServerCertificate=true;";

    /// <inheritdoc />
    public ReportServerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("REPORTSERVER_DESIGN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultDesignConnectionString;
        }

        var options = new DbContextOptionsBuilder<ReportServerDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ReportServerDbContext).Assembly.GetName().Name))
            .Options;

        return new ReportServerDbContext(options, new ReportServerRequestContext());
    }
}
