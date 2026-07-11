using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Html;
using Tempo.Blazor.DocumentFormats.Markdown;
using Tempo.Blazor.DocumentFormats.Notion;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Pasting and importing content that did not come from this editor must not silently lose
/// structure, and everything the exporter writes must import back to the same document.
/// </summary>
public class DocumentPasteAndRoundTripTests
{
    // ── HTML import robustness ────────────────────────────────────────────

    // A non-breaking space is normalized to a plain space like any other whitespace: Word HTML is
    // full of &nbsp; and keeping them would make every pasted line unbreakable.
    [Theory]
    [InlineData("&nbsp;", ' ')]
    [InlineData("&mdash;", '—')]
    [InlineData("&hellip;", '…')]
    [InlineData("&laquo;", '«')]
    public void HtmlImport_DecodesNamedEntities(string entity, char expected)
    {
        var document = new DocumentHtmlImporter().Import($"<p>a{entity}b</p>");

        var block = document.Blocks.Should().ContainSingle().Subject;
        block.Type.Should().Be(DocumentBlockType.Paragraph);
        TextOf(block).Should().Be($"a{expected}b");
    }

    [Theory]
    [InlineData("<p>one</p><wbr><p>two</p>")]
    [InlineData("<p>one</p><input type=\"text\"><p>two</p>")]
    [InlineData("<p>one</p><source src=\"x\"><p>two</p>")]
    [InlineData("<meta charset=\"utf-8\"><p>one</p><p>two</p>")]
    [InlineData("<link rel=\"x\" href=\"y\"><p>one</p><p>two</p>")]
    public void HtmlImport_SurvivesUnclosedVoidElements(string html)
    {
        var document = new DocumentHtmlImporter().Import(html);

        // The lossy plaintext fallback collapses everything into one paragraph.
        document.Blocks.Count.Should().BeGreaterThanOrEqualTo(2,
            "the structure must survive an unclosed void element");
        document.Blocks.Should().OnlyContain(block => block.Type == DocumentBlockType.Paragraph);
    }

    [Fact]
    public void HtmlImport_UnparsableMarkupStillKeepsItsText()
    {
        var document = new DocumentHtmlImporter().Import("<p>kept</p><b>unclosed");

        string.Join(" ", document.Blocks.Select(TextOf)).Should().Contain("kept");
    }

    [Fact]
    public void HtmlImport_ReadsPreCodeAsACodeBlock()
    {
        var document = new DocumentHtmlImporter().Import("<pre><code>var x = 1;</code></pre>");

        var block = document.Blocks.Should().ContainSingle().Subject;
        block.Type.Should().Be(DocumentBlockType.Code);
        block.Content.Should().BeOfType<CodeBlockContent>().Which.Code.Should().Be("var x = 1;");
    }

    // ── Fenced code blocks ────────────────────────────────────────────────

