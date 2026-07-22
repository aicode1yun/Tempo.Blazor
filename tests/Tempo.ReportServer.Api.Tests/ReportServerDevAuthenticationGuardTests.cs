using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Tests;

/// <summary>
/// Defense-in-depth guard for the development authentication bypass: it must never activate in a
/// Production host even if <c>Authentication:Dev:Enabled=true</c> is set (one stray env var otherwise
/// disables real auth), while remaining available in non-production for the Keycloak-free E2E lane.
/// </summary>
public sealed class ReportServerDevAuthenticationGuardTests
{
    [Fact]
    public async Task DevScheme_IsSuppressed_InProduction_EvenWhenEnabled()
    {
        var schemes = await RegisteredSchemesAsync(devEnabled: true, environmentName: Environments.Production);

        schemes.Should().NotContain(ReportServerAuthenticationDefaults.DevScheme);
        // Real authentication stays intact.
        schemes.Should().Contain(JwtBearerDefaults.AuthenticationScheme);
        schemes.Should().Contain(ReportServerAuthenticationDefaults.ApiKeyScheme);
    }

    [Fact]
    public async Task DevScheme_IsRegistered_InDevelopment_WhenEnabled()
    {
        var schemes = await RegisteredSchemesAsync(devEnabled: true, environmentName: Environments.Development);

        schemes.Should().Contain(ReportServerAuthenticationDefaults.DevScheme);
    }

    [Fact]
    public async Task DevScheme_IsAbsent_WhenDisabled()
    {
        var schemes = await RegisteredSchemesAsync(devEnabled: false, environmentName: Environments.Development);

        schemes.Should().NotContain(ReportServerAuthenticationDefaults.DevScheme);
    }

    private static async Task<string[]> RegisteredSchemesAsync(bool devEnabled, string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Dev:Enabled"] = devEnabled ? "true" : "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReportServerAuthentication(configuration, new FakeHostEnvironment(environmentName));

        await using var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        return schemes.Select(scheme => scheme.Name).ToArray();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Tempo.ReportServer.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
