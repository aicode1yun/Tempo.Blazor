#pragma warning disable MA0016, MA0048

using System.Data.Common;
using Tempo.Reporting.Abstractions;

namespace Tempo.ReportServer.Api.DataSources;

/// <summary>Named server-side data source kind.</summary>
public enum ReportDataSourceKind
{
    /// <summary>REST JSON data source.</summary>
    RestJson,

    /// <summary>SQL data source.</summary>
    Sql,
}

/// <summary>Named data source registered outside report definitions.</summary>
public sealed record NamedReportDataSource
{
    private NamedReportDataSource(
        string tenantId,
        string name,
        ReportDataSourceKind kind,
        RestJsonDataSourceOptions? rest,
        SqlDataSourceOptions? sql)
    {
        TenantId = tenantId;
        Name = name;
        Kind = kind;
        Rest = rest;
        SqlOptions = sql;
    }

    /// <summary>Tenant that owns the source.</summary>
    public string TenantId { get; }

    /// <summary>Source name referenced by report definitions.</summary>
    public string Name { get; }

    /// <summary>Source kind.</summary>
    public ReportDataSourceKind Kind { get; }

    /// <summary>REST options.</summary>
    public RestJsonDataSourceOptions? Rest { get; }

    /// <summary>SQL options.</summary>
    public SqlDataSourceOptions? SqlOptions { get; }

    /// <summary>Creates a REST JSON data source.</summary>
    public static NamedReportDataSource RestJson(
        string tenantId,
        string name,
        Uri baseUri,
        IReadOnlyDictionary<string, string>? headers = null)
        => new(
            tenantId,
            name,
            ReportDataSourceKind.RestJson,
            new RestJsonDataSourceOptions(baseUri, headers ?? new Dictionary<string, string>(StringComparer.Ordinal)),
            null);

    /// <summary>Creates a SQL data source.</summary>
    public static NamedReportDataSource Sql(
        string tenantId,
        string name,
        Func<DbConnection> connectionFactory)
        => new(
            tenantId,
            name,
            ReportDataSourceKind.Sql,
            null,
            new SqlDataSourceOptions(connectionFactory));
}

/// <summary>REST JSON data source options.</summary>
public sealed record RestJsonDataSourceOptions(
    Uri BaseUri,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>SQL data source options.</summary>
public sealed record SqlDataSourceOptions(Func<DbConnection> ConnectionFactory);

/// <summary>Tenant-scoped named data source registry.</summary>
public interface IReportDataSourceRegistry
{
    /// <summary>Resolves a source by name for the tenant in the execution context.</summary>
    NamedReportDataSource? Resolve(string name, ReportExecutionContext context);
}

/// <summary>In-memory named data source registry.</summary>
public sealed class InMemoryNamedDataSourceRegistry : IReportDataSourceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<(string TenantId, string Name), NamedReportDataSource> _sources = new();

    /// <summary>Registers or replaces a named data source.</summary>
    public void Register(NamedReportDataSource source)
    {
        lock (_gate)
        {
            _sources[(source.TenantId, source.Name)] = source;
        }
    }

    /// <inheritdoc />
    public NamedReportDataSource? Resolve(string name, ReportExecutionContext context)
    {
        lock (_gate)
        {
            _sources.TryGetValue((context.TenantId, name), out var source);
            return source;
        }
    }
}

#pragma warning restore MA0016, MA0048
