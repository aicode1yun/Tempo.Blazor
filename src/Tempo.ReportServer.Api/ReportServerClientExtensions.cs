using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Reporting.Abstractions.Dtos;

namespace Tempo.ReportServer.Api;

/// <summary>
/// Registration helpers for the typed <see cref="ITempoReportServerClient"/> against a remote
/// Tempo Report Server API host. The API base address always comes from configuration
/// (<c>ReportServer:BaseUrl</c>) — the same key is set on both the server and WASM sides — and is
/// never hardcoded or relative, because the API is a different origin by definition (ADR-0002).
/// </summary>
public static class ReportServerClientExtensions
{
    /// <summary>Configuration key holding the report server API base URL.</summary>
    public const string BaseUrlConfigurationKey = "ReportServer:BaseUrl";

    /// <summary>
    /// Registers <see cref="ITempoReportServerClient"/> as a typed <see cref="HttpClient"/> whose base
    /// address is read from <see cref="BaseUrlConfigurationKey"/>. Consumers inject the typed client;
    /// they never touch <see cref="HttpClient"/> directly.
    /// </summary>
    public static IServiceCollection AddTempoReportServerClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl = configuration[BaseUrlConfigurationKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                $"Configuration key '{BaseUrlConfigurationKey}' is required to reach the Tempo Report Server API host.");
        }

        return services.AddTempoReportServerClient(new Uri(baseUrl, UriKind.Absolute));
    }

    /// <summary>Registers <see cref="ITempoReportServerClient"/> as a typed client with an explicit base address.</summary>
    public static IServiceCollection AddTempoReportServerClient(
        this IServiceCollection services,
        Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        services.AddHttpClient<ITempoReportServerClient, TempoReportServerClient>(client =>
            client.BaseAddress = baseAddress);
        return services;
    }
}
