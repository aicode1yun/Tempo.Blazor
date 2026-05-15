using System.Text.Json;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// SS-SER-01..03: SpreadsheetBlockContent JSON round-trip přes polymorfní IBlockContent.
/// </summary>
public class SpreadsheetBlockContentSerializationTests
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ── SS-SER-01: Serializace obsahuje discriminator "spreadsheet" ────────

    [Fact]
    public void Serialize_ContainsTypeDiscriminator()
    {
        IBlockContent content = new SpreadsheetBlockContent
        {
            SpreadsheetDocumentId = Guid.NewGuid(),
            Width = 800,
            Height = 400,
            Caption = "Test"
        };

        var json = JsonSerializer.Serialize(content, _opts);

        json.Should().Contain("\"$type\":\"spreadsheet\"");
    }

    // ── SS-SER-02: Deserializace vrátí SpreadsheetBlockContent ───────────

    [Fact]
    public void Deserialize_ReturnsSpreadsheetBlockContent()
    {
        var id = Guid.NewGuid();
        var json = $$"""{"$type":"spreadsheet","spreadsheetDocumentId":"{{id}}","width":640,"height":300,"caption":"My sheet"}""";

        var result = JsonSerializer.Deserialize<IBlockContent>(json, _opts);

        result.Should().BeOfType<SpreadsheetBlockContent>();
        var sc = (SpreadsheetBlockContent)result!;
        sc.SpreadsheetDocumentId.Should().Be(id);
        sc.Width.Should().Be(640);
        sc.Height.Should().Be(300);
        sc.Caption.Should().Be("My sheet");
    }

    // ── SS-SER-03: Round-trip zachovává všechny hodnoty ──────────────────

    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        var id = Guid.NewGuid();
        IBlockContent original = new SpreadsheetBlockContent
        {
            SpreadsheetDocumentId = id,
            Width = 1024,
            Height = 500,
            Caption = "Round-trip"
        };

        var json = JsonSerializer.Serialize(original, _opts);
        var restored = JsonSerializer.Deserialize<IBlockContent>(json, _opts) as SpreadsheetBlockContent;

        restored.Should().NotBeNull();
        restored!.SpreadsheetDocumentId.Should().Be(id);
        restored.Width.Should().Be(1024);
        restored.Height.Should().Be(500);
        restored.Caption.Should().Be("Round-trip");
    }
}
