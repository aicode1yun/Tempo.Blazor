using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Reporting.Abstractions.Tests.Definitions;

public sealed class ReportDrillThroughEvaluatorTests
{
    [Fact]
    public void Resolve_MapsStaticFieldAndParameterSources_ToTargetParameters()
    {
        var action = new ReportDrillThroughAction
        {
            TargetReportPath = "Finance/Customer Detail",
            ParameterMappings =
            [
                new ReportDrillThroughParameterMapping("Category", ReportDrillThroughSourceKind.Static, "Closed"),
                new ReportDrillThroughParameterMapping("Customer", ReportDrillThroughSourceKind.Field, "Customer"),
                new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Parameter, "Region"),
            ],
        };

        var context = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Customer"] = "Europe Customer 01",
        };
        var currentParameters = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal)
        {
            ["Region"] = ReportParameterValue.Scalar("EU"),
        };

        var resolution = ReportDrillThroughEvaluator.Resolve(action, context, currentParameters);

        resolution.HasTarget.Should().BeTrue();
        resolution.TargetReportPath.Should().Be("Finance/Customer Detail");
        resolution.Parameters.Should().HaveCount(3);
        resolution.Parameters["Category"].Should().Be("Closed");
        resolution.Parameters["Customer"].Should().Be("Europe Customer 01");
        resolution.Parameters["Region"].Should().Be("EU");
    }

    [Fact]
    public void Resolve_MissingFieldAndParameter_ProduceNullValues()
    {
        var action = new ReportDrillThroughAction
        {
            TargetReportId = "customer-detail",
            ParameterMappings =
            [
                new ReportDrillThroughParameterMapping("Customer", ReportDrillThroughSourceKind.Field, "Missing"),
                new ReportDrillThroughParameterMapping("Region", ReportDrillThroughSourceKind.Parameter, "Absent"),
            ],
        };

        var resolution = ReportDrillThroughEvaluator.Resolve(action);

        resolution.TargetReportId.Should().Be("customer-detail");
        resolution.Parameters["Customer"].Should().BeNull();
        resolution.Parameters["Region"].Should().BeNull();
    }

    [Fact]
    public void Resolve_SkipsMappingsWithoutTargetParameterName()
    {
        var action = new ReportDrillThroughAction
        {
            TargetReportPath = "Finance/Detail",
            ParameterMappings =
            [
                new ReportDrillThroughParameterMapping(" ", ReportDrillThroughSourceKind.Static, "X"),
            ],
        };

        var resolution = ReportDrillThroughEvaluator.Resolve(action);

        resolution.Parameters.Should().BeEmpty();
    }
}
