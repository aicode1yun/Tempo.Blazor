using Tempo.Reporting.Abstractions;

namespace Tempo.Reporting.Abstractions.Tests;

public sealed class ReportExecutionContextTests
{
    [Fact]
    public void Constructor_PreservesTenantUserCultureAndClaims()
    {
        var context = new ReportExecutionContext(
            TenantId: "tenant-01",
            UserId: "user-42",
            CultureName: "cs-CZ",
            Claims: new Dictionary<string, string> { ["role"] = "designer" });

        context.TenantId.Should().Be("tenant-01");
        context.UserId.Should().Be("user-42");
        context.CultureName.Should().Be("cs-CZ");
        context.Claims.Should().ContainKey("role").WhoseValue.Should().Be("designer");
    }
}
