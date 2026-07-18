using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 10: redaction with REAL content removal. DocumentRedactionService.Apply produces an
/// export copy where every run marked InlineMarkType.Redaction has its text replaced by black
/// blocks (█) — the original characters no longer exist in the canonical model, so no export
/// format (DOCX XML, PDF text layer, HTML) can leak them. The source document is never mutated.
/// </summary>
public sealed class DocumentRedactionServiceTests
{
    [Fact]
    public void Apply_ReplacesRedactedRunTextWithBlocks()
    {
        var document = DocumentWithParagraph(
            new TextRun { Text = "Císlo účtu: " },
            Redacted(new TextRun { Text = "123456789/0100" }),
            new TextRun { Text = " je tajné." });

        var result = DocumentRedactionService.Apply(document);

        var runs = Runs(result);
        runs[0].Text.Should().Be("Císlo účtu: ");
        runs[1].Text.Should().Be(new string('█', "123456789/0100".Length));
        runs[2].Text.Should().Be(" je tajné.");
        Plain(result).Should().NotContain("123456789");
    }

    [Fact]
    public void Apply_DoesNotMutateTheSourceDocument()
    {
        var document = DocumentWithParagraph(Redacted(new TextRun { Text = "secret" }));

        DocumentRedactionService.Apply(document);

        Runs(document)[0].Text.Should().Be("secret", "Apply must work on a copy");
    }

    [Fact]
    public void Apply_RedactsInsideTableCellsHeadersAndNotes()
    {
        var document = DocumentWithParagraph(new TextRun { Text = "Body" });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "table",
            Type = DocumentBlockType.Table,
            Order = 20,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Id = "cell-p",
                                        Type = DocumentBlockType.Paragraph,
                                        Content = new ParagraphBlockContent
                                        {
                                            Inlines = [Redacted(new TextRun { Text = "cell-secret" })]
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "hdr",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "hdr-p",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent { Inlines = [Redacted(new TextRun { Text = "header-secret" })] }
                }
            ]
        });

        document.Notes.Add(new DocumentNote
        {
            Id = "note-1",
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "note-p",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent { Inlines = [Redacted(new TextRun { Text = "note-secret" })] }
                }
            ]
        });

        var result = DocumentRedactionService.Apply(document);

        var all = AllText(result);
        all.Should().NotContain("cell-secret");
        all.Should().NotContain("header-secret");
        all.Should().Contain("Body");
        result.Notes[0].Blocks
            .Select(block => block.Content).OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<TextRun>())
            .Should().NotContain(run => run.Text.Contains("note-secret"), "footnote/endnote bodies must be redacted too");
    }

    [Fact]
    public void Apply_WithoutRedactionMarks_ReturnsEquivalentDocument()
    {
        var document = DocumentWithParagraph(new TextRun { Text = "Nothing secret here" });

        var result = DocumentRedactionService.Apply(document);

        Plain(result).Should().Be("Nothing secret here");
    }

    [Fact]
    public void HasRedactions_DetectsMarksAnywhereInTheDocument()
    {
        var clean = DocumentWithParagraph(new TextRun { Text = "clean" });
        var marked = DocumentWithParagraph(Redacted(new TextRun { Text = "secret" }));

        DocumentRedactionService.HasRedactions(clean).Should().BeFalse();
        DocumentRedactionService.HasRedactions(marked).Should().BeTrue();
    }

    private static TextRun Redacted(TextRun run)
    {
        run.Marks.Add(new InlineMark { Type = InlineMarkType.Redaction });
        return run;
    }

    private static DocumentEditorDocument DocumentWithParagraph(params InlineContent[] inlines)
    {
        var document = DocumentEditorDocument.Empty("redaction-test");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 10,
            Content = new ParagraphBlockContent { Inlines = [.. inlines] }
        });
        return document;
    }

    private static List<TextRun> Runs(DocumentEditorDocument document)
        => ((ParagraphBlockContent)document.Blocks[0].Content!).Inlines.OfType<TextRun>().ToList();

    private static string Plain(DocumentEditorDocument document)
        => string.Concat(Runs(document).Select(run => run.Text));

    private static string AllText(DocumentEditorDocument document)
    {
        var body = Tempo.Blazor.DocumentEditor.Services.DocumentTextDiffHelper.ExtractPlainText(document);
        var extras = document.HeadersFooters
            .SelectMany(part => part.Blocks)
            .Concat(document.Blocks
                .Select(block => block.Content)
                .OfType<TableBlockContent>()
                .SelectMany(table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks)))
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<TextRun>())
            .Select(run => run.Text);
        return body + string.Concat(extras);
    }
}
