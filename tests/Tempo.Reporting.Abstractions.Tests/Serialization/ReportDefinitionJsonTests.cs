using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Serialization;
using Tempo.Reporting.Abstractions.Tests.Definitions;

namespace Tempo.Reporting.Abstractions.Tests.Serialization;

public sealed class ReportDefinitionJsonTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsWithByteParityAndStableDiscriminators()
    {
        var definition = ReportDefinitionModelTests.CreateValidDefinition();

        var json = ReportDefinitionJsonSerializer.Serialize(definition);
        var restored = ReportDefinitionJsonSerializer.Deserialize(json);
        var jsonAgain = ReportDefinitionJsonSerializer.Serialize(restored);

        json.Should().StartWith("{\"schemaVersion\":1,\"id\":\"monthly-orders\",\"name\":\"Monthly orders\"");
        json.Should().Contain("\"orientation\":\"portrait\"");
        json.Should().Contain("\"type\":\"textBox\"");
        json.Should().Contain("\"type\":\"subReport\"");
        jsonAgain.Should().Be(json);
    }

    [Fact]
    public void MigrateToCurrentJson_V1ToV1NoOp_ReturnsCanonicalJson()
    {
        var json = ReportDefinitionJsonSerializer.Serialize(ReportDefinitionModelTests.CreateValidDefinition());

        var migrated = ReportDefinitionJsonSerializer.MigrateToCurrentJson(
            json,
            ReportDefinitionMigrationRegistry.Empty);

        migrated.Should().Be(json);
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedSchemaVersion()
    {
        const string json = """
        {"schemaVersion":999,"name":"Future report","pageSetup":{"pageSize":{"width":1,"height":1,"unit":"point"},"orientation":"portrait","margins":{"left":0,"top":0,"right":0,"bottom":0}},"parameters":[],"dataSets":[],"styles":[],"bands":{}}
        """;

        var act = () => ReportDefinitionJsonSerializer.Deserialize(json);

        act.Should().Throw<ReportDefinitionJsonException>()
            .WithMessage("*schema version*999*");
    }
}
