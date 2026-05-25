using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.Wysiwyg.Model;

public class DocumentModelTests
{
    [Fact]
    public void DocumentModel_Created_HasEmptyBody()
    {
        var model = new DocumentModel();

        model.Body.Should().BeEmpty();
    }

    [Fact]
    public void DocumentModel_Created_HasMetadata()
    {
        var model = new DocumentModel();

        model.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void DocumentModel_Created_HasPageSettings()
    {
        var model = new DocumentModel();

        model.PageSettings.Should().NotBeNull();
    }

    [Fact]
    public void DocumentModel_Created_HasEmptySections()
    {
        var model = new DocumentModel();

        model.Sections.Should().BeEmpty();
    }

    [Fact]
    public void DocumentModel_Created_HasEmptyHeadersFooters()
    {
        var model = new DocumentModel();

        model.HeadersFooters.Should().BeEmpty();
    }

    [Fact]
    public void DocumentModel_Created_HasEmptyNotes()
    {
        var model = new DocumentModel();

        model.Notes.Should().BeEmpty();
    }

    [Fact]
    public void DocumentModel_Created_HasEmptyComments()
    {
        var model = new DocumentModel();

        model.Comments.Should().BeEmpty();
    }

    [Fact]
    public void DocumentModel_Created_HasEmptyRevisions()
    {
        var model = new DocumentModel();

        model.Revisions.Should().BeEmpty();
    }

    [Fact]
    public void DocumentModel_HasGeneratedId()
    {
        var model = new DocumentModel();

        model.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DocumentModel_CanAddParagraphBlockToBody()
    {
        var model = new DocumentModel();
        var paragraph = new ParagraphBlock();

        model.Body.Add(paragraph);

        model.Body.Should().ContainSingle()
            .Which.Should().BeOfType<ParagraphBlock>();
    }

    [Fact]
    public void DocumentModel_CanAddHeadingBlockToBody()
    {
        var model = new DocumentModel();
        var heading = new HeadingBlock { Level = 2 };

        model.Body.Add(heading);

        model.Body.Should().ContainSingle()
            .Which.Should().BeOfType<HeadingBlock>()
            .Which.Level.Should().Be(2);
    }

    [Fact]
    public void DocumentModel_CanAddListItemBlockToBody()
    {
        var model = new DocumentModel();
        var listItem = new ListItemBlock { Ordered = true, IndentLevel = 1 };

        model.Body.Add(listItem);

        model.Body.Should().ContainSingle()
            .Which.Should().BeOfType<ListItemBlock>();
        var item = (ListItemBlock)model.Body[0];
        item.Ordered.Should().BeTrue();
        item.IndentLevel.Should().Be(1);
    }

    [Fact]
    public void DocumentModel_CanAddTableBlockToBody()
    {
        var model = new DocumentModel();
        var table = new TableBlock();

        model.Body.Add(table);

        model.Body.Should().ContainSingle()
            .Which.Should().BeOfType<TableBlock>();
    }

    [Fact]
    public void DocumentModel_CanAddImageBlockToBody()
    {
        var model = new DocumentModel();
        var image = new ImageBlock { Src = "image.png", Alt = "Test" };

        model.Body.Add(image);

        model.Body.Should().ContainSingle()
            .Which.Should().BeOfType<ImageBlock>();
    }

    [Fact]
    public void DocumentModel_CanAddPageBreakBlockToBody()
    {
        var model = new DocumentModel();
        var pageBreak = new PageBreakBlock();

        model.Body.Add(pageBreak);

        model.Body.Should().ContainSingle()
            .Which.Should().BeOfType<PageBreakBlock>();
    }

    [Fact]
    public void ParagraphBlock_HasTypeParagraph()
    {
        var block = new ParagraphBlock();

        block.Type.Should().Be("paragraph");
    }

    [Fact]
    public void ParagraphBlock_HasEmptyInlines()
    {
        var block = new ParagraphBlock();

        block.Inlines.Should().BeEmpty();
    }

    [Fact]
    public void ParagraphBlock_HasGeneratedId()
    {
        var block = new ParagraphBlock();

        block.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParagraphBlock_CanAddTextRun()
    {
        var block = new ParagraphBlock();
        var run = new TextRun { Text = "Hello" };

        block.Inlines.Add(run);

        block.Inlines.Should().ContainSingle()
            .Which.Should().BeOfType<TextRun>()
            .Which.Text.Should().Be("Hello");
    }

    [Fact]
    public void HeadingBlock_HasTypeHeading()
    {
        var block = new HeadingBlock();

        block.Type.Should().Be("heading");
    }

    [Fact]
    public void HeadingBlock_DefaultLevelIsOne()
    {
        var block = new HeadingBlock();

        block.Level.Should().Be(1);
    }

    [Fact]
    public void TableBlock_HasTypeTable()
    {
        var block = new TableBlock();

        block.Type.Should().Be("table");
    }

    [Fact]
    public void TableBlock_HasEmptyRows()
    {
        var block = new TableBlock();

        block.Rows.Should().BeEmpty();
    }

    [Fact]
    public void TableBlock_CanAddRowWithCells()
    {
        var block = new TableBlock();
        var row = new TableRow();
        var cell = new TableCell();
        cell.Blocks.Add(new ParagraphBlock());
        row.Cells.Add(cell);
        block.Rows.Add(row);

        block.Rows.Should().ContainSingle();
        block.Rows[0].Cells.Should().ContainSingle();
    }

    [Fact]
    public void TableCell_HasDefaultRowSpanAndColumnSpan()
    {
        var cell = new TableCell();

        cell.RowSpan.Should().Be(1);
        cell.ColumnSpan.Should().Be(1);
    }

    [Fact]
    public void TextRun_HasEmptyTextByDefault()
    {
        var run = new TextRun();

        run.Text.Should().BeEmpty();
    }

    [Fact]
    public void TextRun_HasEmptyMarks()
    {
        var run = new TextRun();

        run.Marks.Should().BeEmpty();
    }

    [Fact]
    public void TextRun_CanAddBoldMark()
    {
        var run = new TextRun { Text = "Bold" };
        run.Marks.Add(new BoldMark());

        run.Marks.Should().ContainSingle()
            .Which.Should().BeOfType<BoldMark>();
    }

    [Fact]
    public void TextRun_CanAddMultipleMarks()
    {
        var run = new TextRun { Text = "BoldItalic" };
        run.Marks.Add(new BoldMark());
        run.Marks.Add(new ItalicMark());

        run.Marks.Should().HaveCount(2);
        run.Marks.Should().Contain(m => m is BoldMark);
        run.Marks.Should().Contain(m => m is ItalicMark);
    }

    [Fact]
    public void ParagraphBlock_CanRepresentSplitRunsWithIndependentFormatting()
    {
        var block = new ParagraphBlock();
        block.Inlines.Add(new TextRun { Text = "Hello " });
        block.Inlines.Add(new TextRun { Text = "world", Marks = { new BoldMark() } });
        block.Inlines.Add(new TextRun { Text = "!" });

        block.Inlines.OfType<TextRun>().Select(run => run.Text)
            .Should()
            .Equal("Hello ", "world", "!");
        block.Inlines.OfType<TextRun>().ElementAt(1).Marks.Should().ContainSingle(mark => mark is BoldMark);
        block.Inlines.OfType<TextRun>().ElementAt(0).Marks.Should().BeEmpty();
        block.Inlines.OfType<TextRun>().ElementAt(2).Marks.Should().BeEmpty();
    }

    [Fact]
    public void ParagraphBlock_CanRepresentMergedRunAfterFormattingRemoval()
    {
        var block = new ParagraphBlock();
        block.Inlines.Add(new TextRun { Text = "Hello " });
        block.Inlines.Add(new TextRun { Text = "world" });

        var merged = string.Concat(block.Inlines.OfType<TextRun>().Select(run => run.Text));
        block.Inlines.Clear();
        block.Inlines.Add(new TextRun { Text = merged });

        block.Inlines.Should().ContainSingle()
            .Which.Should().BeOfType<TextRun>()
            .Which.Text.Should().Be("Hello world");
    }

    [Fact]
    public void BoldMark_HasTypeBold()
    {
        var mark = new BoldMark();

        mark.Type.Should().Be("bold");
    }

    [Fact]
    public void ItalicMark_HasTypeItalic()
    {
        var mark = new ItalicMark();

        mark.Type.Should().Be("italic");
    }

    [Fact]
    public void UnderlineMark_HasTypeUnderline()
    {
        var mark = new UnderlineMark();

        mark.Type.Should().Be("underline");
    }

    [Fact]
    public void StrikethroughMark_HasTypeStrikethrough()
    {
        var mark = new StrikethroughMark();

        mark.Type.Should().Be("strikethrough");
    }

    [Fact]
    public void SubscriptMark_HasTypeSubscript()
    {
        var mark = new SubscriptMark();

        mark.Type.Should().Be("subscript");
    }

    [Fact]
    public void SuperscriptMark_HasTypeSuperscript()
    {
        var mark = new SuperscriptMark();

        mark.Type.Should().Be("superscript");
    }

    [Fact]
    public void FontMark_HasTypeFont()
    {
        var mark = new FontMark();

        mark.Type.Should().Be("font");
    }

    [Fact]
    public void FontMark_HasDefaultFamilyAndSize()
    {
        var mark = new FontMark();

        mark.Family.Should().Be("Calibri");
        mark.Size.Should().Be("11pt");
    }

    [Fact]
    public void ColorMark_HasTypeColor()
    {
        var mark = new ColorMark();

        mark.Type.Should().Be("color");
    }

    [Fact]
    public void ColorMark_HasDefaultBlackColor()
    {
        var mark = new ColorMark();

        mark.Color.Should().Be("#000000");
    }

    [Fact]
    public void HighlightMark_HasTypeHighlight()
    {
        var mark = new HighlightMark();

        mark.Type.Should().Be("highlight");
    }

    [Fact]
    public void LinkMark_HasTypeLink()
    {
        var mark = new LinkMark();

        mark.Type.Should().Be("link");
    }

    [Fact]
    public void ImageBlock_HasTypeImage()
    {
        var block = new ImageBlock();

        block.Type.Should().Be("image");
    }

    [Fact]
    public void ImageBlock_HasDefaultInlineLayout()
    {
        var block = new ImageBlock();

        block.Layout.Should().Be(ImageLayout.Inline);
    }

    [Fact]
    public void PageBreakBlock_HasTypePageBreak()
    {
        var block = new PageBreakBlock();

        block.Type.Should().Be("pageBreak");
    }

    [Fact]
    public void HardBreak_IsInline()
    {
        var inline = new HardBreak();

        inline.Should().BeOfType<HardBreak>();
    }

    [Fact]
    public void DocumentNode_HasAttributesDictionary()
    {
        var node = new ParagraphBlock();

        node.Attributes.Should().NotBeNull();
    }

    [Fact]
    public void ParagraphBlock_HasParagraphProperties()
    {
        var block = new ParagraphBlock();

        block.Properties.Should().NotBeNull();
    }

    [Fact]
    public void ParagraphProperties_DefaultAlignmentIsLeft()
    {
        var props = new ParagraphProperties();

        props.Alignment.Should().Be(TextAlignment.Left);
    }

    [Fact]
    public void ParagraphProperties_DefaultLineSpacingIsOnePointFifteen()
    {
        var props = new ParagraphProperties();

        props.LineSpacing.Should().Be(1.15);
    }

    [Fact]
    public void PageSettings_DefaultIsA4()
    {
        var settings = PageSettings.DefaultA4();

        settings.Width.Should().Be("210mm");
        settings.Height.Should().Be("297mm");
    }

    [Fact]
    public void DocumentMetadata_HasTitleProperty()
    {
        var metadata = new DocumentMetadata { Title = "Contract" };

        metadata.Title.Should().Be("Contract");
    }
}
