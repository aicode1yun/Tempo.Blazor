using System.Linq;
using System.Text.Json;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// R.4.8 cutover bridge — the C# ↔ JS core-engine model converter round-trips the supported
/// document subset (paragraphs, headings, text runs, common marks, alignment).
/// </summary>
public class CoreEngineModelConverterTests
{
    private static JsonElement ToCoreJson(DocumentEditorDocument doc)
    {
        var model = CoreEngineModelConverter.ToCoreModel(doc);
        var json = JsonSerializer.Serialize(model);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void ToCoreModel_ProducesTheEngineModelShape()
    {
        var doc = new DocumentEditorDocument { DocumentId = "doc-1" };
        doc.Blocks =
        [
            new DocumentBlock
            {
                Id = "h",
                Type = DocumentBlockType.Heading,
                Content = new HeadingBlockContent { Level = 2, Inlines = [new TextRun { Id = "h-r", Text = "Title" }] },
            },
            new DocumentBlock
            {
                Id = "p",
                Type = DocumentBlockType.Paragraph,
                ParagraphProperties = new DocumentParagraphProperties { Alignment = DocumentTextAlignment.Center },
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Id = "p-r", Text = "Hello", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
                    ],
                },
            },
        ];

        var core = ToCoreJson(doc);
        Assert.Equal("doc-1", core.GetProperty("documentId").GetString());
        var blocks = core.GetProperty("body").GetProperty("blocks");
        Assert.Equal(2, blocks.GetArrayLength());

        var heading = blocks[0];
        Assert.Equal("paragraph", heading.GetProperty("type").GetString());
        Assert.Equal(2, heading.GetProperty("content").GetProperty("headingLevel").GetInt32());
        Assert.Equal("Heading2", heading.GetProperty("content").GetProperty("styleName").GetString());
        Assert.Equal("Title", heading.GetProperty("content").GetProperty("runs")[0].GetProperty("text").GetString());

