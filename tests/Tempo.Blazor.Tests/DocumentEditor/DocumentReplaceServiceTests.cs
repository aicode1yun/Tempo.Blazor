using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentReplaceServiceTests
{
    private static DocumentSearchService Search() => new();
    private static DocumentReplaceService Replace() => new();

    private static DocumentEditorDocument Doc(params DocumentBlock[] blocks) =>
        new() { Blocks = [.. blocks] };

    private static DocumentBlock Para(string id, params (string text, InlineMarkType[] marks)[] runs) => new()
    {
        Id = id,
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent
        {
            Inlines = [.. runs.Select(r => (InlineContent)new TextRun
            {
                Id = Guid.NewGuid().ToString("N"),
                Text = r.text,
                Marks = [.. r.marks.Select(m => new InlineMark { Type = m })]
            })]
        }
    };

    private static DocumentBlock SimplePara(string id, string text) =>
        Para(id, (text, []));

    private static DocumentBlock ContentControlBlock(string id, params DocumentBlock[] blocks) => new()
    {
        Id = id,
        Type = DocumentBlockType.ContentControl,
        Content = new ContentControlBlockContent
        {
            Blocks = [.. blocks]
        }
    };

    private static string GetFlatText(DocumentBlock block) =>
        block.Content is ParagraphBlockContent p
            ? string.Concat(p.Inlines.OfType<TextRun>().Select(r => r.Text))
            : string.Empty;

    // ─── ReplaceOne ───────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceOne_SimpleMatch_ReplacesText()
    {
        var doc = Doc(SimplePara("b1", "Hello world"));
        var results = Search().Search(doc, new DocumentSearchQuery { Text = "world" });
        Replace().ReplaceOne(doc, results[0], "everyone");

        Assert.Equal("Hello everyone", GetFlatText(doc.Blocks[0]));
    }

    [Fact]
    public void ReplaceOne_PreservesMarksOnNonMatchedRuns()
    {
        // "Hello " (plain) + "world" (bold)
        var doc = Doc(Para("b1",
            ("Hello ", []),
            ("world", [InlineMarkType.Bold])));

        var results = Search().Search(doc, new DocumentSearchQuery { Text = "Hello" });
        Replace().ReplaceOne(doc, results[0], "Hi");

        var para = (ParagraphBlockContent)doc.Blocks[0].Content;
        var boldRun = para.Inlines.OfType<TextRun>().FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        Assert.NotNull(boldRun);
        Assert.Contains("world", boldRun.Text);
    }

    [Fact]
    public void ReplaceOne_PreservesMarksOnMatchedRun()
    {
        // "bold text" in bold — replace "bold" with "strong", mark should be inherited
        var doc = Doc(Para("b1", ("bold text", [InlineMarkType.Bold])));
        var results = Search().Search(doc, new DocumentSearchQuery { Text = "bold" });
        Replace().ReplaceOne(doc, results[0], "strong");

        var para = (ParagraphBlockContent)doc.Blocks[0].Content;
        var boldRun = para.Inlines.OfType<TextRun>().FirstOrDefault(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        Assert.NotNull(boldRun);
        Assert.Contains("strong", boldRun.Text);
    }

    [Fact]
    public void ReplaceOne_MatchAtBeginning_Works()
    {
        var doc = Doc(SimplePara("b1", "Hello world"));
        var results = Search().Search(doc, new DocumentSearchQuery { Text = "Hello" });
        Replace().ReplaceOne(doc, results[0], "Hi");
        Assert.Equal("Hi world", GetFlatText(doc.Blocks[0]));
    }

    [Fact]
    public void ReplaceOne_MatchAtEnd_Works()
    {
        var doc = Doc(SimplePara("b1", "Hello world"));
        var results = Search().Search(doc, new DocumentSearchQuery { Text = "world" });
        Replace().ReplaceOne(doc, results[0], "earth");
        Assert.Equal("Hello earth", GetFlatText(doc.Blocks[0]));
    }

    [Fact]
    public void ReplaceOne_EmptyReplacement_RemovesMatch()
    {
        var doc = Doc(SimplePara("b1", "Hello world"));
        var results = Search().Search(doc, new DocumentSearchQuery { Text = " world" });
        Replace().ReplaceOne(doc, results[0], "");
        Assert.Equal("Hello", GetFlatText(doc.Blocks[0]));
    }

    // ─── ReplaceAll ───────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_SingleBlock_ReplacesAllOccurrences()
    {
        var doc = Doc(SimplePara("b1", "cat and cat and cat"));
        var count = Replace().ReplaceAll(doc, new DocumentSearchQuery { Text = "cat" }, "dog");
        Assert.Equal(3, count);
        Assert.Equal("dog and dog and dog", GetFlatText(doc.Blocks[0]));
    }

    [Fact]
    public void ReplaceAll_MultipleBlocks_ReplacesAcrossBlocks()
    {
        var doc = Doc(SimplePara("b1", "first cat"), SimplePara("b2", "second cat"));
        var count = Replace().ReplaceAll(doc, new DocumentSearchQuery { Text = "cat" }, "dog");
        Assert.Equal(2, count);
        Assert.Equal("first dog", GetFlatText(doc.Blocks[0]));
        Assert.Equal("second dog", GetFlatText(doc.Blocks[1]));
    }

    [Fact]
    public void ReplaceAll_NoMatch_ReturnsZero()
    {
        var doc = Doc(SimplePara("b1", "Hello world"));
        var count = Replace().ReplaceAll(doc, new DocumentSearchQuery { Text = "xyz" }, "abc");
        Assert.Equal(0, count);
        Assert.Equal("Hello world", GetFlatText(doc.Blocks[0]));
    }

    [Fact]
    public void ReplaceAll_CaseSensitive_OnlyReplacesExactCase()
    {
        var doc = Doc(SimplePara("b1", "Cat cat CAT"));
        var count = Replace().ReplaceAll(doc,
            new DocumentSearchQuery { Text = "cat", CaseSensitive = true }, "dog");
        Assert.Equal(1, count);
        Assert.Equal("Cat dog CAT", GetFlatText(doc.Blocks[0]));
    }

    [Fact]
    public void ReplaceAll_ContentControlBlock_ReplacesTextInNestedBlock()
    {
        var nested = SimplePara("nested-p", "Template cat text");
        var doc = Doc(ContentControlBlock("cc1", nested));

        var count = Replace().ReplaceAll(doc, new DocumentSearchQuery { Text = "cat" }, "dog");

        Assert.Equal(1, count);
        Assert.Equal("Template dog text", GetFlatText(nested));
    }
}
