using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Files;

/// <summary>
/// Model tests for the redaction stack: area cloning, the in-memory provider with
/// clone-on-read persistence, and the culture-invariant export payload builder with
/// clamping and degenerate-rect filtering (the payload drives the destructive
/// rasterizing export in JS).
/// </summary>
public class RedactionModelTests
{
    private static RedactionArea Area(
        int page = 1, double x = 0.1, double y = 0.2, double w = 0.3, double h = 0.05,
        RedactionCategory category = RedactionCategory.PersonalId)
        => new() { Id = $"r-{page}-{x}", PageNumber = page, X = x, Y = y, Width = w, Height = h, Category = category };

    [Fact]
    public void Clone_CopiesAllFields_Independently()
    {
        var original = Area();
        original.Note = "RC";

        var clone = original.Clone();
        clone.X = 0.9;
        clone.Note = "changed";

        original.X.Should().Be(0.1);
        original.Note.Should().Be("RC");
        clone.Category.Should().Be(RedactionCategory.PersonalId);
    }

    [Fact]
    public async Task Provider_RoundTrips_AndClonesOnRead()
    {
        var provider = new InMemoryRedactionProvider();
        await provider.SaveAsync("doc-1", [Area()]);

        var loaded = await provider.LoadAsync("doc-1");
        loaded.Should().ContainSingle();
        loaded[0].X = 0.99;

        (await provider.LoadAsync("doc-1"))[0].X.Should().Be(0.1);
        (await provider.LoadAsync("unknown")).Should().BeEmpty();
    }

    [Fact]
    public void PayloadBuilder_GroupsByPage_WithInvariantDecimals()
    {
        var json = RedactionExportPayloadBuilder.Build(
        [
            Area(page: 2, x: 0.5),
            Area(page: 1, x: 0.25, y: 0.125, w: 0.5, h: 0.0625)
        ]);

        json.Should().Contain("\"pageNumber\":1");
        json.Should().Contain("\"pageNumber\":2");
        json.Should().Contain("0.25");
        json.Should().Contain("0.0625");
        json.Should().NotContain(",\"0");   // no culture-specific decimal commas
        json.IndexOf("\"pageNumber\":1", StringComparison.Ordinal)
            .Should().BeLessThan(json.IndexOf("\"pageNumber\":2", StringComparison.Ordinal));
    }

    [Fact]
    public void PayloadBuilder_ClampsOutOfRangeRects_ToThePage()
    {
        var json = RedactionExportPayloadBuilder.Build(
            [Area(x: -0.2, y: 0.9, w: 0.5, h: 0.4)]);

        // x clamps to 0, width to 0.3 (originally reaching 0.3), height to 0.1.
        json.Should().Contain("\"x\":0,");
        json.Should().Contain("\"width\":0.3");
        json.Should().Contain("\"height\":0.1");
    }

    [Fact]
    public void PayloadBuilder_DropsDegenerateRects_AndEmptyInputYieldsNoPages()
    {
        RedactionExportPayloadBuilder.Build([Area(x: 1.2, y: 0.2, w: 0.1, h: 0.1)])
            .Should().Contain("\"pages\":[]");
        RedactionExportPayloadBuilder.Build([]).Should().Contain("\"pages\":[]");
    }
}
