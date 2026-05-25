using FluentAssertions;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorCssTests
{
    [Fact]
    public void CssFiles_AreSplitImportedAndBundledByProject()
    {
        var root = FindRepositoryRoot();
        var cssRoot = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css");
        var mainCss = Path.Combine(cssRoot, "components", "_document-editor.css");
        var toolbarCss = Path.Combine(cssRoot, "components", "_document-editor-toolbar.css");
        var commentsCss = Path.Combine(cssRoot, "components", "_document-editor-comments.css");
        var entryCss = Path.Combine(cssRoot, "tempo-blazor.css");
        var bundledCss = Path.Combine(cssRoot, "tempo-blazor.bundled.css");
        var projectFile = Path.Combine(root, "src", "Tempo.Blazor", "Tempo.Blazor.csproj");

        File.Exists(mainCss).Should().BeTrue();
        File.Exists(toolbarCss).Should().BeTrue();
        File.Exists(commentsCss).Should().BeTrue();

        var imports = File.ReadAllText(entryCss);
        imports.Should().Contain("@import \"components/_document-editor.css\";");
        imports.Should().Contain("@import \"components/_document-editor-toolbar.css\";");
        imports.Should().Contain("@import \"components/_document-editor-comments.css\";");

        var project = File.ReadAllText(projectFile);
        project.Should().Contain("BundleCssFiles");
        project.Should().Contain("tempo-blazor.bundled.css");
        project.Should().Contain("CssBundleInputs");

        var bundled = File.ReadAllText(bundledCss);
        bundled.Should().Contain(".tm-document-editor__ribbon{position:sticky");
        bundled.Should().Contain(".tm-document-editor__comment-rail{display:flex");
    }

    [Fact]
    public void CssFiles_CoverEditorStatesResponsivenessAndDarkMode()
    {
        var root = FindRepositoryRoot();
        var cssRoot = Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components");
        var css = string.Concat(
            File.ReadAllText(Path.Combine(cssRoot, "_document-editor.css")),
            File.ReadAllText(Path.Combine(cssRoot, "_document-editor-toolbar.css")),
            File.ReadAllText(Path.Combine(cssRoot, "_document-editor-comments.css")));

        css.Should().Contain(".tm-document-editor__loading");
        css.Should().Contain(".tm-document-editor__empty");
        css.Should().Contain(".tm-document-editor__error");
        css.Should().Contain(".tm-document-editor--readonly");
        css.Should().Contain(".tm-document-editor__dirty");
        css.Should().Contain(".tm-document-editor__save-message");
        css.Should().Contain(".tm-document-wysiwyg-host");
        css.Should().Contain(".tm-wysiwyg-page__body");
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
        css.Should().Contain(".tm-wysiwyg-page__layer--object");
        css.Should().Contain(".tm-wysiwyg-layout-object");
        css.Should().Contain(".tm-wysiwyg-object-resize-handle--se");
        css.Should().Contain(".tm-wysiwyg--protected .tm-wysiwyg-restricted-editable");

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
        var css = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css"));

        css.Should().Contain(".tm-wysiwyg-page__layer--body-text");
        css.Should().Contain(".tm-wysiwyg-page__layer--object");
        css.Should().Contain(".tm-wysiwyg-layout-object");
        css.Should().Contain(".tm-wysiwyg-selection-box");
        css.Should().Contain(".tm-wysiwyg-object-resize-handle--nw");
        css.Should().Contain(".tm-wysiwyg-object-resize-handle--se");
        css.Should().Contain(".tm-wysiwyg-layout-bubble");
        css.Should().Contain("@media (max-width: 40rem)");
        css.Should().NotContain(".tm-wysiwyg-image--wrap-square");
        css.Should().NotContain(".tm-wysiwyg-image-sidecar-text");
        css.Should().NotContain("data-wrap-sidecar-for");
    }

    [Fact]
    public void CssFiles_ContainRestrictedEditingMarkers()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css"));

        css.Should().Contain(".tm-wysiwyg--protected .tm-wysiwyg-block");
        css.Should().Contain(".tm-wysiwyg--protected .tm-wysiwyg-restricted-editable");
        css.Should().Contain("cursor: not-allowed;");
        css.Should().Contain("cursor: text;");
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
