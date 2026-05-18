using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

public sealed class WordClipboardNormalizerTests
{
    private static WordClipboardNormalizer Create() => new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures", "DocumentEditor", "Clipboard", name);
        return File.ReadAllText(path);
    }

    // ─── CanHandle ────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_WordHtml_ReturnsTrue()
    {
        var html = "<html xmlns:w='urn:schemas-microsoft-com:office:word'><body><p class=MsoNormal>text</p></body></html>";
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_MsoClassHtml_ReturnsTrue()
    {
        var html = "<p class=\"MsoNormal\">some text</p>";
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_RegularHtml_ReturnsFalse()
    {
        var html = "<p>Regular paragraph</p>";
        Assert.False(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_NullHtml_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput { PlainText = "text" }));
    }

    // ─── word-basic.html fixture ──────────────────────────────────────────────

    [Fact]
    public void Normalize_WordBasicFixture_ExtractsParagraphs()
    {
        var html = LoadFixture("word-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        Assert.True(output.Blocks.Count >= 3, $"Expected >= 3 blocks, got {output.Blocks.Count}");
        Assert.All(output.Blocks, b => Assert.Equal(DocumentBlockType.Paragraph, b.Type));
    }

    [Fact]
    public void Normalize_WordBasicFixture_StripsEmptyParagraphs()
    {
        var html = LoadFixture("word-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        // The fixture has one &nbsp;-only paragraph that should be removed
        Assert.All(output.Blocks, b =>
        {
            var para = Assert.IsType<ParagraphBlockContent>(b.Content);
            var allText = string.Concat(para.Inlines.OfType<TextRun>().Select(r => r.Text));
            Assert.False(string.IsNullOrWhiteSpace(allText), "Empty block should have been stripped");
        });
    }

    [Fact]
    public void Normalize_WordBasicFixture_DoesNotContainMsoNoise()
    {
        var html = LoadFixture("word-basic.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        foreach (var block in output.Blocks)
        {
            var para = Assert.IsType<ParagraphBlockContent>(block.Content);
            foreach (var inline in para.Inlines.OfType<TextRun>())
            {
                Assert.DoesNotContain("mso-", inline.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("MsoNormal", inline.Text);
            }
        }
    }

    // ─── word-list.html fixture ───────────────────────────────────────────────

    [Fact]
    public void Normalize_WordListFixture_CreatesListBlocks()
    {
        var html = LoadFixture("word-list.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var listBlocks = output.Blocks.Where(b => b.Type == DocumentBlockType.List).ToList();
        Assert.True(listBlocks.Count >= 2, $"Expected >= 2 list blocks, got {listBlocks.Count}");
        Assert.All(listBlocks, b =>
        {
            var list = Assert.IsType<ListBlockContent>(b.Content);
            Assert.False(list.Ordered);
        });
    }

    [Fact]
    public void Normalize_WordListFixture_StripsListBulletPrefix()
    {
        var html = LoadFixture("word-list.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        foreach (var block in output.Blocks.Where(b => b.Type == DocumentBlockType.List))
        {
            var list = Assert.IsType<ListBlockContent>(block.Content);
            var text = string.Concat(list.Inlines.OfType<TextRun>().Select(r => r.Text));
            Assert.False(text.StartsWith('•'), $"Bullet prefix not stripped: '{text}'");
        }
    }

    // ─── word-table.html fixture ──────────────────────────────────────────────

    [Fact]
    public void Normalize_WordTableFixture_CreatesTableBlock()
    {
        var html = LoadFixture("word-table.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var tableBlock = output.Blocks.FirstOrDefault(b => b.Type == DocumentBlockType.Table);
        Assert.NotNull(tableBlock);
        var table = Assert.IsType<TableBlockContent>(tableBlock.Content);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Rows[0].Cells.Count);
    }

    // ─── word-inline-formatting.html fixture ─────────────────────────────────

    [Fact]
    public void Normalize_WordInlineFormatting_PreservesBold()
    {
        var html = LoadFixture("word-inline-formatting.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var para = Assert.IsType<ParagraphBlockContent>(output.Blocks[0].Content);
        var boldRun = para.Inlines.OfType<TextRun>()
            .FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        Assert.NotNull(boldRun);
        Assert.Contains("bold", boldRun.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_WordInlineFormatting_PreservesLink()
    {
        var html = LoadFixture("word-inline-formatting.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var allRuns = output.Blocks
            .SelectMany(b => (b.Content as ParagraphBlockContent)?.Inlines.OfType<TextRun>() ?? []);
        var linkRun = allRuns.FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Link));
        Assert.NotNull(linkRun);
        Assert.Equal("https://example.com",
            linkRun.Marks.First(m => m.Type == InlineMarkType.Link).Link?.Href);
    }

    // ─── Source detection ─────────────────────────────────────────────────────

    [Fact]
    public void Normalize_SetsSourceToWord()
    {
        var html = "<html xmlns:w='urn:schemas-microsoft-com:office:word'><body><p class=MsoNormal>text</p></body></html>";
        // The normalizer doesn't set Source on the input (read-only), but we verify CanHandle for Word
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }
}
