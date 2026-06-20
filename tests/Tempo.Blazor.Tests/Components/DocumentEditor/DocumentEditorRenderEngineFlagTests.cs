using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>Canvas-only guard for the legacy render-engine compatibility flag.</summary>
#pragma warning disable CS0618
public class DocumentEditorRenderEngineFlagTests
{
    [Theory]
    [InlineData(DocumentEditorRenderEngine.Legacy)]
    [InlineData(DocumentEditorRenderEngine.CoreEnginePreview)]
    [InlineData(DocumentEditorRenderEngine.CanvasEnginePreview)]
    public void Resolve_AlwaysReturnsCanvasEngine(DocumentEditorRenderEngine requested)
    {
        Assert.Equal(DocumentEditorRenderEngine.CanvasEnginePreview,
            DocumentEditorRenderEngineFlag.Resolve(requested, hostedInteropReady: false));
        Assert.Equal(DocumentEditorRenderEngine.CanvasEnginePreview,
            DocumentEditorRenderEngineFlag.Resolve(requested, hostedInteropReady: true));
    }

    [Fact]
    public void Default_RenderEngine_RemainsCanvasEnginePreview()
    {
        Assert.Equal(DocumentEditorRenderEngine.CanvasEnginePreview, new TmDocumentEditor().RenderEngine);
    }
}
#pragma warning restore CS0618
