using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentModelRunSerializationTests
{
    [Fact]
    public void SerializeRoundTrip_PreservesFormattingRevisionAndCommentMarks()
    {
        var document = DocumentEditorDocument.Empty("run-roundtrip");
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "p1",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Id = "r1",
                            Text = "Styled",
                            Marks =
                            [
                                new InlineMark { Type = InlineMarkType.Bold },
                                new InlineMark { Type = InlineMarkType.FontSize, Value = "28pt" },
                                new InlineMark { Type = InlineMarkType.TextColor, Value = "#2563eb" }
                            ]
                        },
                        new TextRun
                        {
                            Id = "r2",
                            Text = " comment",
                            Marks =
                            [
                                new InlineMark
                                {
                                    Type = InlineMarkType.CommentAnchor,
                                    CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1", AnchorId = "anchor-1" }
                                },
                                new InlineMark
                                {
                                    Type = InlineMarkType.Revision,
                                    RevisionId = "revision-1",
                                    Value = DocumentRevisionType.Formatting.ToString()
                                }
                            ]
                        }
                    ]
                }
            }
        ];

        var roundTrip = DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(document));
        var runs = ((ParagraphBlockContent)roundTrip.Blocks.Single().Content).Inlines.OfType<TextRun>().ToList();

        runs.Select(run => run.Text).Should().Equal("Styled", " comment");
        runs[0].Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);
        runs[0].Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontSize && mark.Value == "28pt");
        runs[0].Marks.Should().Contain(mark => mark.Type == InlineMarkType.TextColor && mark.Value == "#2563eb");
        runs[1].Marks.Any(mark => mark.Type == InlineMarkType.CommentAnchor
            && mark.CommentAnchor != null
            && mark.CommentAnchor.CommentId == "comment-1").Should().BeTrue();
        runs[1].Marks.Should().Contain(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == "revision-1");
    }

    [Fact]
    public void Deserialize_LegacySnapshotWithRuns_NormalizesDocumentShell()
    {
        var json =
            """
            {
              "DocumentId": "legacy-runs",
              "SchemaVersion": 0,
              "Blocks": [
                {
                  "Id": "p1",
                  "Type": 0,
                  "Content": {
                    "$type": "paragraph",
                    "Inlines": [
                      { "$type": "text", "Id": "r1", "Text": "Legacy ", "Marks": [] },
                      { "$type": "text", "Id": "r2", "Text": "bold", "Marks": [ { "Type": 0 } ] }
                    ]
                  }
                }
              ]
            }
            """;

        var document = DocumentEditorJson.Deserialize(json);

        document.SchemaVersion.Should().Be(DocumentEditorDocument.CurrentSchemaVersion);
        document.Sections.Should().NotBeEmpty();
        var runs = ((ParagraphBlockContent)document.Blocks.Single().Content).Inlines.OfType<TextRun>().ToList();
        runs.Select(run => run.Text).Should().Equal("Legacy ", "bold");
        runs[1].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void Normalize_InvalidFutureSchema_RejectsSnapshot()
    {
        var json = $$"""
            { "DocumentId": "future", "SchemaVersion": {{DocumentEditorDocument.CurrentSchemaVersion + 1}}, "Blocks": [] }
            """;

        var act = () => DocumentEditorJson.Deserialize(json);

        act.Should().Throw<JsonException>();
    }
}
