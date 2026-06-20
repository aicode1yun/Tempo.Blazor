using FluentAssertions;
using Tempo.Blazor.Abstractions.Wireframe.Export;
using Tempo.Blazor.Demo.Api.Services;
using Xunit;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Tests for <see cref="WireframeExportService"/> helpers and basic export flow.
/// </summary>
public class WireframeExportServiceTests
{
    static WireframeExportServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }
    private static WireframeExportService CreateService() => new();

    // ── SVG helpers ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""<svg width="800" height="600"></svg>""", 800, 600)]
    [InlineData("""<svg width="1200.5" height="900.25"></svg>""", 1200.5, 900.25)]
    [InlineData("""<svg></svg>""", 800, 600)]
    public void ExtractSvgDimensions_ParsesCorrectly(string svg, double expectedW, double expectedH)
    {
        var service = CreateService();
        // Use reflection to test internal helpers
        var type = typeof(WireframeExportService);
        var extractW = type.GetMethod("ExtractSvgWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var extractH = type.GetMethod("ExtractSvgHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var w = (double)extractW!.Invoke(null, [svg])!;
        var h = (double)extractH!.Invoke(null, [svg])!;

        w.Should().Be(expectedW);
        h.Should().Be(expectedH);
    }

    [Fact]
    public void EnsureSvgNamespace_AddsNamespace_WhenMissing()
    {
        var type = typeof(WireframeExportService);
        var method = type.GetMethod("EnsureSvgNamespace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string)method!.Invoke(null, ["<svg width=\"100\" height=\"100\"></svg>"])!;
        result.Should().Contain("xmlns=\"http://www.w3.org/2000/svg\"");
    }

    [Fact]
    public void EnsureSvgNamespace_KeepsExistingNamespace()
    {
        var type = typeof(WireframeExportService);
        var method = type.GetMethod("EnsureSvgNamespace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"></svg>""";
        var result = (string)method!.Invoke(null, [svg])!;
        result.Should().Be(svg);
    }

    [Fact]
    public void EnsureSvgNamespace_ReturnsDefault_ForEmptyInput()
    {
        var type = typeof(WireframeExportService);
        var method = type.GetMethod("EnsureSvgNamespace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string)method!.Invoke(null, [""])!;
        result.Should().Contain("<svg");
        result.Should().Contain("xmlns");
    }

    // ── Color parsing ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("#ffffff", 255, 255, 255)]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("#00ff00", 0, 255, 0)]
    [InlineData("#0000ff", 0, 0, 255)]
    public void ParseColor_ValidHex_ReturnsColor(string hex, byte r, byte g, byte b)
    {
        var type = typeof(WireframeExportService);
        var method = type.GetMethod("ParseColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var color = method!.Invoke(null, [hex]);

        color.Should().NotBeNull();
        // SKColor is a struct; use reflection to read Red/Green/Blue properties
        var colorType = color!.GetType();
        colorType.GetProperty("Red")!.GetValue(color).Should().Be(r);
        colorType.GetProperty("Green")!.GetValue(color).Should().Be(g);
        colorType.GetProperty("Blue")!.GetValue(color).Should().Be(b);
    }

    [Fact]
    public void ParseColor_NullOrEmpty_ReturnsNull()
    {
        var type = typeof(WireframeExportService);
        var method = type.GetMethod("ParseColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method!.Invoke(null, [null]).Should().BeNull();
        method!.Invoke(null, [""]).Should().BeNull();
        method!.Invoke(null, ["   "]).Should().BeNull();
    }

    // ── PNG export (smoke test with simple SVG) ───────────────────────────────

    [Fact]
    public async Task ExportPngAsync_WithSimpleSvg_ReturnsNonEmptyBytes()
    {
        var service = CreateService();
        var request = new WireframeExportRequest
        {
            Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="red"/></svg>""",
            FileName = "test",
            Options = new WireframeExportOptions { IncludeBackground = true, Scale = 1 }
        };

        var png = await service.ExportPngAsync(request);
        png.Should().NotBeNull();
        png.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportPngAsync_WithScale2_ReturnsLargerImage()
    {
        var service = CreateService();
        var request1x = new WireframeExportRequest
        {
            Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"></svg>""",
            Options = new WireframeExportOptions { Scale = 1 }
        };
        var request2x = new WireframeExportRequest
        {
            Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"></svg>""",
            Options = new WireframeExportOptions { Scale = 2 }
        };

        var png1 = await service.ExportPngAsync(request1x);
        var png2 = await service.ExportPngAsync(request2x);

        png2.Length.Should().BeGreaterThan(png1.Length);
    }

    // ── PDF export (smoke test) ───────────────────────────────────────────────

    [Fact]
    public async Task ExportPdfAsync_WithSimpleSvg_ReturnsNonEmptyBytes()
    {
        var service = CreateService();
        var request = new WireframeExportRequest
        {
            Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="200" height="150"><rect width="200" height="150" fill="blue"/></svg>""",
            FileName = "test",
            Options = new WireframeExportOptions { IncludeBackground = true }
        };

        var pdf = await service.ExportPdfAsync(request);
        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(0);
        // PDF magic bytes
        pdf[0].Should().Be(0x25); // '%'
        pdf[1].Should().Be(0x50); // 'P'
    }
}
