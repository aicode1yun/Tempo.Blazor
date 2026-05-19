using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class DocumentEditorToolbarModeTests : LocalizationTestBase
{
    [Fact]
    public void Toolbar_DefaultModeIsRibbon()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>();

        var toolbar = cut.Find("[data-testid='document-toolbar']");
        toolbar.GetAttribute("data-toolbar-mode").Should().Be(nameof(DocumentToolbarMode.Ribbon));
        toolbar.ClassList.Should().NotContain("tm-document-editor__ribbon--compact");
    }

    [Fact]
    public void Toolbar_CompactModeAddsCompactClassAndKeepsIconButtonsAccessible()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ToolbarMode, DocumentToolbarMode.Compact));

        var toolbar = cut.Find("[data-testid='document-toolbar']");
        toolbar.GetAttribute("data-toolbar-mode").Should().Be(nameof(DocumentToolbarMode.Compact));
        toolbar.ClassList.Should().Contain("tm-document-editor__ribbon--compact");
        cut.Find("[data-testid='document-bold']").GetAttribute("aria-label").Should().Be("Bold");
    }

    [Fact]
    public void Toolbar_DistractionFreeModeHidesRibbonShell()
    {
        var cut = RenderComponent<TmDocumentEditorToolbar>(parameters => parameters
            .Add(p => p.ToolbarMode, DocumentToolbarMode.DistractionFree));

        var toolbar = cut.Find("[data-testid='document-toolbar']");
        toolbar.GetAttribute("data-toolbar-mode").Should().Be(nameof(DocumentToolbarMode.DistractionFree));
        toolbar.HasAttribute("hidden").Should().BeTrue();
    }

    [Fact]
    public void Editor_PassesToolbarModeToToolbar()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.ToolbarMode, DocumentToolbarMode.Compact));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='document-toolbar']").GetAttribute("data-toolbar-mode")
                .Should().Be(nameof(DocumentToolbarMode.Compact)));
    }
}
