using FluentAssertions;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorCssTests
{
    [Fact]
    public void CssFiles_AreSplitImportedAndBundledByProject()
    {
        var root = FindRepositoryRoot();
        var cssRoot = Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "wwwroot", "css");
        var mainCss = Path.Combine(cssRoot, "components", "_document-editor.css");
        var toolbarCss = Path.Combine(cssRoot, "components", "_document-editor-toolbar.css");
        var commentsCss = Path.Combine(cssRoot, "components", "_document-editor-comments.css");
        var entryCss = Path.Combine(cssRoot, "tempo-blazor-document-editor.css");
        var coreEntryCss = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "tempo-blazor.css");
        var projectFile = Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "Tempo.Blazor.DocumentEditor.csproj");

        File.Exists(mainCss).Should().BeTrue();
        File.Exists(toolbarCss).Should().BeTrue();
        File.Exists(commentsCss).Should().BeTrue();

        var imports = File.ReadAllText(entryCss);
        imports.Should().Contain("@import \"components/_document-editor.css\";");
        imports.Should().Contain("@import \"components/_document-editor-toolbar.css\";");
        imports.Should().Contain("@import \"components/_document-editor-comments.css\";");
        imports.Should().Contain("@import \"components/_document-editor-find.css\";");

        var coreImports = File.ReadAllText(coreEntryCss);
        coreImports.Should().NotContain("@import \"components/_document-editor.css\";");
        coreImports.Should().NotContain("@import \"components/_document-editor-toolbar.css\";");
        coreImports.Should().NotContain("@import \"components/_document-editor-comments.css\";");

        var project = File.ReadAllText(projectFile);
        project.Should().Contain("<PackageId>Tempo.Blazor.DocumentEditor</PackageId>");
        project.Should().Contain("AngleSharp");
    }

    [Fact]
    public void CssFiles_CoverEditorStatesResponsivenessAndDarkMode()
    {
        var root = FindRepositoryRoot();
        var cssRoot = Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "wwwroot", "css", "components");
        var componentRoot = Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "Components", "DocumentEditor");
        var css = string.Concat(
            File.ReadAllText(Path.Combine(cssRoot, "_document-editor.css")),
            File.ReadAllText(Path.Combine(cssRoot, "_document-editor-toolbar.css")),
            File.ReadAllText(Path.Combine(cssRoot, "_document-editor-comments.css")),
            File.ReadAllText(Path.Combine(componentRoot, "TmDocumentCanvasEngineHost.razor.css")));

        css.Should().Contain(".tm-document-editor__loading");
        css.Should().Contain(".tm-document-editor__empty");
        css.Should().Contain(".tm-document-editor__error");
        css.Should().Contain(".tm-document-editor--readonly");
        css.Should().Contain(".tm-document-editor__dirty");
        css.Should().Contain(".tm-document-editor__save-message");
        css.Should().Contain(".tm-document-canvas-engine-host");
        css.Should().Contain(".tm-document-canvas-engine-host__page");
        css.Should().Contain(".tm-document-canvas-selection-rect");
        css.Should().Contain(".tm-document-inline--comment-anchor");
        css.Should().Contain(".tm-document-comment-thread--selected");
        css.Should().Contain(".tm-document-version-panel__preview");

        css.Should().Contain("@media (max-width: 64rem)");
        css.Should().Contain("@media (max-width: 40rem)");
        css.Should().Contain("inline-size: 4.75rem");
        css.Should().Contain("inline-size: 2.5rem");
        css.Should().Contain("max-height: 70vh");
        css.Should().Contain("content: attr(aria-label)");
        css.Should().Contain(".tm-document-revision-panel__item");
        css.Should().Contain(".tm-document-side-panel[data-panel-layout=\"docked-tabs\"]");
        css.Should().Contain(".tm-document-revision-panel__action--accept");
        css.Should().Contain(".tm-document-revision-panel__action--reject");
        css.Should().Contain(".tm-document-revision-panel__batch-action--accept");
        css.Should().Contain(".tm-document-revision-panel__batch-action--reject");
        css.Should().Contain(".tm-document-revision-panel__action:disabled");
        css.Should().Contain(".tm-document-revision-panel__batch-action:disabled");
        css.Should().Contain(".tm-document-editor__ribbon-tab--active::after");
        css.Should().Contain(".tm-document-editor__track-toggle--on");
        css.Should().Contain(".tm-document-editor__track-toggle--off");
        css.Should().Contain(".tm-document-editor__track-toggle::before");
        css.Should().Contain(".tm-document-editor__track-toggle::after");
        css.Should().Contain("box-shadow: 0 0.75rem 2rem");
        css.Should().Contain(".tm-document-canvas-object-selection");
        css.Should().Contain(".tm-document-canvas-object-resize-handle");
        css.Should().Contain(".tm-document-canvas-revision-overlay__marker");

        css.Should().Contain("[data-theme=\"dark\"] .tm-document-editor__page-surface");
        css.Should().Contain("[data-theme=\"dark\"] .tm-document-editor__ribbon");
        css.Should().Contain("[data-theme=\"dark\"] .tm-document-comment-thread");
        css.Should().Contain("[data-theme=\"dark\"] .tm-document-diff__pane pre");
        css.Should().Contain("outline-color: var(--tm-color-primary)");
    }

    [Fact]
    public void CssFiles_UseStrictLayeredImageLayoutWithoutWysiwygFloatFallback()
    {
        var root = FindRepositoryRoot();
        var css = string.Concat(
            File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "wwwroot", "css", "components", "_document-editor.css")),
            File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "Components", "DocumentEditor", "TmDocumentCanvasEngineHost.razor.css")));

        css.Should().Contain(".tm-document-canvas-engine-host__canvas");
        css.Should().Contain(".tm-document-canvas-selection-rect");
        css.Should().Contain(".tm-document-canvas-object-selection");
        css.Should().Contain(".tm-document-canvas-object-resize-handle");
        css.Should().Contain(".tm-document-canvas-table-resize-preview");
        css.Should().Contain("@media (max-width: 40rem)");
        css.Should().NotContain(".tm-wysiwyg-image--wrap-square");
        css.Should().NotContain(".tm-wysiwyg-image-sidecar-text");
        css.Should().NotContain("data-wrap-sidecar-for");
    }

    [Fact]
    public void CssFiles_ContainCanvasRestrictedEditingMarkers()
    {
        var root = FindRepositoryRoot();
        var css = string.Concat(
            File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "wwwroot", "css", "components", "_document-editor.css")),
            File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor.DocumentEditor", "Components", "DocumentEditor", "TmDocumentCanvasEngineHost.razor.css")));

        css.Should().Contain(".tm-document-editor__ribbon-button--active");
        css.Should().Contain(".tm-document-canvas-engine-host--readonly");
        css.Should().Contain(".tm-document-canvas-engine-host__input");
        css.Should().Contain("cursor: not-allowed;");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
