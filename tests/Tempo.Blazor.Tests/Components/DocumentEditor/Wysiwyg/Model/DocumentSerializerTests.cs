using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Serialization;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

using Wyg = Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;
using Persistence = Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.Wysiwyg.Model;

public class DocumentSerializerTests
{
    private readonly DocumentSerializer _serializer = new();

    [Fact]
    public void Serialize_EmptyDocumentModel_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();

        var result = _serializer.ToPersistenceModel(model);

        result.Should().NotBeNull();
        result.DocumentId.Should().Be(model.Id);
        result.Metadata.Should().NotBeNull();
        result.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_ParagraphWithTextRun_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();
        paragraph.Inlines.Add(new Wyg.TextRun { Text = "Hello world" });
        model.Body.Add(paragraph);

        var result = _serializer.ToPersistenceModel(model);

        result.Blocks.Should().ContainSingle();
        result.Blocks[0].Type.Should().Be(DocumentBlockType.Paragraph);
        result.Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>();
        var content = (ParagraphBlockContent)result.Blocks[0].Content;
        content.Inlines.Should().ContainSingle()
            .Which.Should().BeOfType<Persistence.TextRun>()
            .Which.Text.Should().Be("Hello world");
    }

    [Fact]
    public void Serialize_BoldTextRun_ToDocumentEditorDocumentWithBoldMark()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "Bold" };
        run.Marks.Add(new BoldMark());
        paragraph.Inlines.Add(run);
        model.Body.Add(paragraph);

        var result = _serializer.ToPersistenceModel(model);

        var content = (ParagraphBlockContent)result.Blocks[0].Content;
        var textRun = content.Inlines.OfType<Persistence.TextRun>().Single();
        textRun.Marks.Should().ContainSingle()
            .Which.Type.Should().Be(InlineMarkType.Bold);
    }

    [Fact]
    public void Serialize_MultipleMarks_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "BoldItalic" };
        run.Marks.Add(new BoldMark());
        run.Marks.Add(new ItalicMark());
        paragraph.Inlines.Add(run);
        model.Body.Add(paragraph);

        var result = _serializer.ToPersistenceModel(model);

        var content = (ParagraphBlockContent)result.Blocks[0].Content;
        var textRun = content.Inlines.OfType<Persistence.TextRun>().Single();
        textRun.Marks.Should().HaveCount(2);
        textRun.Marks.Should().Contain(m => m.Type == InlineMarkType.Bold);
        textRun.Marks.Should().Contain(m => m.Type == InlineMarkType.Italic);
    }

    [Fact]
    public void Serialize_HeadingBlock_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var heading = new HeadingBlock { Level = 2 };
        heading.Inlines.Add(new Wyg.TextRun { Text = "Title" });
        model.Body.Add(heading);

        var result = _serializer.ToPersistenceModel(model);

        result.Blocks.Should().ContainSingle();
        result.Blocks[0].Type.Should().Be(DocumentBlockType.Heading);
        var content = (HeadingBlockContent)result.Blocks[0].Content;
        content.Level.Should().Be(2);
    }

    [Fact]
    public void Serialize_ListItemBlock_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var listItem = new ListItemBlock { Ordered = true, IndentLevel = 1 };
        listItem.Inlines.Add(new Wyg.TextRun { Text = "Item" });
        model.Body.Add(listItem);

        var result = _serializer.ToPersistenceModel(model);

        result.Blocks[0].Type.Should().Be(DocumentBlockType.List);
        var content = (ListBlockContent)result.Blocks[0].Content;
        content.Ordered.Should().BeTrue();
        content.IndentLevel.Should().Be(1);
    }

    [Fact]
    public void Serialize_TableBlock_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var table = new TableBlock();
        var row = new TableRow();
        var cell = new TableCell();
        cell.Blocks.Add(new ParagraphBlock());
        row.Cells.Add(cell);
        table.Rows.Add(row);
        model.Body.Add(table);

        var result = _serializer.ToPersistenceModel(model);

        result.Blocks[0].Type.Should().Be(DocumentBlockType.Table);
        var content = (TableBlockContent)result.Blocks[0].Content;
        content.Rows.Should().ContainSingle();
        content.Rows[0].Cells.Should().ContainSingle();
    }

    [Fact]
    public void Serialize_ImageBlock_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var image = new ImageBlock
        {
            Src = "image.png",
            Alt = "Test image",
            Layout = ImageLayout.Inline
        };
        model.Body.Add(image);

        var result = _serializer.ToPersistenceModel(model);

        result.Blocks[0].Type.Should().Be(DocumentBlockType.Image);
        var content = (ImageBlockContent)result.Blocks[0].Content;
        content.Url.Should().Be("image.png");
        content.AltText.Should().Be("Test image");
    }

    [Fact]
    public void Serialize_FloatingImageBlock_ToDocumentObjectLayout()
    {
        var model = new DocumentModel();
        model.Body.Add(new ImageBlock
        {
            Src = "image.png",
            Alt = "Floating image",
            Layout = ImageLayout.Floating,
            Position = new ImagePosition { X = "24", Y = "36" },
            WrapMode = ImageWrapMode.BehindText,
            Size = new Wyg.ImageSize { Width = "220", Height = "124" }
        });

        var result = _serializer.ToPersistenceModel(model);

        var content = result.Blocks[0].Content.Should().BeOfType<ImageBlockContent>().Subject;
        content.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Anchored);
        content.Layout.Position.X.Should().Be(24);
        content.Layout.Position.Y.Should().Be(36);
        content.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.BehindText);
        content.Layout.Transform.Width.Should().Be(220);
        content.Layout.Transform.Height.Should().Be(124);
    }

    [Fact]
    public void Deserialize_ImageBlockWithObjectLayout_ToFloatingWysiwygImageBlock()
    {
        var persistence = new DocumentEditorDocument();
        persistence.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = "image.png",
                AltText = "Front image",
                Layout = new DocumentObjectLayout
                {
                    Kind = DocumentObjectLayoutKind.Fixed,
                    Anchor = new DocumentObjectAnchor
                    {
                        MoveWithText = false,
                        FixedOnPage = true,
                        LockAnchor = true
                    },
                    Position = new DocumentObjectPosition
                    {
                        X = 14,
                        Y = 22
                    },
                    Wrap = new DocumentObjectWrap
                    {
                        Mode = DocumentWrapMode.InFrontOfText
                    },
                    Transform = new DocumentObjectTransform
                    {
                        Width = 180,
                        Height = 95
                    }
                }
            }
        });

        var result = _serializer.FromPersistenceModel(persistence);

        var image = result.Body.Should().ContainSingle().Subject.Should().BeOfType<ImageBlock>().Subject;
        image.Layout.Should().Be(ImageLayout.Floating);
        image.Position.Should().NotBeNull();
        image.Position!.X.Should().Be("14");
        image.Position.Y.Should().Be("22");
        image.WrapMode.Should().Be(ImageWrapMode.InFrontOfText);
        image.Size.Width.Should().Be("180");
        image.Size.Height.Should().Be("95");
    }

    [Fact]
    public void Serialize_PageBreakBlock_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        model.Body.Add(new PageBreakBlock());

        var result = _serializer.ToPersistenceModel(model);

        result.Blocks[0].Type.Should().Be(DocumentBlockType.PageBreak);
    }

    [Fact]
    public void Serialize_Metadata_ToDocumentEditorDocument()
    {
        var model = new DocumentModel
        {
            Metadata = { Title = "Contract", AuthorName = "John Doe" }
        };

        var result = _serializer.ToPersistenceModel(model);

        result.Metadata.Title.Should().Be("Contract");
    }

    [Fact]
    public void Serialize_PageSettings_ToDocumentEditorDocument()
    {
        var model = new DocumentModel
        {
            PageSettings = { Width = "210mm", Height = "297mm", MarginTop = "25mm" }
        };

        var result = _serializer.ToPersistenceModel(model);

        result.PageSettings.Should().NotBeNull();
    }

    [Fact]
    public void Serialize_HeaderFooter_ToDocumentEditorDocument()
    {
        var model = new DocumentModel();
        var header = new HeaderFooter
        {
            Type = HeaderFooterType.Header,
            Scope = HeaderFooterScope.Primary
        };
        var headerPara = new ParagraphBlock();
        headerPara.Inlines.Add(new Wyg.TextRun { Text = "Header text" });
        header.Blocks.Add(headerPara);
        model.HeadersFooters.Add(header);

        var result = _serializer.ToPersistenceModel(model);

        result.HeadersFooters.Should().ContainSingle();
        result.HeadersFooters[0].Type.Should().Be(DocumentHeaderFooterType.Header);
    }

    [Fact]
    public void Deserialize_EmptyDocumentEditorDocument_ToDocumentModel()
    {
        var persistence = new DocumentEditorDocument { DocumentId = "doc-1" };

        var result = _serializer.FromPersistenceModel(persistence);

        result.Should().NotBeNull();
        result.Id.Should().Be("doc-1");
        result.Body.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_ParagraphWithTextRun_ToDocumentModel()
    {
        var persistence = new DocumentEditorDocument();
        persistence.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new Persistence.TextRun { Text = "Hello" }]
            }
        });

        var result = _serializer.FromPersistenceModel(persistence);

        result.Body.Should().ContainSingle().Which.Should().BeOfType<ParagraphBlock>();
        var paragraph = (ParagraphBlock)result.Body[0];
        paragraph.Inlines.Should().ContainSingle().Which.Should().BeOfType<Wyg.TextRun>()
            .Which.Text.Should().Be("Hello");
    }

    [Fact]
    public void RoundTrip_ModelToPersistenceToModel_PreservesData()
    {
        var original = new DocumentModel();
        original.Metadata.Title = "Test Document";
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "Bold text" };
        run.Marks.Add(new BoldMark());
        paragraph.Inlines.Add(run);
        original.Body.Add(paragraph);
        original.Body.Add(new HeadingBlock { Level = 2 });

        var persistence = _serializer.ToPersistenceModel(original);
        var roundTrip = _serializer.FromPersistenceModel(persistence);

        roundTrip.Metadata.Title.Should().Be("Test Document");
        roundTrip.Body.Should().HaveCount(2);
        roundTrip.Body[0].Should().BeOfType<ParagraphBlock>();
        var rtParagraph = (ParagraphBlock)roundTrip.Body[0];
        rtParagraph.Inlines[0].Should().BeOfType<Wyg.TextRun>();
        var rtRun = (Wyg.TextRun)rtParagraph.Inlines[0];
        rtRun.Text.Should().Be("Bold text");
        rtRun.Marks.Should().Contain(m => m is BoldMark);
        roundTrip.Body[1].Should().BeOfType<HeadingBlock>()
            .Which.Level.Should().Be(2);
    }

    [Fact]
    public void RoundTrip_ModelToPersistenceToModel_PreservesFormattingCommentsAndRevisions()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-24T08:00:00Z");
        var original = new DocumentModel();
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "Reviewed text" };
        run.Marks.Add(new BoldMark());
        run.Marks.Add(new ItalicMark());
        run.Marks.Add(new FontMark { Family = "Georgia, serif", Size = "28pt" });
        run.Marks.Add(new ColorMark { Color = "#2563eb" });
        run.Marks.Add(new HighlightMark { Color = "#fef08a" });
        run.Marks.Add(new LinkMark { Href = "https://example.com", Title = "Example" });
        paragraph.Inlines.Add(run);
        original.Body.Add(paragraph);
        original.Comments.Add(new Wyg.DocumentComment
        {
            Id = "comment-1",
            IsResolved = true,
            Anchor = new Wyg.DocumentCommentAnchor
            {
                StartBlockId = paragraph.Id,
                StartInlineIndex = 0,
                StartTextOffset = 0,
                EndBlockId = paragraph.Id,
                EndInlineIndex = 0,
                EndTextOffset = 8
            },
            Entries =
            {
                new Wyg.DocumentCommentEntry
                {
                    Id = "entry-1",
                    AuthorId = "author-1",
                    AuthorName = "Reviewer",
                    Text = "Looks good",
                    CreatedAt = createdAt
                }
            }
        });
        original.Revisions.Add(new Wyg.DocumentRevision
        {
            Id = "revision-1",
            Type = Wyg.DocumentRevisionType.Formatting,
            AuthorId = "author-1",
            AuthorName = "Reviewer",
            CreatedAt = createdAt,
            Action = Wyg.DocumentRevisionAction.Pending
        });

        var persistence = _serializer.ToPersistenceModel(original);
        var roundTrip = _serializer.FromPersistenceModel(persistence);

        var rtRun = ((ParagraphBlock)roundTrip.Body.Single()).Inlines.OfType<Wyg.TextRun>().Single();
        rtRun.Text.Should().Be("Reviewed text");
        rtRun.Marks.Should().Contain(mark => mark is BoldMark);
        rtRun.Marks.Should().Contain(mark => mark is ItalicMark);
        rtRun.Marks.OfType<FontMark>().Should().Contain(mark => mark.Family == "Georgia, serif");
        rtRun.Marks.OfType<FontMark>().Should().Contain(mark => mark.Size == "28pt");
        rtRun.Marks.OfType<ColorMark>().Should().ContainSingle(mark => mark.Color == "#2563eb");
        rtRun.Marks.OfType<HighlightMark>().Should().ContainSingle(mark => mark.Color == "#fef08a");
        rtRun.Marks.OfType<LinkMark>().Should().ContainSingle(mark =>
            mark.Href == "https://example.com" && mark.Title == "Example");

        var comment = roundTrip.Comments.Should().ContainSingle().Subject;
        comment.Id.Should().Be("comment-1");
        comment.IsResolved.Should().BeTrue();
        comment.Anchor.StartBlockId.Should().Be(paragraph.Id);
        comment.Anchor.StartTextOffset.Should().Be(0);
        comment.Anchor.EndTextOffset.Should().Be(8);
        var entry = comment.Entries.Should().ContainSingle().Subject;
        entry.Id.Should().Be("entry-1");
        entry.AuthorId.Should().Be("author-1");
        entry.AuthorName.Should().Be("Reviewer");
        entry.CreatedAt.Should().Be(createdAt);

        var revision = roundTrip.Revisions.Should().ContainSingle().Subject;
        revision.Id.Should().Be("revision-1");
        revision.Type.Should().Be(Wyg.DocumentRevisionType.Formatting);
        revision.AuthorId.Should().Be("author-1");
        revision.AuthorName.Should().Be("Reviewer");
        revision.CreatedAt.Should().Be(createdAt);
        revision.Action.Should().Be(Wyg.DocumentRevisionAction.Pending);
    }

    [Fact]
    public void RoundTrip_ModelToPersistenceToModel_PreservesStableTextPropertiesAndRevisionMetadata()
    {
        var createdAt = DateTimeOffset.Parse("2026-05-24T09:30:00Z");
        var original = new DocumentModel { Id = "phase16-serializer-doc" };
        var paragraph = new ParagraphBlock
        {
            Id = "phase16-block",
            Properties = new Wyg.ParagraphProperties
            {
                Alignment = Wyg.TextAlignment.Justify,
                LineSpacing = 1.5,
                SpaceBefore = "6pt",
                SpaceAfter = "12pt",
                LeftIndent = "18pt",
                RightIndent = "9pt",
                FirstLineIndent = "24pt"
            }
        };
        paragraph.Inlines.Add(new Wyg.TextRun
        {
            Id = "phase16-run-a",
            Text = "First ",
            Marks = { new BoldMark() }
        });
        paragraph.Inlines.Add(new Wyg.TextRun
        {
            Id = "phase16-run-b",
            Text = "second",
            Marks =
            {
                new FontMark { Family = "Aptos", Size = "16pt" },
                new ColorMark { Color = "#111827" },
                new HighlightMark { Color = "#fef08a" }
            }
        });
        original.Body.Add(paragraph);
        original.Comments.Add(new Wyg.DocumentComment
        {
            Id = "phase16-comment",
            Anchor = new Wyg.DocumentCommentAnchor
            {
                StartBlockId = "phase16-block",
                StartInlineIndex = 1,
                StartTextOffset = 0,
                EndBlockId = "phase16-block",
                EndInlineIndex = 1,
                EndTextOffset = 6
            }
        });
        original.Revisions.Add(new Wyg.DocumentRevision
        {
            Id = "phase16-revision",
            Type = Wyg.DocumentRevisionType.Deletion,
            AuthorId = "reviewer",
            AuthorName = "Reviewer",
            CreatedAt = createdAt,
            Action = Wyg.DocumentRevisionAction.Pending,
            GroupId = "phase16-group",
            PayloadJson = """{"text":"second"}""",
            Range = new Wyg.DocumentRevisionRange
            {
                BlockId = "phase16-block",
                StartInlineIndex = 1,
                StartOffset = 0,
                EndInlineIndex = 1,
                EndOffset = 6
            }
        });

        var persistence = _serializer.ToPersistenceModel(original);
        var roundTrip = _serializer.FromPersistenceModel(persistence);

        var persistedBlock = persistence.Blocks.Should().ContainSingle().Subject;
        persistedBlock.Id.Should().Be("phase16-block");
        persistedBlock.ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Justify);
        persistedBlock.ParagraphProperties.LineSpacing.Should().Be(1.5);
        persistedBlock.ParagraphProperties.SpacingBefore.Should().Be(6);
        persistedBlock.ParagraphProperties.SpacingAfter.Should().Be(12);
        persistedBlock.ParagraphProperties.LeftIndent.Should().Be(18);
        persistedBlock.ParagraphProperties.RightIndent.Should().Be(9);
        persistedBlock.ParagraphProperties.FirstLineIndent.Should().Be(24);
        var persistedRuns = ((ParagraphBlockContent)persistedBlock.Content).Inlines.OfType<Persistence.TextRun>().ToList();
        persistedRuns.Should().HaveCount(2);
        persistedRuns[0].Id.Should().Be("phase16-run-a");
        persistedRuns[1].Id.Should().Be("phase16-run-b");
        var persistedRevision = persistence.Revisions.Should().ContainSingle().Subject;
        persistedRevision.GroupId.Should().Be("phase16-group");
        persistedRevision.PayloadJson.Should().Be("""{"text":"second"}""");
        persistedRevision.Range.BlockId.Should().Be("phase16-block");
        persistedRevision.Range.StartInlineIndex.Should().Be(1);
        persistedRevision.Range.EndOffset.Should().Be(6);

        var roundTripParagraph = roundTrip.Body.Should().ContainSingle().Subject.Should().BeOfType<ParagraphBlock>().Subject;
        roundTripParagraph.Id.Should().Be("phase16-block");
        roundTripParagraph.Properties!.Alignment.Should().Be(Wyg.TextAlignment.Justify);
        roundTripParagraph.Properties.LineSpacing.Should().Be(1.5);
        roundTripParagraph.Properties.SpaceBefore.Should().Be("6pt");
        roundTripParagraph.Properties.SpaceAfter.Should().Be("12pt");
        roundTripParagraph.Properties.LeftIndent.Should().Be("18pt");
        roundTripParagraph.Properties.RightIndent.Should().Be("9pt");
        roundTripParagraph.Properties.FirstLineIndent.Should().Be("24pt");
        roundTripParagraph.Inlines.OfType<Wyg.TextRun>().Select(run => run.Id)
            .Should().Equal("phase16-run-a", "phase16-run-b");
        roundTrip.Comments.Should().ContainSingle().Subject.Anchor.StartInlineIndex.Should().Be(1);
        var roundTripRevision = roundTrip.Revisions.Should().ContainSingle().Subject;
        roundTripRevision.GroupId.Should().Be("phase16-group");
        roundTripRevision.PayloadJson.Should().Be("""{"text":"second"}""");
        roundTripRevision.Range.BlockId.Should().Be("phase16-block");
        roundTripRevision.Range.StartInlineIndex.Should().Be(1);
        roundTripRevision.Range.EndOffset.Should().Be(6);
        roundTripRevision.AuthorId.Should().Be("reviewer");
        roundTripRevision.AuthorName.Should().Be("Reviewer");
        roundTripRevision.CreatedAt.Should().Be(createdAt);
        roundTripRevision.Type.Should().Be(Wyg.DocumentRevisionType.Deletion);
    }

    [Fact]
    public void Deserialize_OldJsonWithoutNewProperties_DoesNotThrow()
    {
        var persistence = new DocumentEditorDocument();
        persistence.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new Persistence.TextRun { Text = "Legacy text" }]
            }
        });

        var act = () => _serializer.FromPersistenceModel(persistence);

        act.Should().NotThrow();
    }

    [Fact]
    public void Serialize_StrikethroughMark_ToPersistenceModel()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "Deleted" };
        run.Marks.Add(new StrikethroughMark());
        paragraph.Inlines.Add(run);
        model.Body.Add(paragraph);

        var result = _serializer.ToPersistenceModel(model);

        var content = (ParagraphBlockContent)result.Blocks[0].Content;
        var textRun = content.Inlines.OfType<Persistence.TextRun>().Single();
        textRun.Marks.Should().Contain(m => m.Type == InlineMarkType.Strikethrough);
    }

    [Fact]
    public void Serialize_LinkMark_ToPersistenceModel()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "Click here" };
        run.Marks.Add(new LinkMark { Href = "https://example.com" });
        paragraph.Inlines.Add(run);
        model.Body.Add(paragraph);

        var result = _serializer.ToPersistenceModel(model);

        var content = (ParagraphBlockContent)result.Blocks[0].Content;
        var textRun = content.Inlines.OfType<Persistence.TextRun>().Single();
        var linkMark = textRun.Marks.First(m => m.Type == InlineMarkType.Link);
        linkMark.Link!.Href.Should().Be("https://example.com");
    }

    [Fact]
    public void Serialize_FontAndColorMarks_ToPersistenceModel()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();
        var run = new Wyg.TextRun { Text = "Styled" };
        run.Marks.Add(new FontMark { Family = "Georgia, serif", Size = "24pt" });
        run.Marks.Add(new ColorMark { Color = "#123456" });
        run.Marks.Add(new HighlightMark { Color = "#fff59d" });
        paragraph.Inlines.Add(run);
        model.Body.Add(paragraph);

        var result = _serializer.ToPersistenceModel(model);

        var content = (ParagraphBlockContent)result.Blocks[0].Content;
        var textRun = content.Inlines.OfType<Persistence.TextRun>().Single();
        textRun.Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontFamily && mark.Value == "Georgia, serif");
        textRun.Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontSize && mark.Value == "24pt");
        textRun.Marks.Should().Contain(mark => mark.Type == InlineMarkType.TextColor && mark.Value == "#123456");
        textRun.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Highlight && mark.Value == "#fff59d");
    }
}
