using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class DocumentOutlineServiceTests
{
    private static DocumentEditorDocument MakeDocument(params DocumentBlock[] blocks)
    {
        var doc = new DocumentEditorDocument();
        doc.Blocks.AddRange(blocks);
        return doc;
    }

    private static DocumentBlock MakeHeading(string id, int level, string text) => new()
    {
        Id = id,
        Type = DocumentBlockType.Heading,
        Content = new HeadingBlockContent
        {
            Level = level,
            Inlines = [new TextRun { Text = text }]
        }
    };

    private static DocumentBlock MakeParagraph(string id, string text) => new()
    {
        Id = id,
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent
        {
            Inlines = [new TextRun { Text = text }]
        }
    };

    // ── 14.3.1 – Basic outline extraction ──────────────────────────────────

    [Fact]
    public void GetOutline_EmptyDocument_ReturnsEmpty()
    {
        var svc = new DocumentOutlineService();
        var result = svc.GetOutline(new DocumentEditorDocument());
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetOutline_NoHeadings_ReturnsEmpty()
    {
        var svc = new DocumentOutlineService();
        var doc = MakeDocument(MakeParagraph("p1", "Some text"));
        svc.GetOutline(doc).Should().BeEmpty();
    }

    [Fact]
    public void GetOutline_SingleHeading_ReturnsSingleItem()
    {
        var svc = new DocumentOutlineService();
        var doc = MakeDocument(MakeHeading("h1", 1, "Introduction"));

        var result = svc.GetOutline(doc);

        result.Should().HaveCount(1);
        result[0].BlockId.Should().Be("h1");
        result[0].Level.Should().Be(1);
        result[0].Text.Should().Be("Introduction");
    }

    [Fact]
    public void GetOutline_MultipleHeadings_ReturnsInOrder()
    {
        var svc = new DocumentOutlineService();
        var doc = MakeDocument(
            MakeHeading("h1", 1, "Chapter 1"),
            MakeParagraph("p1", "text"),
            MakeHeading("h2", 2, "Section 1.1"),
            MakeHeading("h3", 1, "Chapter 2"));

        var result = svc.GetOutline(doc);

        result.Should().HaveCount(3);
        result[0].Text.Should().Be("Chapter 1");
        result[1].Text.Should().Be("Section 1.1");
        result[2].Text.Should().Be("Chapter 2");
    }

    [Fact]
    public void GetOutline_HeadingWithMultipleRuns_ConcatenatesText()
    {
        var svc = new DocumentOutlineService();
        var block = new DocumentBlock
        {
            Id = "h1",
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = "Hello " }, new TextRun { Text = "World" }]
            }
        };
        var doc = MakeDocument(block);

        var result = svc.GetOutline(doc);

        result[0].Text.Should().Be("Hello World");
    }

    [Fact]
    public void GetOutline_TokenRunInHeading_UsesDisplayName()
    {
        var svc = new DocumentOutlineService();
        var block = new DocumentBlock
        {
            Id = "h1",
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent
            {
                Level = 2,
                Inlines = [new TokenRun { Key = "clientName", DisplayName = "Client Name" }]
            }
        };
        var doc = MakeDocument(block);

        var result = svc.GetOutline(doc);

        result[0].Text.Should().Be("Client Name");
    }

    [Fact]
    public void GetOutline_EmptyHeading_IncludesItemWithEmptyText()
    {
        var svc = new DocumentOutlineService();
        var doc = MakeDocument(MakeHeading("h1", 1, ""));

        var result = svc.GetOutline(doc);

        result.Should().HaveCount(1);
        result[0].Text.Should().Be("");
    }

    [Fact]
    public void GetOutline_NullDocument_ThrowsArgumentNullException()
    {
        var svc = new DocumentOutlineService();
        var act = () => svc.GetOutline(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── 14.3.2 – DocumentOutlineItem model ────────────────────────────────

    [Fact]
    public void OutlineItem_RecordEquality_ByProperties()
    {
        var a = new DocumentOutlineItem("h1", 1, "Intro");
        var b = new DocumentOutlineItem("h1", 1, "Intro");
        a.Should().Be(b);
    }

    [Fact]
    public void OutlineItem_DifferentBlockId_NotEqual()
    {
        var a = new DocumentOutlineItem("h1", 1, "Intro");
        var b = new DocumentOutlineItem("h2", 1, "Intro");
        a.Should().NotBe(b);
    }
}
