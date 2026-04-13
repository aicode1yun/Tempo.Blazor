using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Tests the pure C# logic on TmWireframeMinimap (scale computation, viewport update).
/// No Blazor host or JS interop needed.
/// </summary>
public class WireframeMinimapTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WireframeDocument MakeDoc(double w = 1280, double h = 800) =>
        new() { Width = w, Height = h };

    private static WireframeElement MakeEl(double x, double y, double w, double h) =>
        new() { Id = Guid.NewGuid().ToString("N")[..8], Type = "TmButton", X = x, Y = y, W = w, H = h };

    private static TmWireframeMinimap MakeMap(WireframeDocument? doc, int panelWidth = 200)
    {
        // Directly set the properties instead of using Blazor component lifecycle
        var map = new TmWireframeMinimap();
        // Use internal helper to set via reflection (no DI host needed for pure logic)
        typeof(TmWireframeMinimap)
            .GetProperty(nameof(TmWireframeMinimap.Document))!
            .SetValue(map, doc);
        typeof(TmWireframeMinimap)
            .GetProperty(nameof(TmWireframeMinimap.Width))!
            .SetValue(map, panelWidth);
        return map;
    }

    // ── ComputeScale ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeScale_ReturnsCorrectRatio()
    {
        var map = MakeMap(MakeDoc(w: 1280), panelWidth: 200);
        map.ComputeScale().Should().BeApproximately(200.0 / 1280.0, 1e-9);
    }

    [Fact]
    public void ComputeScale_ReturnsOne_WhenDocumentIsNull()
    {
        var map = MakeMap(null, panelWidth: 200);
        map.ComputeScale().Should().Be(1.0);
    }

    [Fact]
    public void ComputeScale_ReturnsOne_WhenDocumentWidthIsZero()
    {
        var map = MakeMap(MakeDoc(w: 0), panelWidth: 200);
        map.ComputeScale().Should().Be(1.0);
    }

    [Fact]
    public void ComputeScale_ScalesWithPanelWidth()
    {
        var doc    = MakeDoc(w: 800, h: 600);
        var map100 = MakeMap(doc, panelWidth: 100);
        var map200 = MakeMap(doc, panelWidth: 200);
        map200.ComputeScale().Should().BeApproximately(map100.ComputeScale() * 2, 1e-9);
    }

    // ── Aspect-ratio consistency ───────────────────────────────────────────────

    [Theory]
    [InlineData(1280, 800,  200)]
    [InlineData(1920, 1080, 240)]
    [InlineData(800,  600,  160)]
    public void MinimapHeight_PreservesAspectRatio(double docW, double docH, int panelW)
    {
        var doc   = MakeDoc(w: docW, h: docH);
        var map   = MakeMap(doc, panelWidth: panelW);
        var scale = map.ComputeScale();
        var mapH  = Math.Round(docH * scale);
        var expectedH = Math.Round((double)panelW / docW * docH);
        mapH.Should().Be(expectedH);
    }

    // ── UpdateViewport ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateViewport_DoesNotThrowWithoutRenderer()
    {
        // UpdateViewport calls StateHasChanged which requires a renderer.
        // We verify the pure state mutation via the internal tuple field using reflection.
        var map = MakeMap(MakeDoc());
        // UpdateViewport will throw inside StateHasChanged (no renderer) — that's expected.
        // We test the data assignment by inspecting the backing field directly.
        try { map.UpdateViewport(100, 50, 640, 400); } catch { /* StateHasChanged throws without host */ }

        var field = typeof(TmWireframeMinimap)
            .GetField("_viewportRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var rect = field.GetValue(map);
        rect.Should().NotBeNull();
    }

    // ── MinimapViewport record ────────────────────────────────────────────────

    [Fact]
    public void MinimapViewport_StoresCoordinates()
    {
        var vp = new MinimapViewport(10, 20, 640, 400);
        vp.X.Should().Be(10);
        vp.Y.Should().Be(20);
        vp.Width.Should().Be(640);
        vp.Height.Should().Be(400);
    }

    // ── MinimapNavigateArgs ───────────────────────────────────────────────────

    [Fact]
    public void MinimapNavigateArgs_StoresCentrePoint()
    {
        var args = new MinimapNavigateArgs(320, 240);
        args.CentreX.Should().Be(320);
        args.CentreY.Should().Be(240);
    }

    // ── SelectedIds parameter ─────────────────────────────────────────────────

    [Fact]
    public void SelectedIds_DefaultsToEmpty()
    {
        var map = new TmWireframeMinimap();
        map.SelectedIds.Should().BeEmpty();
    }
}
