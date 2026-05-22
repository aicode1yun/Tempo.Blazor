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
