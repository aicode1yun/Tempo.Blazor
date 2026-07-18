using Bunit;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Czech localization of the editor UI: with the Czech localizer registered, the rendered toolbar,
/// ribbon tabs and status texts come from TmResources.cs.json — no English fallbacks and no raw
/// keys leak into the markup.
/// </summary>
public class TmDocumentEditorCzechLocalizationTests : LocalizationTestBase
{
    [Fact]
    public void Editor_WithCzechLocalizer_RendersCzechRibbonAndToolbar()
    {
        UseCzechLocalization();
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-cs");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-cs")
                      .Add(p => p.Provider, provider));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        var markup = cut.Markup;
        markup.Should().Contain("Domů", "the Home ribbon tab must render in Czech");
        markup.Should().Contain("Vložit", "the Insert ribbon tab must render in Czech");
        markup.Should().Contain("Revize", "the Review ribbon tab must render in Czech");
        markup.Should().Contain("Uložit", "the Save button must render in Czech");
        markup.Should().NotContain("TmDocumentEditor_", "no raw localization keys may leak into the markup");
    }

    [Fact]
    public void Editor_WithEnglishLocalizer_RendersEnglishRibbon()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-en");

        var cut = RenderDocumentEditor(parameters =>
            parameters.Add(p => p.DocumentId, "doc-en")
                      .Add(p => p.Provider, provider));
        cut.WaitForElement("[data-testid='document-canvas-engine-host']");

        var markup = cut.Markup;
        markup.Should().Contain("Home").And.Contain("Insert").And.Contain("Review").And.Contain("Save");
        markup.Should().NotContain("TmDocumentEditor_");
    }
}
