using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.CanvasEngine;

/// <summary>
/// bUnit coverage for the canvas page-image export bridge (plan S1.7): the host pulls one image per
/// page from the engine via interop, and the editor delegates to the host (or fails loudly when the
/// canvas engine is not the active, mounted engine).
/// </summary>
public sealed class CanvasEnginePageImageExportTests : LocalizationTestBase
{
    private const string InteropModulePath = "./_content/Tempo.Blazor/js/document-editor-canvas/interop.mjs";

    [Fact]
    public async Task CanvasEngineHost_ExportPageImagesAsync_ExportsEveryPageWithDataUrl()
    {
        SetupCanvasModule();
        var document = DocumentEditorDocument.Empty("canvas-host-export");

        var cut = RenderComponent<TmDocumentCanvasEngineHost>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.AriaLabel, "Document editor")
            .Add(p => p.InputAriaLabel, "Document editor"));
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        var images = await cut.InvokeAsync(() => cut.Instance.ExportPageImagesAsync());

        images.Should().HaveCount(2);
        images.Select(image => image.PageIndex).Should().ContainInOrder(0, 1);
        images[0].DataUrl.Should().Be("data:image/png;base64,PAGE0");
        images[1].DataUrl.Should().Be("data:image/png;base64,PAGE1");
        images.Should().OnlyContain(image => image.Scale == 2);
        images.Should().OnlyContain(image => image.Width > 0 && image.Height > 0);
    }

    [Fact]
    public async Task CanvasEngineHost_ExportPageImagesAsync_PassesScaleAndFormatToInterop()
    {
        var module = SetupCanvasModule();
        var document = DocumentEditorDocument.Empty("canvas-host-export-options");

        var cut = RenderComponent<TmDocumentCanvasEngineHost>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.AriaLabel, "Document editor")
            .Add(p => p.InputAriaLabel, "Document editor"));
        cut.WaitForAssertion(() => cut.Instance.IsReady.Should().BeTrue());

        await cut.InvokeAsync(() => cut.Instance.ExportPageImagesAsync(new DocumentPageImageExportOptions
        {
            Scale = 3,
            Format = "jpeg",
            Quality = 0.8
        }));

        var exportCall = module.Invocations.First(invocation => invocation.Identifier == "exportPageImage");
        var optionsJson = exportCall.Arguments[2]?.ToString() ?? string.Empty;
        optionsJson.Should().Contain("\"scale\":3");
        optionsJson.Should().Contain("jpeg");
    }

    [Fact]
    public async Task TmDocumentEditor_ExportPageImagesAsync_DelegatesToCanvasHost()
    {
        SetupCanvasModule();
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("canvas-editor-export");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "canvas-editor-export")
            .Add(p => p.Provider, provider)
            .Add(p => p.RenderEngine, DocumentEditorRenderEngine.CanvasEnginePreview));
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());

        var images = await cut.InvokeAsync(() => cut.Instance.ExportPageImagesAsync());

        images.Should().HaveCount(2);
        images[0].DataUrl.Should().Be("data:image/png;base64,PAGE0");
    }

    [Fact]
    public async Task TmDocumentEditor_ExportPageImagesAsync_ThrowsWhenCanvasEngineIsNotActive()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("canvas-editor-export-legacy");

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "canvas-editor-export-legacy")
            .Add(p => p.Provider, provider));

        var act = async () => await cut.InvokeAsync(() => cut.Instance.ExportPageImagesAsync());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*canvas*");
    }

    private BunitJSModuleInterop SetupCanvasModule()
    {
        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<string>("mount", _ => true).SetResult("canvas-host-test-handle");
        module.Setup<bool>("isDirty", _ => true).SetResult(false);
        module.SetupVoid("markSaved", _ => true).SetVoidResult();
        module.SetupVoid("focus", _ => true).SetVoidResult();
        module.Setup<string?>("getFormattingStateJson", _ => true).SetResult("""{"bold":false,"italic":false,"underline":false,"alignment":"left"}""");
        module.Setup<string?>("getUndoStateJson", _ => true).SetResult("""{"canUndo":false,"canRedo":false}""");
        module.Setup<string?>("getSelectionStateJson", _ => true).SetResult("""{"isCollapsed":true,"pageIndex":0}""");
        module.Setup<string?>("getDiagnosticsJson", _ => true).SetResult("""{"architectureName":"CanvasDocumentEngine","pageSurfaceStrategy":"canvas-per-visible-page","pageCount":1}""");
        module.Setup<string?>("getPageMetricsJson", _ => true).SetResult("""{"totalPages":2,"renderedPages":2,"virtualizedPages":0,"activePageIndex":0,"pages":[{"pageIndex":0,"pageNumber":1},{"pageIndex":1,"pageNumber":2}]}""");
        module.Setup<string?>("exportPageImage", invocation => invocation.Arguments.Count >= 2 && Convert.ToInt32(invocation.Arguments[1]) == 0)
            .SetResult("""{"pageIndex":0,"width":794,"height":1123,"scale":2,"dataUrl":"data:image/png;base64,PAGE0"}""");
        module.Setup<string?>("exportPageImage", invocation => invocation.Arguments.Count >= 2 && Convert.ToInt32(invocation.Arguments[1]) == 1)
            .SetResult("""{"pageIndex":1,"width":794,"height":1123,"scale":2,"dataUrl":"data:image/png;base64,PAGE1"}""");
        module.SetupVoid("dispose", _ => true).SetVoidResult();
        return module;
    }
}
