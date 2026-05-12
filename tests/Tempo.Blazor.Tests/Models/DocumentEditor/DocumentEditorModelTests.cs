using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentEditorModelTests
{
    [Fact]
    public void Empty_CreatesDocumentWithSchemaVersionAndDefaultSection()
    {
        var document = DocumentEditorDocument.Empty("doc-1");

        document.DocumentId.Should().Be("doc-1");
        document.SchemaVersion.Should().Be(DocumentEditorDocument.CurrentSchemaVersion);
        document.Metadata.Status.Should().Be(DocumentEditorStatus.Draft);
        document.PageSettings.Size.Name.Should().Be("A4");
        document.PageSettings.Margins.Left.Should().Be(72);
        document.Sections.Should().ContainSingle();
        document.Sections[0].Properties.NoteNumbering.RestartEachSection.Should().BeTrue();
    }

    [Fact]
    public void Document_SerializesBlockContentWithTypeDiscriminator()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Hello" },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" }
                ]
            }
        });

        var json = JsonSerializer.Serialize(document);
        var restored = JsonSerializer.Deserialize<DocumentEditorDocument>(json);

        json.Should().Contain("\"$type\":\"paragraph\"");
        json.Should().Contain("\"$type\":\"text\"");
        json.Should().Contain("\"$type\":\"token\"");
        restored!.Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>();
        ((ParagraphBlockContent)restored.Blocks[0].Content).Inlines[1].Should().BeOfType<TokenRun>();
    }

    [Fact]
    public void DocumentJson_UpgradesLegacySnapshotAndRepairsMissingCollections()
    {
        const string json = """
            {
              "SchemaVersion": 0,
              "DocumentId": "legacy-doc",
              "Metadata": null,
              "PageSettings": null,
              "Sections": null,
              "Blocks": [
                {
                  "Id": "",
                  "Type": 0,
                  "Order": 1,
                  "Content": { "$type": "paragraph", "Inlines": [] }
                }
              ],
              "Comments": null,
              "Notes": null,
              "HeadersFooters": null,
              "Revisions": null,
              "Assets": null,
              "Anchors": null
            }
            """;

        var document = DocumentEditorJson.Deserialize(json);
        var normalized = DocumentEditorJson.Normalize(json);

        document.SchemaVersion.Should().Be(DocumentEditorDocument.CurrentSchemaVersion);
        document.DocumentId.Should().Be("legacy-doc");
        document.Metadata.Should().NotBeNull();
        document.PageSettings.Should().NotBeNull();
        document.Sections.Should().ContainSingle();
        document.Blocks.Should().ContainSingle();
        document.Blocks[0].Id.Should().NotBeNullOrWhiteSpace();
        document.Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>();
        document.Comments.Should().BeEmpty();
        document.Notes.Should().BeEmpty();
        document.HeadersFooters.Should().BeEmpty();
        document.Revisions.Should().BeEmpty();
        document.Assets.Should().BeEmpty();
        document.Anchors.Should().BeEmpty();
        normalized.Should().Contain("\"SchemaVersion\":1");
    }

    [Fact]
    public void DocumentJson_RejectsFutureSchemaVersion()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        var json = DocumentEditorJson.Serialize(document)
            .Replace("\"SchemaVersion\":1", "\"SchemaVersion\":999", StringComparison.Ordinal);

        FluentActions.Invoking(() => DocumentEditorJson.Deserialize(json))
            .Should()
            .Throw<JsonException>()
            .WithMessage("*Unsupported document editor schema version 999*");
    }

    [Fact]
    public void DocumentJson_RejectsUnknownBlockContentDiscriminator()
    {
        const string json = """
            {
              "SchemaVersion": 1,
              "DocumentId": "doc-1",
              "Blocks": [
                {
                  "Type": 0,
                  "Content": { "$type": "mystery", "Text": "Unsupported" }
                }
              ]
            }
            """;

        FluentActions.Invoking(() => DocumentEditorJson.Deserialize(json))
            .Should()
            .Throw<JsonException>();
    }

    [Fact]
    public void SectionProperties_CanReferenceHeadersFootersAndIndependentPageSettings()
    {
        var section = new DocumentSection
        {
            Id = "section-1",
            Properties = new DocumentSectionProperties
            {
                DifferentFirstPage = true,
                DifferentOddAndEvenPages = true,
                PageSettings = new DocumentPageSettings
                {
                    Size = DocumentPageSize.Letter,
                    Landscape = true
                },
                HeaderFooterReferences =
                [
                    new DocumentHeaderFooterReference
                    {
                        HeaderFooterId = "header-1",
                        Type = DocumentHeaderFooterType.Header,
                        Scope = DocumentHeaderFooterScope.FirstPage
                    }
                ]
            }
        };

        section.Properties.PageSettings.Size.Name.Should().Be("Letter");
        section.Properties.PageSettings.Landscape.Should().BeTrue();
        section.Properties.HeaderFooterReferences.Should().ContainSingle(reference =>
            reference.HeaderFooterId == "header-1" &&
            reference.Scope == DocumentHeaderFooterScope.FirstPage);
    }

    [Fact]
    public void InlineContent_StoresTextFormattingLinksTokensAndCommentAnchors()
    {
        var text = new TextRun
        {
            Text = "Agreement",
            Marks =
            [
                new InlineMark { Type = InlineMarkType.Bold },
                new InlineMark { Type = InlineMarkType.Italic },
                new InlineMark { Type = InlineMarkType.Underline },
                new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = "https://example.test" } },
                new InlineMark
                {
                    Type = InlineMarkType.CommentAnchor,
                    CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" }
                }
            ]
        };

        var token = new TokenRun
        {
            Key = "case.number",
            DisplayName = "Case number",
            FallbackText = "N/A"
        };

        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);
        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Italic);
        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Underline);
        text.Marks.Should().Contain(mark => mark.Link != null && mark.Link.Href == "https://example.test");
        text.Marks.Should().Contain(mark => mark.CommentAnchor != null && mark.CommentAnchor.CommentId == "comment-1");
        token.Key.Should().Be("case.number");
    }

    [Fact]
    public void TokenRun_StoresTypeMetadataAndSerializes()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TokenRun
                    {
                        Key = "client.name",
                        DisplayName = "Client name",
                        TokenType = "text",
                        TypeLabel = "Text",
                        ColorClass = "token-client",
                        Description = "Client full name"
                    }
                ]
            }
        });

        var json = JsonSerializer.Serialize(document);
        var restored = JsonSerializer.Deserialize<DocumentEditorDocument>(json);
        var token = ((ParagraphBlockContent)restored!.Blocks[0].Content).Inlines.OfType<TokenRun>().Single();

        token.TokenType.Should().Be("text");
        token.TypeLabel.Should().Be("Text");
        token.ColorClass.Should().Be("token-client");
        token.Description.Should().Be("Client full name");
    }

    [Fact]
    public void DocumentTokenHelper_ExtractsAndValidatesTokenKeys()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TokenRun { Key = "client.name", DisplayName = "Client name" },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" },
                    new TokenRun { Key = "case.number", DisplayName = "Case number" }
                ]
            }
        });

        var result = DocumentTokenHelper.ValidateTokens(document, ["client.name"]);

        result.TokenKeys.Should().BeEquivalentTo(["client.name", "case.number"]);
        result.DuplicateTokenKeys.Should().ContainSingle("client.name");
        result.MissingTokenKeys.Should().ContainSingle("case.number");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task TemplatePreview_ReplacesTokensWithValuesAndKeepsSourceUnchanged()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Dear " },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" },
                    new TextRun { Text = ", ref " },
                    new TokenRun { Key = "case.number", DisplayName = "Case number", FallbackText = "missing case" }
                ]
            }
        });
        var provider = new TestTokenValueProvider(new Dictionary<string, DocumentTokenValue>
        {
            ["client.name"] = DocumentTokenValue.Resolved("client.name", "ACME Ltd.")
        });

        var preview = await new DocumentTemplatePreviewService(provider).CreatePreviewAsync(
            document,
            new DocumentTokenResolutionContext { DocumentId = "doc-1" });

        GetParagraphText(preview).Should().Be("Dear ACME Ltd., ref missing case");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines.Should().Contain(inline => inline is TokenRun);
        GetParagraphText(document).Should().Be("Dear Client name, ref Case number");
    }

    [Fact]
    public void DocumentTokenHelper_CreatesRunFromExistingTokenProviderToken()
    {
        var token = DocumentTokenHelper.FromToken(new TestToken(
            "client.name",
            "Client name",
            "Client full name",
            "Client",
            "Text"));

        token.Key.Should().Be("client.name");
        token.DisplayName.Should().Be("Client name");
        token.Description.Should().Be("Client full name");
        token.TypeLabel.Should().Be("Text");
        token.TokenType.Should().Be("text");
    }

    private static string GetParagraphText(DocumentEditorDocument document)
    {
        return string.Concat(((ParagraphBlockContent)document.Blocks[0].Content).Inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => token.DisplayName,
            _ => string.Empty
        }));
    }

    private sealed class TestTokenValueProvider : IDocumentTokenValueProvider
    {
        private readonly IReadOnlyDictionary<string, DocumentTokenValue> _values;

        public TestTokenValueProvider(IReadOnlyDictionary<string, DocumentTokenValue> values)
        {
            _values = values;
        }

        public Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
            DocumentTokenResolutionContext context,
            IReadOnlyList<TokenRun> tokens,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values);
        }
    }

    private sealed record TestToken(
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string? TypeLabel) : IToken
    {
        public string? Icon => null;

        public string? ColorClass => null;
    }
}
