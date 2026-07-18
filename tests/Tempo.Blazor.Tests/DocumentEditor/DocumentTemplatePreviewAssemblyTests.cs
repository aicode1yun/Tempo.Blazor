using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Template preview must run full document assembly: conditional chains and computed tokens
/// evaluate against provider-resolved test data, so the editor's preview-with-test-data toggle
/// shows the assembled result, not just token substitution.
/// </summary>
public class DocumentTemplatePreviewAssemblyTests
{
    [Fact]
    public async Task CreatePreview_EvaluatesConditionalBlocksAgainstResolvedTokenValues()
    {
        var template = DocumentEditorDocument.Empty();
        template.DocumentId = "assembly-preview";
        template.Blocks =
        [
            new DocumentBlock
            {
                Id = "c-if",
                Type = DocumentBlockType.ContentControl,
                Order = 1,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("if", "amount > 10000", "g1"),
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = "p-if",
                            Type = DocumentBlockType.Paragraph,
                            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Schválení ředitele." }] },
                        },
                    ],
                },
            },
            new DocumentBlock
            {
                Id = "c-else",
                Type = DocumentBlockType.ContentControl,
                Order = 2,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("else", null, "g1"),
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = "p-else",
                            Type = DocumentBlockType.Paragraph,
                            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Bez schválení." }] },
                        },
                    ],
                },
            },
        ];

        var provider = new StaticTokenValueProvider(new Dictionary<string, DocumentTokenValue>
        {
            ["amount"] = new() { Key = "amount", Value = "25000" },
        });

        var preview = await new DocumentTemplatePreviewService(provider).CreatePreviewAsync(
            template,
            new DocumentTokenResolutionContext { DocumentId = "assembly-preview" });

        var text = string.Join(" ", preview.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<TextRun>())
            .Select(run => run.Text));
        text.Should().Contain("Schválení ředitele.").And.NotContain("Bez schválení.");
        preview.Blocks.Should().NotContain(block => block.Content is ContentControlBlockContent);
    }

    private sealed class StaticTokenValueProvider(IReadOnlyDictionary<string, DocumentTokenValue> values)
        : IDocumentTokenValueProvider
    {
        public Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
            DocumentTokenResolutionContext context,
            IReadOnlyList<TokenRun> tokens,
            CancellationToken cancellationToken = default)
            => Task.FromResult(values);
    }
}