        var para = blocks[1];
        Assert.Equal("center", para.GetProperty("content").GetProperty("alignment").GetString());
        var run = para.GetProperty("content").GetProperty("runs")[0];
        Assert.Equal("Hello", run.GetProperty("text").GetString());
        Assert.Equal("bold", run.GetProperty("marks")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void RoundTrip_PreservesTextHeadingsMarksAndAlignment()
    {
        var doc = new DocumentEditorDocument { DocumentId = "rt" };
        doc.Blocks =
        [
            new DocumentBlock { Id = "h", Type = DocumentBlockType.Heading, Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Chapter" }] } },
            new DocumentBlock
            {
                Id = "p",
                Type = DocumentBlockType.Paragraph,
                ParagraphProperties = new DocumentParagraphProperties { Alignment = DocumentTextAlignment.Right },
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "bold", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
                        new TextRun { Text = "link", Marks = [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = "https://x.dev" } }] },
                        new TextRun { Text = "hi", Marks = [new InlineMark { Type = InlineMarkType.Highlight, Value = "#ff0" }] },
                    ],
                },
            },
        ];

        var rebuilt = CoreEngineModelConverter.FromCoreModel(ToCoreJson(doc));

        Assert.Equal("rt", rebuilt.DocumentId);
        Assert.Equal(2, rebuilt.Blocks.Count);

        var h = rebuilt.Blocks[0];
        Assert.Equal(DocumentBlockType.Heading, h.Type);
        Assert.Equal(1, Assert.IsType<HeadingBlockContent>(h.Content).Level);
        Assert.Equal("Chapter", ((TextRun)((HeadingBlockContent)h.Content).Inlines[0]).Text);

        var p = rebuilt.Blocks[1];
        Assert.Equal(DocumentTextAlignment.Right, p.ParagraphProperties.Alignment);
        var inlines = ((ParagraphBlockContent)p.Content).Inlines.Cast<TextRun>().ToList();
        Assert.Equal(new[] { "bold", "link", "hi" }, inlines.Select(r => r.Text));
        Assert.Equal(InlineMarkType.Bold, inlines[0].Marks[0].Type);
        Assert.Equal(InlineMarkType.Link, inlines[1].Marks[0].Type);
        Assert.Equal("https://x.dev", inlines[1].Marks[0].Link!.Href);
        Assert.Equal(InlineMarkType.Highlight, inlines[2].Marks[0].Type);
        Assert.Equal("#ff0", inlines[2].Marks[0].Value);
    }

    // ===== R.5.1 — full round-trip (no data loss on save) ================================

    private static DocumentEditorDocument RoundTrip(DocumentEditorDocument doc) =>
        CoreEngineModelConverter.FromCoreModel(ToCoreJson(doc));

    private static DocumentBlock Para(string text) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] },
    };

    private static string CellText(TableCellContent cell) =>
        ((TextRun)((ParagraphBlockContent)cell.Blocks[0].Content).Inlines[0]).Text;

    [Fact]
    public void RoundTrip_PreservesTable_StructureSpansHeaderAndCellText()
    {
        var table = new TableBlockContent { Layout = { Alignment = TableHorizontalAlignment.Center, Width = 480 } };
        var row0 = new TableRowContent();
        row0.Cells.Add(new TableCellContent { Id = "c00", IsHeader = true, Blocks = [Para("A1")] });
        row0.Cells.Add(new TableCellContent { Id = "c01", ColumnSpan = 2, Blocks = [Para("A2")] });
        table.Rows.Add(row0);
        var row1 = new TableRowContent();
        row1.Cells.Add(new TableCellContent { Id = "c10", Blocks = [Para("B1")] });
        row1.Cells.Add(new TableCellContent { Id = "c11", RowSpan = 2, Width = 120, Blocks = [Para("B2")] });
        table.Rows.Add(row1);

        var doc = new DocumentEditorDocument { DocumentId = "tbl" };
        doc.Blocks = [new DocumentBlock { Id = "t", Type = DocumentBlockType.Table, Content = table }];

        var block = Assert.Single(RoundTrip(doc).Blocks);
        Assert.Equal(DocumentBlockType.Table, block.Type);
        var rebuilt = Assert.IsType<TableBlockContent>(block.Content);
        Assert.Equal(2, rebuilt.Rows.Count);
        Assert.Equal(2, rebuilt.Rows[0].Cells.Count);
        Assert.True(rebuilt.Rows[0].Cells[0].IsHeader);
        Assert.Equal(2, rebuilt.Rows[0].Cells[1].ColumnSpan);
        Assert.Equal(2, rebuilt.Rows[1].Cells[1].RowSpan);
        Assert.Equal(120, rebuilt.Rows[1].Cells[1].Width);
        Assert.Equal("A1", CellText(rebuilt.Rows[0].Cells[0]));
        Assert.Equal("A2", CellText(rebuilt.Rows[0].Cells[1]));
        Assert.Equal("B1", CellText(rebuilt.Rows[1].Cells[0]));
        Assert.Equal("B2", CellText(rebuilt.Rows[1].Cells[1]));
        Assert.Equal(TableHorizontalAlignment.Center, rebuilt.Layout.Alignment);
        Assert.Equal(480, rebuilt.Layout.Width);
    }

    [Fact]
    public void RoundTrip_PreservesStandaloneImage_VisibleFieldsAndPreservedMetadata()
    {
        var image = new ImageBlockContent
        {
            Source = DocumentImageSource.Asset,
            Url = "pic.png",
            AssetId = "asset-1",
            AltText = "alt text",
            Caption = "a caption",
            LinkUrl = "https://link.example",
            Layout = new DocumentObjectLayout
            {
                Wrap = { Mode = DocumentWrapMode.Square },
                Transform = { Width = 200, Height = 150 },
                Position = { X = 50, Y = 30 },
                Stacking = { ZIndex = 7 },
            },
        };
        var doc = new DocumentEditorDocument { DocumentId = "img" };
        doc.Blocks = [new DocumentBlock { Id = "i", Type = DocumentBlockType.Image, Content = image }];

        var block = Assert.Single(RoundTrip(doc).Blocks);
        Assert.Equal(DocumentBlockType.Image, block.Type);
        var rebuilt = Assert.IsType<ImageBlockContent>(block.Content);
        // Engine-managed visible fields (overlaid from the drawing run).
        Assert.Equal("pic.png", rebuilt.Url);
        Assert.Equal("alt text", rebuilt.AltText);
        Assert.Equal("a caption", rebuilt.Caption);
        Assert.Equal(DocumentWrapMode.Square, rebuilt.Layout.Wrap.Mode);
        Assert.Equal(200, rebuilt.Layout.Transform.Width);
        Assert.Equal(150, rebuilt.Layout.Transform.Height);
        Assert.Equal(50, rebuilt.Layout.Position.X);
        Assert.Equal(30, rebuilt.Layout.Position.Y);
        Assert.Equal(7, rebuilt.Layout.Stacking.ZIndex);
        // Preserve channel — metadata the engine never sees.
        Assert.Equal(DocumentImageSource.Asset, rebuilt.Source);
        Assert.Equal("asset-1", rebuilt.AssetId);
        Assert.Equal("https://link.example", rebuilt.LinkUrl);
    }

    [Fact]
    public void RoundTrip_PreservesPageBreak()
    {
        var doc = new DocumentEditorDocument { DocumentId = "pb" };
        doc.Blocks =
        [
            Para("before"),
            new DocumentBlock { Id = "brk", Type = DocumentBlockType.PageBreak, Content = new PageBreakBlockContent { NextSectionId = "sec-2" } },
            Para("after"),
        ];

        var rebuilt = RoundTrip(doc).Blocks;
        Assert.Equal(3, rebuilt.Count);
        Assert.Equal(DocumentBlockType.PageBreak, rebuilt[1].Type);
        Assert.Equal("sec-2", Assert.IsType<PageBreakBlockContent>(rebuilt[1].Content).NextSectionId);
    }

    [Fact]
    public void RoundTrip_PreservesListsAndQuote()
    {
        var doc = new DocumentEditorDocument { DocumentId = "lst" };
        doc.Blocks =
        [
            new DocumentBlock { Id = "ol", Type = DocumentBlockType.List, Content = new ListBlockContent { Ordered = true, IndentLevel = 1, StartNumber = 3, Inlines = [new TextRun { Text = "ordered" }] } },
            new DocumentBlock { Id = "ul", Type = DocumentBlockType.List, Content = new ListBlockContent { Ordered = false, Inlines = [new TextRun { Text = "bullet" }] } },
            new DocumentBlock { Id = "q", Type = DocumentBlockType.Quote, Content = new QuoteBlockContent { Inlines = [new TextRun { Text = "quoted" }] } },
        ];

        var rebuilt = RoundTrip(doc).Blocks;

        var ol = Assert.IsType<ListBlockContent>(rebuilt[0].Content);
        Assert.True(ol.Ordered);
        Assert.Equal(1, ol.IndentLevel);
        Assert.Equal(3, ol.StartNumber);
        Assert.Equal("ordered", ((TextRun)ol.Inlines[0]).Text);

        var ul = Assert.IsType<ListBlockContent>(rebuilt[1].Content);
        Assert.False(ul.Ordered);
        Assert.Equal("bullet", ((TextRun)ul.Inlines[0]).Text);

        Assert.Equal(DocumentBlockType.Quote, rebuilt[2].Type);
        Assert.Equal("quoted", ((TextRun)Assert.IsType<QuoteBlockContent>(rebuilt[2].Content).Inlines[0]).Text);
    }

    [Fact]
    public void RoundTrip_PreservesInlineDrawingRunWithinParagraph()
    {
        var doc = new DocumentEditorDocument { DocumentId = "inl" };
        doc.Blocks =
        [
            new DocumentBlock
            {
                Id = "p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "before" },
                        new DocumentDrawingRun
                        {
                            ObjectId = "obj9",
                            Url = "d.png",
                            Source = DocumentImageSource.Asset,
                            AssetId = "a9",
                            LinkUrl = "https://l.example",
                            Layout = new DocumentObjectLayout { Wrap = { Mode = DocumentWrapMode.Square }, Transform = { Width = 80, Height = 60 } },
                        },
                        new TextRun { Text = "after" },
                    ],
                },
            },
        ];

        var block = Assert.Single(RoundTrip(doc).Blocks);
        Assert.Equal(DocumentBlockType.Paragraph, block.Type);
        var inlines = ((ParagraphBlockContent)block.Content).Inlines;
        Assert.Equal(3, inlines.Count);
        Assert.Equal("before", ((TextRun)inlines[0]).Text);
        Assert.Equal("after", ((TextRun)inlines[2]).Text);
        var drawing = Assert.IsType<DocumentDrawingRun>(inlines[1]);
        Assert.Equal("obj9", drawing.ObjectId);
        Assert.Equal("d.png", drawing.Url);
        Assert.Equal(DocumentImageSource.Asset, drawing.Source);
        Assert.Equal("a9", drawing.AssetId);
        Assert.Equal("https://l.example", drawing.LinkUrl);
        Assert.Equal(DocumentWrapMode.Square, drawing.Layout.Wrap.Mode);
        Assert.Equal(80, drawing.Layout.Transform.Width);
    }

    [Fact]
    public void RoundTrip_PreservesMarksTheEngineDoesNotModel()
    {
        var doc = new DocumentEditorDocument { DocumentId = "mk" };
        doc.Blocks =
        [
            new DocumentBlock
            {
                Id = "p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Text = "x2",
                            Marks =
                            [
                                new InlineMark { Type = InlineMarkType.Bold },          // modelled by the engine
                                new InlineMark { Type = InlineMarkType.Superscript },   // NOT modelled → preserve channel
                            ],
                        },
                    ],
                },
            },
        ];

        var block = Assert.Single(RoundTrip(doc).Blocks);
        var run = (TextRun)((ParagraphBlockContent)block.Content).Inlines[0];
        Assert.Equal("x2", run.Text);
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Bold);
        Assert.Contains(run.Marks, m => m.Type == InlineMarkType.Superscript);
    }

    [Fact]
    public void RoundTrip_PreservesBookmarkMark()
    {
        var doc = new DocumentEditorDocument { DocumentId = "bm" };
        doc.Blocks =
        [
            new DocumentBlock
            {
                Id = "p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = "anchor", Marks = [new InlineMark { Type = InlineMarkType.Bookmark, Value = "intro" }] }],
                },
            },
        ];

        var block = Assert.Single(RoundTrip(doc).Blocks);
        var run = (TextRun)((ParagraphBlockContent)block.Content).Inlines[0];
        var bookmark = Assert.Single(run.Marks, m => m.Type == InlineMarkType.Bookmark);
        Assert.Equal("intro", bookmark.Value);
    }
}
