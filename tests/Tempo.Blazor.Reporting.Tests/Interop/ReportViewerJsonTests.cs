using System.Text.Json;
using Tempo.Blazor.Reporting.Interop;
using Tempo.Reporting.Abstractions.Data;

namespace Tempo.Blazor.Reporting.Tests.Interop;

public sealed class ReportViewerJsonTests
{
    [Fact]
    public void Options_RoundTripReportParameterValues()
    {
        var parameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
        {
            ["Region"] = ReportParameterValue.Scalar("EU"),
            ["MinimumTotal"] = ReportParameterValue.Scalar(1000m),
            ["IncludeClosed"] = ReportParameterValue.Scalar(true),
        };

        var json = JsonSerializer.Serialize(parameters, ReportViewerJson.Options);
        var roundTrip = JsonSerializer.Deserialize<Dictionary<string, ReportParameterValue>>(
            json,
            ReportViewerJson.Options);

        roundTrip.Should().NotBeNull();
        roundTrip!["Region"].ScalarValue.Should().Be("EU");
        roundTrip["MinimumTotal"].ScalarValue.Should().Be(1000L);
        roundTrip["IncludeClosed"].ScalarValue.Should().Be(true);
    }
}
