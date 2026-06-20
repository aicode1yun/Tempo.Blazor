using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// The canonical converter must preserve signing field runs (plan S2.23/S2.24). Without this, a save /
/// reconcile cycle (canvas JSON -> DocumentEditorDocument -> canvas JSON) would silently drop inline
/// signing fields. Covers a field in the body and one in a header/footer.
/// </summary>
public class CanvasSigningFieldRoundTripTests
{
    private static DocumentEditorDocument DocumentWithSigningFields()
    {
        var document = DocumentEditorDocument.Empty("signing-round-trip");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "t1", Text = "Sign: " },
                    new DocumentSigningFieldRun { Id = "sf1", Uuid = "body-field", FieldType = "signature", SubmitterUuid = "signer", Required = true, Label = "Signature", BoxWidth = 180, BoxHeight = 44 },
                ],
            },
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "footer-1",
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "f1",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent { Inlines = [new DocumentSigningFieldRun { Id = "sf2", Uuid = "footer-field", FieldType = "initials", SubmitterUuid = "signer", Label = "Initials" }] },
                },
            ],
        });
        return document;
    }

    [Fact]
    public void ToCanvasModel_EmitsSigningFieldRunsForBodyAndHeaderFooter()
    {
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(DocumentWithSigningFields());

        var bodyRun = canvas.Body.Blocks[0].Content!.Runs.FirstOrDefault(run => run.Type == "signingField");
        bodyRun.Should().NotBeNull();
        bodyRun!.SigningField!.Uuid.Should().Be("body-field");
        bodyRun.SigningField.FieldType.Should().Be("signature");

        var footerRun = canvas.HeadersFooters[0].Blocks[0].Content!.Runs.FirstOrDefault(run => run.Type == "signingField");
        footerRun.Should().NotBeNull();
        footerRun!.SigningField!.Uuid.Should().Be("footer-field");
    }

    [Fact]
    public void RoundTrip_PreservesSigningFieldsInBodyAndHeaderFooter()
    {
        var roundTripped = CanvasDocumentModelConverter.FromCanvasModel(
            CanvasDocumentModelConverter.ToCanvasModel(DocumentWithSigningFields()));

        var body = roundTripped.Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>().Subject;
        var bodyField = body.Inlines.OfType<DocumentSigningFieldRun>().Single();
        bodyField.Uuid.Should().Be("body-field");
        bodyField.FieldType.Should().Be("signature");
        bodyField.SubmitterUuid.Should().Be("signer");
        bodyField.Required.Should().BeTrue();

        var footerContent = roundTripped.HeadersFooters[0].Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>().Subject;
        var footerField = footerContent.Inlines.OfType<DocumentSigningFieldRun>().Single();
        footerField.Uuid.Should().Be("footer-field");
        footerField.FieldType.Should().Be("initials");
    }
}
