using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Tests.DocumentLibrary;

/// <summary>
/// Tests for <see cref="TempoDocumentKind"/> — the discriminator that ties a stored
/// document to the editor that produced it. Serialised as a camelCase string so that
/// MCP/AI consumers read meaningful values rather than integers.
/// </summary>
public class TempoDocumentKindTests
{
    private static readonly JsonSerializerOptions CamelCaseEnum = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void Enum_DefinesAllThreeKinds()
    {
        Enum.GetValues<TempoDocumentKind>().Should().BeEquivalentTo(new[]
        {
            TempoDocumentKind.Wireframe,
            TempoDocumentKind.Diagram,
            TempoDocumentKind.Spreadsheet
        });
    }

    [Theory]
    [InlineData(TempoDocumentKind.Wireframe, "\"wireframe\"")]
    [InlineData(TempoDocumentKind.Diagram, "\"diagram\"")]
    [InlineData(TempoDocumentKind.Spreadsheet, "\"spreadsheet\"")]
    public void Serialises_AsCamelCaseString(TempoDocumentKind kind, string expectedJson)
    {
        var json = JsonSerializer.Serialize(kind, CamelCaseEnum);

        json.Should().Be(expectedJson);
    }

    [Theory]
    [InlineData("\"wireframe\"", TempoDocumentKind.Wireframe)]
    [InlineData("\"diagram\"", TempoDocumentKind.Diagram)]
    [InlineData("\"spreadsheet\"", TempoDocumentKind.Spreadsheet)]
    public void Deserialises_FromCamelCaseString(string json, TempoDocumentKind expected)
    {
        var kind = JsonSerializer.Deserialize<TempoDocumentKind>(json, CamelCaseEnum);

        kind.Should().Be(expected);
    }
}
