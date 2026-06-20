using FluentAssertions;

namespace Tempo.Blazor.Tests.Planning;

public class DocumentEditorDeveloperGuideTests
{
    [Theory]
    [InlineData("## Feature Registry")]
    [InlineData("## Command Registry")]
    [InlineData("## Toolbar Modes")]
    [InlineData("## Clipboard Pipeline Extension Point")]
    [InlineData("## Image Provider UX")]
    [InlineData("## Table Properties Model")]
    [InlineData("## Autosave And Pending Actions")]
    [InlineData("## Watchdog Recovery")]
    [InlineData("## Accessibility Expectations")]
    public void DeveloperGuide_ContainsRequiredDocumentEditorSections(string heading)
    {
        ReadDeveloperGuide().Should().Contain(heading);
    }

    [Fact]
    public void DeveloperGuide_ReferencesPhase22DemoScenarios()
    {
        var guide = ReadDeveloperGuide();

        guide.Should().Contain("/document-editor");
        guide.Should().Contain("toolbar mode switch");
        guide.Should().Contain("feature toggles");
        guide.Should().Contain("image provider");
        guide.Should().Contain("table properties");
        guide.Should().Contain("comments and review");
        guide.Should().Contain("paste report");
        guide.Should().Contain("autosave error");
    }

    private static string ReadDeveloperGuide()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docs", "document-editor-developer-guide.md");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not locate docs/document-editor-developer-guide.md from test output directory.");
    }
}
