using System.Text.RegularExpressions;
using FluentAssertions;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorLayoutPhase17LegacyRemovalTests
{
    [Fact]
    public void Phase17_WysiwygRuntimeAndCssDoNotContainLegacySidecarImplementation()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "js", "document-editor-wysiwyg.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "components", "_document-editor.css"));
        var bundledCss = File.ReadAllText(Path.Combine(root, "src", "Tempo.Blazor", "wwwroot", "css", "tempo-blazor.bundled.css"));

        script.Should().NotContain("_createWrappedImageSideTextBlockModel");
        script.Should().NotContain("_isWrappedImageSideTextBlock");
        script.Should().NotContain("_isTextBlockForWrappedImage");
        script.Should().NotContain("_isWrappedImageSideTextLayout");
        script.Should().NotContain("_ensureWrappedImageSideTextBlock");
        script.Should().NotContain("_findWrappedImageSideTextBlockAtPoint");
        script.Should().NotContain("_focusWrappedImageSideTextBlock");
        script.Should().NotContain("data-wrap-sidecar-for");
        script.Should().NotContain("tm-wysiwyg-image-sidecar-text");

        css.Should().NotContain(".tm-wysiwyg-image-sidecar-text");
        css.Should().NotContain("data-wrap-sidecar-for");
        Regex.IsMatch(css, @"\.tm-wysiwyg-image--wrap-(square|tight|through)[^{]*\{[^}]*float\s*:", RegexOptions.Singleline)
            .Should().BeFalse();
        Regex.IsMatch(css, @"\.tm-wysiwyg-image--wrap-(square|tight|through)[^{]*\{[^}]*shape-outside\s*:", RegexOptions.Singleline)
            .Should().BeFalse();

        bundledCss.Should().NotContain(".tm-wysiwyg-image-sidecar-text");
        bundledCss.Should().NotContain("data-wrap-sidecar-for");
        Regex.IsMatch(bundledCss, @"\.tm-wysiwyg-image--wrap-(square|tight|through)[^{]*\{[^}]*float\s*:", RegexOptions.Singleline)
            .Should().BeFalse();
        Regex.IsMatch(bundledCss, @"\.tm-wysiwyg-image--wrap-(square|tight|through)[^{]*\{[^}]*shape-outside\s*:", RegexOptions.Singleline)
            .Should().BeFalse();
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
