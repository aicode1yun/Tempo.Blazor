using Bunit;
using Microsoft.AspNetCore.Components;
using Tempo.ReportServer.Web.Components;
using Tempo.ReportServer.Web.Tests.Fixtures;

namespace Tempo.ReportServer.Web.Tests;

/// <summary>
/// Guards the render-mode marker the report server layout renders. E2E render-mode assertions
/// (functional-server / functional-wasm) key on <c>#render-mode-marker</c>'s <c>data-mode</c> and the
/// lowercase <c>data-interactive</c> flag, so both must stay stable.
/// </summary>
public sealed class RenderModeMarkerTests : ReportServerWebTestBase
{
    [Theory]
    [InlineData("Server", true, "true")]
    [InlineData("WebAssembly", true, "true")]
    [InlineData("Static", false, "false")]
    public void Marker_ExposesRendererNameAndLowercaseInteractiveFlag(string mode, bool interactive, string expectedInteractive)
    {
        SetRendererInfo(new RendererInfo(mode, interactive));

        var cut = Render<RenderModeMarker>();

        var marker = cut.Find("#render-mode-marker");
        marker.GetAttribute("data-testid").Should().Be("render-mode-marker");
        marker.GetAttribute("data-mode").Should().Be(mode);
        marker.GetAttribute("data-interactive").Should().Be(expectedInteractive);
    }
}
