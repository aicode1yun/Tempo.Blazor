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

    /// <summary>
    /// R.5 cutover: the component now DEFAULTS to the core engine. The flip is reversible (this
    /// default parameter value); legacy is still selectable via an explicit RenderEngine.
    /// </summary>
    [Fact]
    public void Default_RenderEngine_IsCoreEnginePreview_AfterR5Cutover()
    {
        Assert.Equal(DocumentEditorRenderEngine.CoreEnginePreview, new TmDocumentEditor().RenderEngine);
    }
}