    [Fact]
    public void MarkdownImport_ReadsFencedCodeBlock()
    {
        const string markdown = """
            ```csharp
            var x = 1;
            if (x > 0) { }
            ```
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var block = document.Blocks.Should().ContainSingle().Subject;
        block.Type.Should().Be(DocumentBlockType.Code);
        var code = block.Content.Should().BeOfType<CodeBlockContent>().Subject;
        code.Language.Should().Be("csharp");
        code.Code.Should().Be("var x = 1;\nif (x > 0) { }");
    }

    [Fact]
    public void MarkdownImport_DoesNotParseInlineMarkupInsideAFence()
    {
        const string markdown = """
            ```
            **not bold** and _not italic_
            ```
            """;

        var document = new DocumentMarkdownImporter().Import(markdown);

        var code = document.Blocks.Should().ContainSingle().Subject
            .Content.Should().BeOfType<CodeBlockContent>().Subject;
        code.Code.Should().Be("**not bold** and _not italic_");
    }

    [Fact]
    public void MarkdownImport_FenceWithoutLanguageHasNoLanguage()
    {
        var document = new DocumentMarkdownImporter().Import("```\nplain\n```");

        document.Blocks.Should().ContainSingle().Subject
            .Content.Should().BeOfType<CodeBlockContent>().Which.Language.Should().BeNull();
    }

    [Fact]
    public void MarkdownImport_UnterminatedFenceStillYieldsACodeBlock()
    {
        var document = new DocumentMarkdownImporter().Import("```\nstill code");

        document.Blocks.Should().ContainSingle().Subject
            .Content.Should().BeOfType<CodeBlockContent>().Which.Code.Should().Be("still code");
    }

    [Fact]
    public void MarkdownExport_WritesFencedCodeBlock()
    {
        var document = DocumentOf(new DocumentBlock
        {
            Type = DocumentBlockType.Code,
            Order = 0,
            Content = new CodeBlockContent { Language = "csharp", Code = "var x = 1;" }
        });

        new DocumentMarkdownExporter().Export(document).Should().Contain("```csharp\nvar x = 1;\n```");
    }

    [Fact]
    public void Markdown_CodeBlockSurvivesARoundTrip()
    {
        const string markdown = "```csharp\nvar x = **1**;\n```";

        var exported = new DocumentMarkdownExporter().Export(new DocumentMarkdownImporter().Import(markdown));
        var reimported = new DocumentMarkdownImporter().Import(exported);

        var code = reimported.Blocks.Should().ContainSingle().Subject
            .Content.Should().BeOfType<CodeBlockContent>().Subject;
        code.Language.Should().Be("csharp");
        code.Code.Should().Be("var x = **1**;");
    }

    // ── Intra-word underscores ────────────────────────────────────────────

    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("a_b_c")]
    [InlineData("file__name")]
    public void MarkdownImport_IntraWordUnderscoreIsNotEmphasis(string text)
    {
        var document = new DocumentMarkdownImporter().Import(text);

        var inlines = ((ParagraphBlockContent)document.Blocks.Single().Content!).Inlines;
        inlines.Should().OnlyContain(inline => inline.Marks.Count == 0,
            "underscores inside a word are literal characters, not emphasis");
        string.Concat(inlines.OfType<TextRun>().Select(run => run.Text)).Should().Be(text);
    }

    [Theory]
    [InlineData("_italic_", InlineMarkType.Italic)]
    [InlineData("__bold__", InlineMarkType.Bold)]
    public void MarkdownImport_UnderscoreAtWordBoundaryStillEmphasises(string markdown, InlineMarkType expected)
    {
        var document = new DocumentMarkdownImporter().Import(markdown);

        var inlines = ((ParagraphBlockContent)document.Blocks.Single().Content!).Inlines;
        inlines.Should().ContainSingle().Which.Marks.Should().ContainSingle()
            .Which.Type.Should().Be(expected);
    }

    [Fact]
    public void MarkdownImport_LeadingDoubleUnderscoreStillBolds()
    {
        // CommonMark bolds "__init__" — the delimiters sit at word boundaries. Only underscores
        // with a word character on the outside are literal.
        var document = new DocumentMarkdownImporter().Import("__init__");

        var inlines = ((ParagraphBlockContent)document.Blocks.Single().Content!).Inlines;
        inlines.Should().ContainSingle().Which.Marks.Should().ContainSingle()
            .Which.Type.Should().Be(InlineMarkType.Bold);
    }

    [Fact]
    public void MarkdownImport_UnderscoreEmphasisInsideASentenceStillWorks()
    {
        var document = new DocumentMarkdownImporter().Import("say _hello_ now");

        var inlines = ((ParagraphBlockContent)document.Blocks.Single().Content!).Inlines;
        inlines.Should().Contain(inline => inline.Marks.Any(mark => mark.Type == InlineMarkType.Italic));
    }

    // ── Inline code round-trip ────────────────────────────────────────────

    [Fact]
    public void MarkdownExport_RendersInlineCodeAsBackticks()
    {
        var document = DocumentOf(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "call " },
                    new TextRun { Text = "Foo()", Marks = [new InlineMark { Type = InlineMarkType.FontFamily }] }
                ]
            }
        });

        new DocumentMarkdownExporter().Export(document).Should().Contain("call `Foo()`");
    }

    [Fact]
    public void Markdown_InlineCodeSurvivesARoundTrip()
    {
        var reimported = new DocumentMarkdownImporter().Import(
            new DocumentMarkdownExporter().Export(new DocumentMarkdownImporter().Import("call `Foo()` now")));

        var inlines = ((ParagraphBlockContent)reimported.Blocks.Single().Content!).Inlines;
        inlines.Should().Contain(inline =>
            inline.Marks.Any(mark => mark.Type == InlineMarkType.FontFamily));
        string.Concat(inlines.OfType<TextRun>().Select(run => run.Text)).Should().Be("call Foo() now");
    }

    // ── Task lists ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("- [ ] open", false)]
    [InlineData("- [x] done", true)]
    [InlineData("- [X] done", true)]
    public void MarkdownImport_ReadsTaskListItems(string markdown, bool expectedChecked)
    {
        var document = new DocumentMarkdownImporter().Import(markdown);

        var list = document.Blocks.Should().ContainSingle().Subject
            .Content.Should().BeOfType<ListBlockContent>().Subject;
        list.IsChecked.Should().Be(expectedChecked);
        TextOf(document.Blocks[0]).Should().NotContain("[", "the checkbox is state, not text");
    }

    [Fact]
    public void MarkdownImport_PlainListItemIsNotATask()
    {
        var document = new DocumentMarkdownImporter().Import("- plain");

        document.Blocks.Single().Content.Should().BeOfType<ListBlockContent>()
            .Which.IsChecked.Should().BeNull();
    }

    [Fact]
    public void Markdown_TaskListSurvivesARoundTrip()
    {
        var exported = new DocumentMarkdownExporter().Export(new DocumentMarkdownImporter().Import("- [x] done"));

        exported.Should().Contain("- [x] done");
        new DocumentMarkdownImporter().Import(exported).Blocks.Single()
            .Content.Should().BeOfType<ListBlockContent>().Which.IsChecked.Should().BeTrue();
    }

    // ── Divider / page break symmetry ─────────────────────────────────────

    [Fact]
    public void MarkdownExport_PageBreakIsAThematicBreak() =>
        new DocumentMarkdownExporter().Export(DocumentOf(new DocumentBlock
        {
            Type = DocumentBlockType.PageBreak,
            Order = 0,
            Content = new PageBreakBlockContent()
        })).Trim().Should().Be("---");

    [Fact]
    public void Markdown_ThematicBreakSurvivesARoundTrip()
    {
        var reimported = new DocumentMarkdownImporter().Import(
            new DocumentMarkdownExporter().Export(new DocumentMarkdownImporter().Import("---")));

        reimported.Blocks.Should().ContainSingle().Which.Type.Should().Be(DocumentBlockType.PageBreak);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static DocumentEditorDocument DocumentOf(params DocumentBlock[] blocks)
    {
        var document = new DocumentEditorDocument();
        foreach (var block in blocks) document.Blocks.Add(block);
        return document;
    }

    private static string TextOf(DocumentBlock block) => block.Content switch
    {
        ParagraphBlockContent paragraph => Text(paragraph.Inlines),
        HeadingBlockContent heading => Text(heading.Inlines),
        ListBlockContent list => Text(list.Inlines),
        QuoteBlockContent quote => Text(quote.Inlines),
        CodeBlockContent code => code.Code,
        _ => string.Empty
    };

    private static string Text(IEnumerable<InlineContent> inlines) =>
        string.Concat(inlines.OfType<TextRun>().Select(run => run.Text));

    // ── Notion converter symmetry ─────────────────────────────────────────

    [Fact]
    public void NotionDivider_BecomesAPageBreak_AndComesBack()
    {
        var pageId = Guid.NewGuid();
        var notion = DocumentModelToNotionConverter.ConvertBlocks(
            [new DocumentBlock { Type = DocumentBlockType.PageBreak, Order = 0, Content = new PageBreakBlockContent() }],
            pageId);

        notion.Should().ContainSingle().Which.Type.Should().Be(BlockType.Divider);

        var back = NotionToDocumentModelConverter.ConvertBlocks(notion);
        back.Should().ContainSingle().Which.Type.Should().Be(DocumentBlockType.PageBreak);
    }

    [Fact]
    public void NotionCodeBlock_RoundTripsThroughTheDocumentModel()
    {
        var pageId = Guid.NewGuid();
        var notion = DocumentModelToNotionConverter.ConvertBlocks(
            [new DocumentBlock
            {
                Type = DocumentBlockType.Code,
                Order = 0,
                Content = new CodeBlockContent { Language = "csharp", Code = "var x = 1;" }
            }],
            pageId);

        notion.Should().ContainSingle().Which.Type.Should().Be(BlockType.Code);

        var back = NotionToDocumentModelConverter.ConvertBlocks(notion);
        var code = back.Should().ContainSingle().Subject
            .Content.Should().BeOfType<CodeBlockContent>().Subject;
        code.Language.Should().Be("csharp");
        code.Code.Should().Be("var x = 1;");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TaskListItem_RoundTripsThroughNotionAsATodo(bool isChecked)
    {
        var notion = DocumentModelToNotionConverter.ConvertBlocks(
            [new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Order = 0,
                Content = new ListBlockContent { IsChecked = isChecked, Inlines = [new TextRun { Text = "task" }] }
            }],
            Guid.NewGuid());

        notion.Should().ContainSingle().Which.Type.Should().Be(BlockType.TodoItem);

        var back = NotionToDocumentModelConverter.ConvertBlocks(notion);
        var list = back.Should().ContainSingle().Subject
            .Content.Should().BeOfType<ListBlockContent>().Subject;
        list.IsChecked.Should().Be(isChecked);
        Text(list.Inlines).Should().Be("task");
    }

    [Fact]
    public void LegacyTaskPrefixInTheTextStillBecomesATodo()
    {
        // Documents built by 2.0.x callers encode the checkbox as a literal "[x] " prefix.
        var notion = DocumentModelToNotionConverter.ConvertBlocks(
            [new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Order = 0,
                Content = new ListBlockContent { Inlines = [new TextRun { Text = "[x] legacy" }] }
            }],
            Guid.NewGuid());

        notion.Should().ContainSingle().Which.Type.Should().Be(BlockType.TodoItem);
    }
}
