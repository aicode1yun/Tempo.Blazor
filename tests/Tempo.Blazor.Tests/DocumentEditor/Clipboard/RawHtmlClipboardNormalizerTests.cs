using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

public sealed class RawHtmlClipboardNormalizerTests
{
    private static RawHtmlClipboardNormalizer Create() => new();

    // ─── CanHandle ────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_WithHtml_ReturnsTrue()
    {
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = "<p>text</p>" }));
    }

    [Fact]
    public void CanHandle_WithoutHtml_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput { PlainText = "text only" }));
    }

    // ─── Paragraph ────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_SingleParagraph_CreatesParagraphBlock()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p>Hello world</p>" });

        var block = Assert.Single(output.Blocks);
        Assert.Equal(DocumentBlockType.Paragraph, block.Type);
        var para = Assert.IsType<ParagraphBlockContent>(block.Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Equal("Hello world", run.Text);
    }

    [Fact]
    public void Normalize_MultipleParagraphs_CreatesMultipleBlocks()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p>First</p><p>Second</p>" });

        Assert.Equal(2, output.Blocks.Count);
        Assert.All(output.Blocks, b => Assert.Equal(DocumentBlockType.Paragraph, b.Type));
    }

    // ─── Headings ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("<h1>Title</h1>", 1)]
    [InlineData("<h2>Sub</h2>", 2)]
    [InlineData("<h3>Sub-sub</h3>", 3)]
    [InlineData("<h6>Deep</h6>", 6)]
    public void Normalize_Heading_CreatesHeadingBlockWithLevel(string html, int expectedLevel)
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var block = Assert.Single(output.Blocks);
        Assert.Equal(DocumentBlockType.Heading, block.Type);
        var heading = Assert.IsType<HeadingBlockContent>(block.Content);
        Assert.Equal(expectedLevel, heading.Level);
    }

    // ─── Lists ────────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_UnorderedList_CreatesListBlocks()
    {
        var output = Create().Normalize(new DocumentClipboardInput
        {
            Html = "<ul><li>Item A</li><li>Item B</li></ul>"
        });

        Assert.Equal(2, output.Blocks.Count);
        Assert.All(output.Blocks, b =>
        {
            Assert.Equal(DocumentBlockType.List, b.Type);
            var list = Assert.IsType<ListBlockContent>(b.Content);
            Assert.False(list.Ordered);
        });
    }

    [Fact]
    public void Normalize_OrderedList_CreatesOrderedListBlocks()
    {
        var output = Create().Normalize(new DocumentClipboardInput
        {
            Html = "<ol><li>Step 1</li><li>Step 2</li></ol>"
        });

        Assert.Equal(2, output.Blocks.Count);
        Assert.All(output.Blocks, b =>
        {
            var list = Assert.IsType<ListBlockContent>(b.Content);
            Assert.True(list.Ordered);
        });
    }

    // ─── Table ────────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_SimpleTable_CreatesTableBlock()
    {
        const string html = "<table><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></table>";
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var block = Assert.Single(output.Blocks);
        Assert.Equal(DocumentBlockType.Table, block.Type);
        var table = Assert.IsType<TableBlockContent>(block.Content);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Rows[0].Cells.Count);
        Assert.Equal(2, table.Rows[1].Cells.Count);
    }

    // ─── Inline marks ────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_Bold_AddsBoldMark()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p><strong>Bold text</strong></p>" });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void Normalize_BoldWithB_AddsBoldMark()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p><b>Bold</b></p>" });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void Normalize_Italic_AddsItalicMark()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p><em>Italic</em></p>" });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Italic);
    }

    [Fact]
    public void Normalize_Underline_AddsUnderlineMark()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p><u>Underlined</u></p>" });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Underline);
    }

    [Fact]
    public void Normalize_Link_AddsLinkMarkWithHref()
    {
        var output = Create().Normalize(new DocumentClipboardInput
        {
            Html = "<p><a href=\"https://example.com\">Link text</a></p>"
        });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        var linkMark = run.Marks.FirstOrDefault(m => m.Type == InlineMarkType.Link);
        Assert.NotNull(linkMark);
        Assert.Equal("https://example.com", linkMark.Link?.Href);
    }

    // ─── Sanitization ─────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_ScriptElement_IsStripped()
    {
        var output = Create().Normalize(new DocumentClipboardInput
        {
            Html = "<p>Safe</p><script>alert('xss')</script>"
        });

        var block = Assert.Single(output.Blocks);
        var para = Assert.IsType<ParagraphBlockContent>(block.Content);
        Assert.Equal("Safe", Assert.IsType<TextRun>(para.Inlines[0]).Text);
        Assert.Contains(output.Warnings, w => w.Code == "stripped-element");
    }

    [Fact]
    public void Normalize_UnsafeLink_RemovesLinkMarkAndAddsWarning()
    {
        var output = Create().Normalize(new DocumentClipboardInput
        {
            Html = """<p><a href="javascript:alert(1)">Unsafe</a></p>"""
        });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines[0]);
        Assert.Empty(run.Marks);
        Assert.Contains(output.Warnings, w => w.Code == "unsafe-link-removed");
    }

    [Fact]
    public void Normalize_DivWithText_CreatesParagraphBlock()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<div>Div text</div>" });

        Assert.Single(output.Blocks);
        Assert.Equal(DocumentBlockType.Paragraph, output.Blocks[0].Type);
    }

    [Fact]
    public void Normalize_AllowedInlineStyleBold_PreservesText()
    {
        var output = Create().Normalize(new DocumentClipboardInput
        {
            Html = "<p>Normal <strong>bold</strong> end</p>"
        });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        Assert.Equal(3, para.Inlines.Count);
        Assert.Equal("Normal ", Assert.IsType<TextRun>(para.Inlines[0]).Text);
        Assert.Equal("bold", Assert.IsType<TextRun>(para.Inlines[1]).Text);
        Assert.Equal(" end", Assert.IsType<TextRun>(para.Inlines[2]).Text);
    }

    [Fact]
    public void Normalize_EmptyParagraph_IsSkipped()
    {
        var output = Create().Normalize(new DocumentClipboardInput { Html = "<p></p><p>Content</p><p>  </p>" });

        Assert.Single(output.Blocks);
        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        Assert.Equal("Content", Assert.IsType<TextRun>(para.Inlines[0]).Text);
    }
}
