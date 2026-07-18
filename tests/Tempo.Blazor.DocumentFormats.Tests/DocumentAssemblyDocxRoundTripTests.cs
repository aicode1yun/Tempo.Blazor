using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Assembly constructs must survive the DOCX round-trip: conditional block chains and repeating
/// sections ride the content control's Metadata dictionary, which the SDT mapping serializes into
/// the tm:content-control-json payload — export → import must reconstruct them losslessly.
/// </summary>
public class DocumentAssemblyDocxRoundTripTests
{
    [Fact]
    public async Task ConditionalChainAndRepeatingSection_SurviveDocxRoundTrip()
    {
        var template = DocumentEditorDocument.Empty();
        template.DocumentId = "assembly-roundtrip";
        template.Blocks =
        [
            ControlBlock("c-if", DocumentAssemblyMetadata.CreateConditionalBlock("if", "amount > 10000", "g1"),
                Paragraph("p-if", "Vysoká hodnota s eskalací.")),
            ControlBlock("c-else", DocumentAssemblyMetadata.CreateConditionalBlock("else", null, "g1"),
                Paragraph("p-else", "Standardní režim.")),
            ControlBlock("rep", DocumentAssemblyMetadata.CreateRepeatingSection("items"),
                Paragraph("rep-row", "Položka")),
        ];
        for (var i = 0; i < template.Blocks.Count; i++)
        {
            template.Blocks[i].Order = i + 1;
        }

        var exported = await new DocumentDocxExporter().ExportAsync(template);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content), new());

        var controls = imported.Document.Blocks
            .Select(block => block.Content)
            .OfType<ContentControlBlockContent>()
            .ToList();
        controls.Should().HaveCount(3, "all three assembly controls must survive the round-trip");

        var ifControl = controls.Single(control => DocumentAssemblyMetadata.GetBranch(control.Control) == "if");
        DocumentAssemblyMetadata.GetExpression(ifControl.Control).Should().Be("amount > 10000");
        DocumentAssemblyMetadata.GetGroup(ifControl.Control).Should().Be("g1");
        ifControl.Control.Alias.Should().Be("IF amount > 10000", "the visual label survives too");
        InnerText(ifControl).Should().Contain("Vysoká hodnota s eskalací.");

        var elseControl = controls.Single(control => DocumentAssemblyMetadata.GetBranch(control.Control) == "else");
        DocumentAssemblyMetadata.GetGroup(elseControl.Control).Should().Be("g1");
        InnerText(elseControl).Should().Contain("Standardní režim.");

        var repeating = controls.Single(control => control.Control.Kind == DocumentContentControlKind.RepeatingSection);
        DocumentAssemblyMetadata.GetBinding(repeating.Control).Should().Be("items");
        InnerText(repeating).Should().Contain("Položka");
    }

    private static DocumentBlock ControlBlock(string id, DocumentContentControl control, DocumentBlock inner)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.ContentControl,
            Content = new ContentControlBlockContent { Control = control, Blocks = [inner] },
        };

    private static DocumentBlock Paragraph(string id, string text)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] },
        };

    private static string InnerText(ContentControlBlockContent control)
        => string.Join(" ", control.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<TextRun>())
            .Select(run => run.Text));
}
