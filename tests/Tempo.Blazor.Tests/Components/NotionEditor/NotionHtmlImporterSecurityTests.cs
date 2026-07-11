using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Sanitizace inline HTML a korektní párování vnořených stejnojmenných elementů.
/// </summary>
public class NotionHtmlImporterSecurityTests
{
    private static readonly Guid PageId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData("<p><a href=\"javascript:alert(1)\">click</a></p>")]
    [InlineData("<p><a href=\"JAVASCRIPT:alert(1)\">click</a></p>")]
    [InlineData("<p><a href=\"vbscript:msgbox\">click</a></p>")]
    public void Import_DropsUnsafeAnchorSchemes(string html)
    {
        var blocks = NotionHtmlImporter.Import(html, PageId);

        var text = ((ITextBlockContent)blocks.Single(block => block.Type == BlockType.Paragraph).Content).Html;
        text.Should().NotContainEquivalentOf("javascript:");
        text.Should().NotContainEquivalentOf("vbscript:");
        text.Should().Contain("click");
    }

    [Fact]
    public void Import_KeepsSafeAnchorScheme()
    {
        var blocks = NotionHtmlImporter.Import("<p><a href=\"https://example.test\">click</a></p>", PageId);

        var text = ((ITextBlockContent)blocks.Single(block => block.Type == BlockType.Paragraph).Content).Html;
        text.Should().Contain("href=\"https://example.test\"");
    }

    [Fact]
    public void Import_DropsScriptAndEventHandlers()
    {
        var blocks = NotionHtmlImporter.Import("<p>hi<script>alert(1)</script><span onclick=\"x()\">y</span></p>", PageId);

        var text = ((ITextBlockContent)blocks.Single(block => block.Type == BlockType.Paragraph).Content).Html;
        text.Should().NotContain("<script");
        text.Should().NotContain("onclick");
    }

    [Fact]
    public void Import_HrefCannotBreakOutOfTheAttribute()
    {
        var blocks = NotionHtmlImporter.Import("<p><a href='https://x/?a=><script>alert(1)</script>'>click</a></p>", PageId);

        var text = ((ITextBlockContent)blocks.Single(block => block.Type == BlockType.Paragraph).Content).Html;
        text.Should().NotContain("<script");
        text.Should().NotContain("\">");
    }

    [Fact]
    public void Import_ImageWithUnsafeSchemeIsSkipped()
    {
        var blocks = NotionHtmlImporter.Import("<img src=\"javascript:alert(1)\" alt=\"x\">", PageId);

        blocks.Should().NotContain(block => block.Type == BlockType.Image);
    }

    [Fact]
    public void Import_NestedDivsAreParsedWithCorrectPairing()
    {
        const string html = "<div><div><p>inner</p></div><p>outer</p></div>";

        var blocks = NotionHtmlImporter.Import(html, PageId);

        var paragraphs = blocks.Where(block => block.Type == BlockType.Paragraph)
            .Select(block => ((ITextBlockContent)block.Content).Html)
            .ToList();
        paragraphs.Should().Equal("inner", "outer");
    }

    [Fact]
    public void Import_NestedUnorderedListsProduceIndentLevels()
    {
        const string html = """
            <ul>
              <li>one
                <ul><li>one-a</li></ul>
              </li>
              <li>two</li>
            </ul>
            """;

        var blocks = NotionHtmlImporter.Import(html, PageId);

        var items = blocks.Where(block => block.Type == BlockType.BulletList).ToList();
        items.Should().HaveCount(3);
        Text(items[0]).Should().Contain("one");
        Indent(items[0]).Should().Be(0);
        Text(items[1]).Should().Contain("one-a");
        Indent(items[1]).Should().Be(1);
        Text(items[2]).Should().Contain("two");
        Indent(items[2]).Should().Be(0);
    }

    [Fact]
    public void Import_NestedListInsideListDoesNotSwallowFollowingItem()
    {
        const string html = "<ul><li>a<ul><li>a1</li></ul></li><li>b</li></ul>";

        var blocks = NotionHtmlImporter.Import(html, PageId);

        blocks.Where(block => block.Type == BlockType.BulletList)
            .Select(Text)
            .Should().HaveCount(3);
    }

    private static string Text(IPageBlock block) => ((ITextBlockContent)block.Content).Html;

    private static int Indent(IPageBlock block) => ((IListBlockContent)block.Content).IndentLevel;
}
