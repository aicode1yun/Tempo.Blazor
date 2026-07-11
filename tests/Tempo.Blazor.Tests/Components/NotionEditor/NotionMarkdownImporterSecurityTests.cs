using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// InlineMarkdownToHtml ukládá HTML přímo do block contentu, který se renderuje jako raw markup.
/// Veškerý ne-markup text tedy musí být HTML-encodovaný a URL schémata whitelistovaná.
/// </summary>
public class NotionMarkdownImporterSecurityTests
{
    private static readonly Guid PageId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe>")]
    [InlineData("<div onclick=\"steal()\">text</div>")]
    public void InlineMarkdownToHtml_EncodesRawHtml(string payload)
    {
        var html = NotionMarkdownImporter.InlineMarkdownToHtml(payload);

        // None of these payloads contain markdown, so nothing may survive as markup.
        // Event handlers may remain as inert text once the angle brackets are encoded.
        html.Should().NotContain("<");
        html.Should().Contain("&lt;");
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JaVaScRiPt:alert(1))")]
    [InlineData("[click](vbscript:msgbox)")]
    [InlineData("[click](data:text/html;base64,PHN2Zz4=)")]
    public void InlineMarkdownToHtml_DropsUnsafeLinkSchemes(string markdown)
    {
        var html = NotionMarkdownImporter.InlineMarkdownToHtml(markdown);

        html.Should().NotContain("href");
        html.Should().NotContainEquivalentOf("javascript:");
        html.Should().NotContainEquivalentOf("vbscript:");
        html.Should().NotContainEquivalentOf("data:text/html");
        html.Should().Contain("click");
    }

    [Theory]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("![x](vbscript:msgbox)")]
    public void InlineMarkdownToHtml_DropsUnsafeImageSchemes(string markdown)
    {
        var html = NotionMarkdownImporter.InlineMarkdownToHtml(markdown);

        html.Should().NotContain("<img");
        html.Should().NotContainEquivalentOf("javascript:");
    }

    [Theory]
    [InlineData("[ok](https://example.test/a?b=1)", "https://example.test/a?b=1")]
    [InlineData("[ok](http://example.test)", "http://example.test")]
    [InlineData("[ok](mailto:a@example.test)", "mailto:a@example.test")]
    [InlineData("[ok](/relative/page)", "/relative/page")]
    public void InlineMarkdownToHtml_KeepsSafeLinkSchemes(string markdown, string expectedHref)
    {
        var html = NotionMarkdownImporter.InlineMarkdownToHtml(markdown);

        html.Should().Contain($"href=\"{expectedHref}\"");
        html.Should().Contain(">ok</a>");
    }

    [Fact]
    public void InlineMarkdownToHtml_KeepsMarkdownEmphasis()
    {
        var html = NotionMarkdownImporter.InlineMarkdownToHtml("**bold** *italic* ~~gone~~ `code` ==hi==");

        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("<em>italic</em>");
        html.Should().Contain("<s>gone</s>");
        html.Should().Contain("<code>code</code>");
        html.Should().Contain("<mark>hi</mark>");
    }

    [Fact]
    public void InlineMarkdownToHtml_DoesNotInterpretMarkupInsideCodeSpan()
    {
        var html = NotionMarkdownImporter.InlineMarkdownToHtml("`<b>raw</b>`");

        html.Should().Contain("<code>&lt;b&gt;raw&lt;/b&gt;</code>");
    }

    [Fact]
    public void Import_BlockImageWithUnsafeSchemeDoesNotBecomeImageBlock()
    {
        var blocks = NotionMarkdownImporter.Import("![x](javascript:alert(1))", PageId);

        blocks.Should().NotContain(block => block.Type == BlockType.Image);
    }

    [Fact]
    public void Import_QuoteKeepsLineBreaksAsMarkup()
    {
        var blocks = NotionMarkdownImporter.Import("> first\n> second", PageId);

        var quote = blocks.Should().ContainSingle(block => block.Type == BlockType.Quote).Subject;
        var html = ((ITextBlockContent)quote.Content).Html;
        html.Should().Contain("<br>");
        html.Should().NotContain("&lt;br&gt;");
    }
}
