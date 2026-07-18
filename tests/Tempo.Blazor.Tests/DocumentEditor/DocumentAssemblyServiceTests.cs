using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Document assembly: conditional block chains (IF/ELSEIF/ELSE over token values), repeating
/// sections bound to collection rows, and computed tokens — assembled into a plain document.
/// </summary>
public class DocumentAssemblyServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Assemble_IfTrue_KeepsIfBranchAndDropsElse()
    {
        var document = Template(
            ConditionalBlock("c-if", "if", "amount > 10000", "g1", Paragraph("p-if", "Vysoká hodnota.")),
            ConditionalBlock("c-else", "else", null, "g1", Paragraph("p-else", "Běžná hodnota.")));

        var assembled = Assemble(document, ("amount", "15000"));

        Text(assembled).Should().Contain("Vysoká hodnota.").And.NotContain("Běžná hodnota.");
        assembled.Blocks.Should().NotContain(block => block.Content is ContentControlBlockContent,
            "assembly unwraps the winning branch and removes the control wrappers");
    }

    [Fact]
    public void Assemble_IfFalse_KeepsElseBranch()
    {
        var document = Template(
            ConditionalBlock("c-if", "if", "amount > 10000", "g1", Paragraph("p-if", "Vysoká hodnota.")),
            ConditionalBlock("c-else", "else", null, "g1", Paragraph("p-else", "Běžná hodnota.")));

        var assembled = Assemble(document, ("amount", "500"));

        Text(assembled).Should().Contain("Běžná hodnota.").And.NotContain("Vysoká hodnota.");
    }

    [Fact]
    public void Assemble_ElseIfChain_PicksFirstTrueBranch()
    {
        var document = Template(
            ConditionalBlock("c-if", "if", "amount > 100000", "g1", Paragraph("p1", "Obrovská.")),
            ConditionalBlock("c-elseif", "elseif", "amount > 10000", "g1", Paragraph("p2", "Velká.")),
            ConditionalBlock("c-else", "else", null, "g1", Paragraph("p3", "Malá.")));

        var assembled = Assemble(document, ("amount", "20000"));

        Text(assembled).Should().Contain("Velká.").And.NotContain("Obrovská.").And.NotContain("Malá.");
    }

    [Fact]
    public void Assemble_RepeatingSection_EmitsOneCopyPerRowWithRowTokens()
    {
        var repeating = new DocumentBlock
        {
            Id = "rep-1",
            Type = DocumentBlockType.ContentControl,
            Content = new ContentControlBlockContent
            {
                Control = DocumentAssemblyMetadata.CreateRepeatingSection("items"),
                Blocks =
                [
                    new DocumentBlock
                    {
                        Id = "rep-row",
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent
                        {
                            Inlines =
                            [
                                new TokenRun { Key = "name", DisplayName = "Name" },
                                new TextRun { Text = ": " },
                                new TokenRun { Key = "price", DisplayName = "Price" },
                            ],
                        },
                    },
                ],
            },
        };
        var document = Template(repeating);
        var values = new Dictionary<string, DocumentTokenValue>
        {
            ["items"] = new()
            {
                Key = "items",
                Rows =
                [
                    new Dictionary<string, string?> { ["name"] = "Licence", ["price"] = "1000" },
                    new Dictionary<string, string?> { ["name"] = "Podpora", ["price"] = "500" },
                ],
            },
        };

        var assembled = new DocumentAssemblyService().Assemble(document, values, new DocumentAssemblyOptions { Now = Now });

        var text = Text(assembled);
        text.Should().Contain("Licence: 1000").And.Contain("Podpora: 500");
        assembled.Blocks.Count(block => block.Content is ParagraphBlockContent).Should().Be(2, "one paragraph per row");
    }

    [Fact]
    public void Assemble_ExpressionToken_ComputesAggregateAndCurrency()
    {
        var document = Template(new DocumentBlock
        {
            Id = "total",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Celkem: " },
                    new TokenRun
                    {
                        Key = "total",
                        DisplayName = "Total",
                        Expression = "CURRENCY(SUM(items, 'price'), 'cs-CZ', 'CZK')",
                    },
                ],
            },
        });
        var values = new Dictionary<string, DocumentTokenValue>
        {
            ["items"] = new()
            {
                Key = "items",
                Rows =
                [
                    new Dictionary<string, string?> { ["price"] = "1000" },
                    new Dictionary<string, string?> { ["price"] = "500.50" },
                ],
            },
        };

        var assembled = new DocumentAssemblyService().Assemble(document, values, new DocumentAssemblyOptions { Now = Now });

        Text(assembled).Should().Contain("500,50").And.Contain("Kč");
    }

    [Fact]
    public void Assemble_PlainTokens_ResolveLikeTemplatePreview()
    {
        var document = Template(Paragraph("p1", null, new TokenRun { Key = "customer", DisplayName = "Customer" }));

        var assembled = Assemble(document, ("customer", "Novák s.r.o."));

        Text(assembled).Should().Contain("Novák s.r.o.");
    }

    [Fact]
    public void Assemble_DocumentWithoutAssemblyConstructs_IsUnchangedStructurally()
    {
        var document = Template(Paragraph("p1", "Obyčejný odstavec."));

        var assembled = Assemble(document);

        Text(assembled).Should().Be("Obyčejný odstavec.");
    }

    [Fact]
    public void Assemble_InvalidConditionExpression_FailsClosed_KeepsElse()
    {
        var document = Template(
            ConditionalBlock("c-if", "if", "??invalid??", "g1", Paragraph("p-if", "Podmíněný obsah.")),
            ConditionalBlock("c-else", "else", null, "g1", Paragraph("p-else", "Výchozí obsah.")));

        var assembled = Assemble(document);

        Text(assembled).Should().Contain("Výchozí obsah.").And.NotContain("Podmíněný obsah.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static DocumentEditorDocument Assemble(DocumentEditorDocument document, params (string Key, string Value)[] values)
        => new DocumentAssemblyService().Assemble(
            document,
            values.ToDictionary(pair => pair.Key, pair => new DocumentTokenValue { Key = pair.Key, Value = pair.Value }),
            new DocumentAssemblyOptions { Now = Now });

    private static DocumentEditorDocument Template(params DocumentBlock[] blocks)
    {
        var document = DocumentEditorDocument.Empty();
        document.DocumentId = "assembly-template";
        document.Blocks = blocks.ToList();
        for (var i = 0; i < document.Blocks.Count; i++)
        {
            document.Blocks[i].Order = i + 1;
        }

        return document;
    }

    private static DocumentBlock ConditionalBlock(string id, string branch, string? expression, string groupId, DocumentBlock inner)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.ContentControl,
            Content = new ContentControlBlockContent
            {
                Control = DocumentAssemblyMetadata.CreateConditionalBlock(branch, expression, groupId),
                Blocks = [inner],
            },
        };

    private static DocumentBlock Paragraph(string id, string? text, params InlineContent[] extraInlines)
    {
        var inlines = new List<InlineContent>();
        if (text is not null)
        {
            inlines.Add(new TextRun { Text = text });
        }

        inlines.AddRange(extraInlines);
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = inlines },
        };
    }

    private static string Text(DocumentEditorDocument document)
        => string.Join(
            "\n",
            document.Blocks.Select(block => block.Content switch
            {
                ParagraphBlockContent paragraph => string.Concat(paragraph.Inlines.Select(InlineText)),
                _ => string.Empty,
            }));

    private static string InlineText(InlineContent inline)
        => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => $"{{{{{token.Key}}}}}",
            _ => string.Empty,
        };
}
