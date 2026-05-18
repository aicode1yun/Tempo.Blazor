using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

public sealed class UrlClipboardNormalizerTests
{
    private static UrlClipboardNormalizer Create() => new();

    // ─── CanHandle ────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_HttpsUrl_NoHtml_ReturnsTrue()
    {
        Assert.True(Create().CanHandle(new DocumentClipboardInput { PlainText = "https://example.com" }));
    }

    [Fact]
    public void CanHandle_HttpUrl_NoHtml_ReturnsTrue()
    {
        Assert.True(Create().CanHandle(new DocumentClipboardInput { PlainText = "http://example.com/path?q=1" }));
    }

    [Fact]
    public void CanHandle_UrlWithHtml_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput
        {
            Html = "<p>https://example.com</p>",
            PlainText = "https://example.com"
        }));
    }

    [Fact]
    public void CanHandle_PlainText_NotUrl_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput { PlainText = "Just normal text" }));
    }

    [Fact]
    public void CanHandle_MultiLineText_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput { PlainText = "https://example.com\nhttps://other.com" }));
    }

    [Fact]
    public void CanHandle_EmptyInput_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput()));
    }

    // ─── Normalize ────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_HttpsUrl_CreatesSingleParagraphBlock()
    {
        var output = Create().Normalize(new DocumentClipboardInput { PlainText = "https://example.com" });

        Assert.Single(output.Blocks);
        Assert.Equal(DocumentBlockType.Paragraph, output.Blocks[0].Type);
    }

    [Fact]
    public void Normalize_Url_TextRunContainsUrl()
    {
        var output = Create().Normalize(new DocumentClipboardInput { PlainText = "https://example.com/path" });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines.Single());
        Assert.Equal("https://example.com/path", run.Text);
    }

    [Fact]
    public void Normalize_Url_TextRunHasLinkMark()
    {
        var output = Create().Normalize(new DocumentClipboardInput { PlainText = "https://example.com" });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines.Single());
        var linkMark = run.Marks.Single(m => m.Type == InlineMarkType.Link);
        Assert.Equal("https://example.com", linkMark.Link?.Href);
    }

    [Fact]
    public void Normalize_UrlWithWhitespace_TrimsAndCreatesLink()
    {
        var output = Create().Normalize(new DocumentClipboardInput { PlainText = "  https://example.com  " });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var run = Assert.IsType<TextRun>(para.Inlines.Single());
        Assert.Equal("https://example.com", run.Text);
        Assert.Equal("https://example.com", run.Marks.Single(m => m.Type == InlineMarkType.Link).Link?.Href);
    }
}
