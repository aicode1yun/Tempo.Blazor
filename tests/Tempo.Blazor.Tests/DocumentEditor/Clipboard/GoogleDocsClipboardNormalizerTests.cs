using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

public sealed class GoogleDocsClipboardNormalizerTests
{
    private static GoogleDocsClipboardNormalizer Create() => new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DocumentEditor", "Clipboard", name);
        return File.ReadAllText(path);
    }

    // ─── CanHandle ────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_GoogleDocsGuid_ReturnsTrue()
    {
        var html = "<b id='docs-internal-guid-12345678-0000-0000-0000-000000000000'><p>text</p></b>";
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_RegularHtml_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput { Html = "<p>Regular text</p>" }));
    }

    [Fact]
    public void CanHandle_NullHtml_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput()));
    }

    // ─── google-docs-basic.html ───────────────────────────────────────────────

    [Fact]
    public void Normalize_GoogleDocsBasicFixture_ExtractsTwoParagraphs()
    {
        var html = LoadFixture("google-docs-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        Assert.Equal(2, output.Blocks.Count);
        Assert.All(output.Blocks, b => Assert.Equal(DocumentBlockType.Paragraph, b.Type));
    }

    [Fact]
    public void Normalize_GoogleDocsBasicFixture_ExtractsText()
    {
        var html = LoadFixture("google-docs-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var firstPara = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var text = string.Concat(firstPara.Inlines.OfType<TextRun>().Select(r => r.Text));
        Assert.Contains("Google Docs", text);
    }

    [Fact]
    public void Normalize_GoogleDocsBasicFixture_PreservesBoldFromFontWeight()
    {
        var html = LoadFixture("google-docs-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var secondPara = Assert.IsType<ParagraphBlockContent>(output.Blocks[1].Content);
        var boldRun = secondPara.Inlines.OfType<TextRun>()
            .FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        Assert.NotNull(boldRun);
        Assert.Contains("bold", boldRun.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_GoogleDocsBasicFixture_PreservesItalicFromFontStyle()
    {
        var html = LoadFixture("google-docs-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var secondPara = Assert.IsType<ParagraphBlockContent>(output.Blocks[1].Content);
        var italicRun = secondPara.Inlines.OfType<TextRun>()
            .FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Italic));
        Assert.NotNull(italicRun);
        Assert.Contains("italic", italicRun.Text, StringComparison.OrdinalIgnoreCase);
    }

    // ─── google-docs-headings.html ────────────────────────────────────────────

    [Fact]
    public void Normalize_GoogleDocsHeadingsFixture_DetectsH1()
    {
        var html = LoadFixture("google-docs-headings.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var h1 = output.Blocks.FirstOrDefault(b => b.Type == DocumentBlockType.Heading);
        Assert.NotNull(h1);
        var heading = Assert.IsType<HeadingBlockContent>(h1.Content);
        Assert.Equal(1, heading.Level);
        var text = string.Concat(heading.Inlines.OfType<TextRun>().Select(r => r.Text));
        Assert.Contains("Main Heading", text);
    }

    [Fact]
    public void Normalize_GoogleDocsHeadingsFixture_DetectsH2()
    {
        var html = LoadFixture("google-docs-headings.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var headings = output.Blocks
            .Where(b => b.Type == DocumentBlockType.Heading)
            .Select(b => Assert.IsType<HeadingBlockContent>(b.Content))
            .ToList();
        Assert.True(headings.Count >= 2);
        Assert.Equal(2, headings[1].Level);
    }

    [Fact]
    public void Normalize_GoogleDocsHeadingsFixture_HasParagraphAfterHeadings()
    {
        var html = LoadFixture("google-docs-headings.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var lastBlock = output.Blocks.Last();
        Assert.Equal(DocumentBlockType.Paragraph, lastBlock.Type);
    }
}
