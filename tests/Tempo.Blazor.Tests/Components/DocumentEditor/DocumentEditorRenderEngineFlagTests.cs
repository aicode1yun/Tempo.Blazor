using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// R.4.8 cutover feature-flag guard. The core engine is feature-complete + browser-verified
/// standalone (R.4.0–R.4.7) but not yet wired into the hosted component's C# interop, so the
/// flag must fail safe: requesting the preview falls back to legacy until interop is ready.
/// </summary>
public class DocumentEditorRenderEngineFlagTests
{
    [Fact]
    public void Legacy_AlwaysResolvesToLegacy()
    {
        Assert.Equal(DocumentEditorRenderEngine.Legacy,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.Legacy, hostedInteropReady: false));
        Assert.Equal(DocumentEditorRenderEngine.Legacy,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.Legacy, hostedInteropReady: true));
    }

    [Fact]
    public void CoreEnginePreview_FallsBackToLegacy_UntilHostedInteropReady()
    {
        Assert.Equal(DocumentEditorRenderEngine.Legacy,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.CoreEnginePreview, hostedInteropReady: false));
    }

    [Fact]
    public void CoreEnginePreview_RunsOnlyWhenHostedInteropReady()
    {
        Assert.Equal(DocumentEditorRenderEngine.CoreEnginePreview,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.CoreEnginePreview, hostedInteropReady: true));
    }

    [Fact]
    public void CanvasEnginePreview_RunsWhenExplicitlyRequested()
    {
        Assert.Equal(DocumentEditorRenderEngine.CanvasEnginePreview,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.CanvasEnginePreview, hostedInteropReady: false));
        Assert.Equal(DocumentEditorRenderEngine.CanvasEnginePreview,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.CanvasEnginePreview, hostedInteropReady: true));
    }

    /// <summary>
    /// Phase 25 cutover: the component now defaults to the canvas engine. The flip is
    /// reversible through the RenderEngine parameter; legacy is still selectable explicitly.
    /// </summary>
    [Fact]
    public void Default_RenderEngine_IsCanvasEnginePreview_AfterPhase25Cutover()
    {
        Assert.Equal(DocumentEditorRenderEngine.CanvasEnginePreview, new TmDocumentEditor().RenderEngine);
    }

    [Fact]
    public void Explicit_Legacy_RemainsAvailableAsRollback()
    {
        Assert.Equal(DocumentEditorRenderEngine.Legacy,
            DocumentEditorRenderEngineFlag.Resolve(DocumentEditorRenderEngine.Legacy, hostedInteropReady: true));
    }
}
