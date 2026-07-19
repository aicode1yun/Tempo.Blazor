using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Blocks.Text;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// A code block whose language is Markdown can be flipped into a read-only preview
/// that renders GFM — tables included — without leaving the block or losing the source.
/// </summary>
public sealed class TmNotionCodeBlockMarkdownPreviewTests : LocalizationTestBase
{
    private const string TableMarkdown = """
        | Name | Status |
        | :--- | ---: |
        | CF26 | Ready |
        """;

    public TmNotionCodeBlockMarkdownPreviewTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmNotionCodeBlock_CaptionPlaceholder"] = "Add caption…",
            ["TmNotionCodeBlock_Copied"] = "Copied!",
            ["TmNotionCodeBlock_CopyCode"] = "Copy",
            ["TmNotionCodeBlock_SelectLanguage"] = "Language",
            ["TmNotionCodeBlock_ShowPreview"] = "Preview",
            ["TmNotionCodeBlock_ShowEditor"] = "Edit",
            ["TmNotionCodeBlock_PreviewLabel"] = "Rendered Markdown preview"
        });
    }

    [Fact]
    public void PreviewToggle_IsHiddenForNonMarkdownLanguages()
    {
        var cut = RenderCodeBlock("var x = 1;", "JavaScript");

        cut.FindAll("[data-testid='notion-code-preview-toggle']").Should().BeEmpty();
    }

    [Fact]
    public void PreviewToggle_IsShownForMarkdownLanguage()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown");

        cut.FindAll("[data-testid='notion-code-preview-toggle']").Should().ContainSingle();
    }

    [Fact]
    public void PreviewToggle_IsHiddenWhenCapabilityIsDisabled()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown", allowMarkdownPreview: false);

        cut.FindAll("[data-testid='notion-code-preview-toggle']").Should().BeEmpty();
    }

    [Fact]
    public void CodeBlock_StartsInEditorMode()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown");

        cut.FindAll("textarea.tm-notion-code-block__content").Should().ContainSingle();
        cut.FindAll("[data-testid='notion-code-preview']").Should().BeEmpty();
    }

    [Fact]
    public void TogglingPreview_RendersMarkdownTable()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown");

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        var preview = cut.Find("[data-testid='notion-code-preview']");
        preview.InnerHtml.Should().Contain("<table");
        preview.QuerySelectorAll("th").Select(cell => cell.TextContent).Should().Equal("Name", "Status");
        preview.QuerySelectorAll("td").Select(cell => cell.TextContent).Should().Equal("CF26", "Ready");
        cut.FindAll("textarea.tm-notion-code-block__content").Should().BeEmpty();
    }

    [Fact]
    public void TogglingBackToEditor_KeepsSourceMarkdown()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown");

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();
        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        cut.FindAll("[data-testid='notion-code-preview']").Should().BeEmpty();
        cut.FindAll("textarea.tm-notion-code-block__content").Should().ContainSingle();
        cut.Instance.Content!.Code.Should().Be(TableMarkdown);
    }

    [Fact]
    public void Preview_KeepsColumnAlignmentFromSeparatorRow()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown");

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        var html = cut.Find("[data-testid='notion-code-preview']").InnerHtml;
        html.Should().Contain("text-align:left");
        html.Should().Contain("text-align:right");
    }

    [Fact]
    public void Preview_RendersHeadingsAndLists()
    {
        var cut = RenderCodeBlock("# Title\n\n- one\n- two", "Markdown");

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        var html = cut.Find("[data-testid='notion-code-preview']").InnerHtml;
        html.Should().Contain("Title");
        html.Should().Contain("<ul>");
        html.Should().Contain("one");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("| broken | table\nnot a separator\n| x |")]
    [InlineData("|||")]
    [InlineData("| a |\n| --- |")]
    public void Preview_DoesNotThrowOnEmptyOrInvalidMarkdown(string markdown)
    {
        var cut = RenderCodeBlock(markdown, "Markdown");

        var toggle = () => cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        toggle.Should().NotThrow();
        cut.FindAll("[data-testid='notion-code-preview']").Should().ContainSingle();
    }

    [Fact]
    public void Preview_DoesNotEmitRawScriptFromMarkdown()
    {
        var cut = RenderCodeBlock("<script>alert(1)</script>\n\n[x](javascript:alert(1))", "Markdown");

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        var html = cut.Find("[data-testid='notion-code-preview']").InnerHtml;
        html.Should().NotContain("<script");
        html.Should().NotContainEquivalentOf("javascript:");
    }

    [Fact]
    public void PreviewToggle_ResetsWhenLanguageLeavesMarkdown()
    {
        var content = new CodeBlockContent { Code = TableMarkdown, Language = "Markdown" };
        var cut = Render<TmNotionCodeBlock>(parameters => parameters
            .Add(p => p.Content, content)
            .Add(p => p.ReadOnly, true));

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();
        cut.FindAll("[data-testid='notion-code-preview']").Should().ContainSingle();

        cut.Render(parameters => parameters
            .Add(p => p.Content, new CodeBlockContent { Code = TableMarkdown, Language = "JavaScript" }));

        cut.FindAll("[data-testid='notion-code-preview']").Should().BeEmpty();
        cut.FindAll("[data-testid='notion-code-preview-toggle']").Should().BeEmpty();
    }

    private IRenderedComponent<TmNotionCodeBlock> RenderCodeBlock(
        string code,
        string language,
        bool allowMarkdownPreview = true,
        bool readOnly = true)
    {
        var content = new CodeBlockContent { Code = code, Language = language };

        return Render<TmNotionCodeBlock>(parameters => parameters
            .Add(p => p.Content, content)
            .Add(p => p.ReadOnly, readOnly)
            .Add(p => p.AllowMarkdownPreview, allowMarkdownPreview));
    }

    // ── Language selector ──────────────────────────────────────────────────

    [Theory]
    [InlineData("yaml", "YAML")]
    [InlineData("bash", "Bash")]
    [InlineData("c#", "C#")]
    [InlineData("markdown", "Markdown")]
    public void AStoredLanguageIsMatchedAgainstTheListRegardlessOfCase(string stored, string expected)
    {
        // The dropdown binds to the option values. A stored "yaml" matches no option, so the
        // selector falls back to its first one and claims the block is plain text.
        var cut = RenderCodeBlock("x", stored, readOnly: false);

        SelectedLanguage(cut).Should().Be(expected);
    }

    [Fact]
    public void AnUnknownLanguageIsKept()
    {
        SelectedLanguage(RenderCodeBlock("x", "Brainfuck", readOnly: false)).Should().Be("Brainfuck");
    }

    [Fact]
    public void ABlockStoredAsMarkdownOffersThePreviewToggle()
    {
        RenderCodeBlock(TableMarkdown, "markdown")
            .FindAll("[data-testid='notion-code-preview-toggle']").Should().ContainSingle();
    }

    private static string SelectedLanguage(IRenderedComponent<TmNotionCodeBlock> cut) =>
        ((AngleSharp.Html.Dom.IHtmlSelectElement)cut.Find("select")).Value;

    // ── Syntax highlighting ────────────────────────────────────────────────

    [Fact]
    public void TheCodeIsHighlightedWithItsPrismGrammarId()
    {
        RenderCodeBlock("var x = 1;", "C#", readOnly: false);

        LastHighlightLanguage().Should().Be("csharp");
    }

    [Fact]
    public void AStoredLowercaseLanguageStillReachesTheHighlighter()
    {
        RenderCodeBlock("a: 1", "yaml", readOnly: false);

        LastHighlightLanguage().Should().Be("yaml");
    }

    [Fact]
    public void PlainTextIsNotHighlighted()
    {
        RenderCodeBlock("hello", "Plain Text", readOnly: false);

        HighlightLanguages().Should().NotBeEmpty();
        HighlightLanguages().Should().OnlyContain(language => language == null);
    }

    [Fact]
    public void AReadOnlyBlockIsStillHighlighted()
    {
        // Reading someone else's page is when highlighting matters most.
        RenderCodeBlock("var x = 1;", "C#");

        LastHighlightLanguage().Should().Be("csharp");
    }

    [Fact]
    public void ThePreviewDoesNotRehighlight()
    {
        var cut = RenderCodeBlock(TableMarkdown, "Markdown", readOnly: false);
        var before = HighlightLanguages().Count;

        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        // The preview replaces the textarea, so there is nothing left to paint behind it.
        HighlightLanguages().Count.Should().Be(before, "the preview must not re-trigger the highlighter");
    }

    private List<string?> HighlightLanguages() =>
        JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "tmNotionEditor.highlightToHtml")
            .Select(invocation => (string?)invocation.Arguments[1])
            .ToList();

    private string? LastHighlightLanguage() => HighlightLanguages().Should().NotBeEmpty().And.Subject.Last();

    [Theory]
    [InlineData("")]
    [InlineData("<br>")]
    [InlineData("<br/>")]
    [InlineData("  <br>  ")]
    [InlineData("\n")]
    public void AnEmptyOrBreakOnlyMarkdownSourceRendersAnEmptyPreview(string code)
    {
        var cut = RenderCodeBlock(code, "Markdown", readOnly: false);
        cut.Find("[data-testid='notion-code-preview-toggle']").Click();

        var preview = cut.Find("[data-testid='notion-code-preview']");
        preview.TextContent.Trim().Should().BeEmpty();
        preview.InnerHtml.Should().NotContain("<br", "an empty code block must not render a stray blank line");
    }
}
