using Dm = Tempo.Blazor.DocumentEditor.Models;
using Nm = Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.DocumentFormats.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class NotionToDocumentModelConverterTests
{
    [Theory]
    [MemberData(nameof(AllBlockTypes))]
    public void ConvertPage_MapsEveryNotionBlockTypeToAtLeastOneDocumentBlock(BlockType type)
    {
        var page = Page("All block mappings");
        var blocks = BuildBlocksFor(type);

        var result = NotionToDocumentModelConverter.ConvertPage(page, blocks);

        result.Document.Blocks.Should().NotBeEmpty();
        result.Document.Metadata.Title.Should().Be("All block mappings");
        result.Document.Blocks.SelectMany(GetText).Should().Contain(text => !string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void ConvertPage_MapsRichTextHeadingsAndLists()
    {
        var blocks = new IPageBlock[]
        {
            Block(BlockType.Paragraph, 0, new Nm.TextBlockContent { Html = "Hello <strong>bold</strong> <a href=\"https://example.test\">link</a>" }),
            Block(BlockType.Heading2, 1, new Nm.HeadingBlockContent { Html = "Section" }),
            Block(BlockType.BulletList, 2, new Nm.ListBlockContent { Html = "Bullet", IndentLevel = 1 }),
            Block(BlockType.NumberedList, 3, new Nm.ListBlockContent { Html = "Numbered" }),
            Block(BlockType.TodoItem, 4, new Nm.TodoBlockContent { Html = "Done", IsChecked = true })
        };

        var document = NotionToDocumentModelConverter.ConvertPage(Page("Rich"), blocks).Document;

        document.Blocks[0].Content.Should().BeOfType<Dm.ParagraphBlockContent>();
        ((Dm.TextRun)((Dm.ParagraphBlockContent)document.Blocks[0].Content).Inlines[1]).Marks.Should().Contain(mark => mark.Type == Dm.InlineMarkType.Bold);
        ((Dm.TextRun)((Dm.ParagraphBlockContent)document.Blocks[0].Content).Inlines[3]).Marks.Should().Contain(mark => mark.Type == Dm.InlineMarkType.Link && mark.Link!.Href == "https://example.test");
        ((Dm.HeadingBlockContent)document.Blocks[1].Content).Level.Should().Be(2);
        ((Dm.ListBlockContent)document.Blocks[2].Content).IndentLevel.Should().Be(1);
        ((Dm.ListBlockContent)document.Blocks[3].Content).Ordered.Should().BeTrue();
        // The checkbox is state, not text: a literal "[x] " prefix used to be escaped by the
        // Markdown exporter into "\[x\]", which no importer reads back as a task.
        ((Dm.ListBlockContent)document.Blocks[4].Content).IsChecked.Should().BeTrue();
        GetText(document.Blocks[4]).Should().ContainSingle().Which.Should().Be("Done");
    }

    [Fact]
    public void ConvertPage_GroupsConsecutiveTableRowsIntoDocumentTable()
    {
        var blocks = new IPageBlock[]
        {
            Block(BlockType.Table, 0, new Nm.TableBlockContent { ColumnCount = 2, HasHeaderRow = true }),
            Block(BlockType.TableRow, 1, RichRow("Name", "Status")),
            Block(BlockType.TableRow, 2, RichRow("CF25", "Ready"))
        };
        ((PageBlock)blocks[1]).ParentBlockId = blocks[0].Id;
        ((PageBlock)blocks[2]).ParentBlockId = blocks[0].Id;

        var document = NotionToDocumentModelConverter.ConvertPage(Page("Table"), blocks).Document;

        var table = document.Blocks.Should().ContainSingle().Subject.Content.Should().BeOfType<Dm.TableBlockContent>().Subject;
        table.Rows.Should().HaveCount(2);
        table.Rows[0].Cells.Should().HaveCount(2);
        table.Rows[0].Cells[0].IsHeader.Should().BeTrue();
        GetText(table.Rows[1].Cells[1].Blocks[0]).Should().Contain("Ready");
    }

    [Fact]
    public void ConvertPage_SanitizesHistoricalRichTableCellsBeforeParsing()
    {
        var table = Block(
            BlockType.Table,
            0,
            new Nm.TableBlockContent { ColumnCount = 1 });
        var row = Block(
            BlockType.TableRow,
            0,
            new Nm.TableRowBlockContent
            {
                RichCells =
                [
                    new Nm.NotionTableCell
                    {
                        Html = """Safe<img src=x onerror="alert(1)"><strong>Bold</strong>"""
                    }
                ]
            });
        row.ParentBlockId = table.Id;

        var document = NotionToDocumentModelConverter.ConvertPage(Page("Safe table"), [table, row]).Document;
        var cellBlock = ((Dm.TableBlockContent)document.Blocks.Single().Content)
            .Rows.Single()
            .Cells.Single()
            .Blocks.Single();

        GetText(cellBlock).Should().Equal("Safe", "Bold");
    }

    [Fact]
    public void ConvertPage_MapsImageWithoutFallbackWarning()
    {
        var blocks = new[]
        {
            Block(BlockType.Image, 0, new Nm.ImageBlockContent
            {
                Url = "https://example.test/image.png",
                AltText = "Architecture",
                Caption = "Diagram",
                Width = 420
            })
        };

        var result = NotionToDocumentModelConverter.ConvertPage(Page("Image"), blocks);

        var image = result.Document.Blocks.Should().ContainSingle().Subject.Content.Should().BeOfType<Dm.ImageBlockContent>().Subject;
        image.Url.Should().Be("https://example.test/image.png");
        image.AltText.Should().Be("Architecture");
        image.Caption.Should().Be("Diagram");
        image.Size.Width.Should().Be(420);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ConvertPage_EmitsWarningForApproximateUnsupportedBlocks()
    {
        var blocks = new[]
        {
            Block(BlockType.Code, 0, new Nm.CodeBlockContent { Language = "csharp", Code = "Console.WriteLine(\"CF25\");" }),
            Block(BlockType.Diagram, 1, new Nm.DiagramBlockContent { Caption = "Process map" })
        };

        var result = NotionToDocumentModelConverter.ConvertPage(Page("Warnings"), blocks);

        result.Document.Blocks.Should().HaveCount(2);

        // Code now has a document-model counterpart, so converting it is exact, not approximate.
        result.Document.Blocks[0].Type.Should().Be(Dm.DocumentBlockType.Code);
        result.Warnings.Should().NotContain(warning => warning.SourcePath!.Contains(BlockType.Code.ToString(), StringComparison.Ordinal));

        result.Warnings.Should().Contain(warning => warning.Code == "notion.block.approximate" && warning.SourcePath!.Contains(BlockType.Diagram.ToString(), StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> AllBlockTypes()
        => Enum.GetValues<BlockType>().Select(type => new object[] { type });

    private static INotionPage Page(string title) => new NotionPage
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Title = title,
        Description = "Export test page",
        CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
        LastEditedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc),
        CreatedByUserId = "tester",
        LastEditedByUserId = "tester"
    };

    private static IReadOnlyList<IPageBlock> BuildBlocksFor(BlockType type)
    {
        if (type == BlockType.Table)
        {
            return
            [
                Block(BlockType.Table, 0, new Nm.TableBlockContent { ColumnCount = 2, HasHeaderRow = true }),
                Block(BlockType.TableRow, 1, new Nm.TableRowBlockContent { Cells = ["Key", "Value"] })
            ];
        }

        return [Block(type, 0, ContentFor(type))];
    }

    private static PageBlock Block(BlockType type, int order, Nm.IBlockContent content) => new()
    {
        Id = Guid.NewGuid(),
        PageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Type = type,
        Order = order,
        Content = content,
        CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
        LastEditedAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc)
    };

    private static Nm.TableRowBlockContent RichRow(params string[] cells)
        => new()
        {
            RichCells = cells.Select(cell =>
                new Nm.NotionTableCell { Html = cell }).ToList()
        };

    private static Nm.IBlockContent ContentFor(BlockType type) => type switch
    {
        BlockType.Paragraph => new Nm.TextBlockContent { Html = "Paragraph text" },
        BlockType.Heading1 => new Nm.HeadingBlockContent { Html = "Heading 1", Level = 1 },
        BlockType.Heading2 => new Nm.HeadingBlockContent { Html = "Heading 2", Level = 2 },
        BlockType.Heading3 => new Nm.HeadingBlockContent { Html = "Heading 3", Level = 3 },
        BlockType.Quote => new Nm.TextBlockContent { Html = "Quote text" },
        BlockType.Callout => new Nm.CalloutBlockContent { IconEmoji = "!", Html = "Callout text" },
        BlockType.Code => new Nm.CodeBlockContent { Language = "csharp", Code = "var exported = true;" },
        BlockType.Divider => new Nm.DividerBlockContent(),
        BlockType.Equation => new Nm.EquationBlockContent { Expression = "E = mc^2" },
        BlockType.BulletList => new Nm.ListBlockContent { Html = "Bullet text" },
        BlockType.NumberedList => new Nm.ListBlockContent { Html = "Numbered text" },
        BlockType.TodoItem => new Nm.TodoBlockContent { Html = "Todo text", IsChecked = true },
        BlockType.Toggle => new Nm.ToggleBlockContent { Html = "Toggle text" },
        BlockType.TableRow => new Nm.TableRowBlockContent { Cells = ["Standalone", "Row"] },
        BlockType.Image => new Nm.ImageBlockContent { Url = "https://example.test/image.png", AltText = "Image text", Caption = "Image caption" },
        BlockType.Video => new Nm.VideoBlockContent { Url = "https://example.test/video.mp4", Caption = "Video caption" },
        BlockType.Audio => new Nm.AudioBlockContent { Url = "https://example.test/audio.mp3", Caption = "Audio caption" },
        BlockType.File => new Nm.FileBlockContent { FileName = "export.pdf", Url = "https://example.test/export.pdf", Caption = "File caption" },
        BlockType.Pdf => new Nm.PdfBlockContent { Url = "https://example.test/file.pdf", Caption = "PDF caption" },
        BlockType.Bookmark => new Nm.BookmarkBlockContent { Title = "Bookmark title", Url = "https://example.test", Description = "Bookmark description" },
        BlockType.Embed => new Nm.EmbedBlockContent { Url = "https://example.test/embed", Caption = "Embed caption" },
        BlockType.ChildPage => new Nm.ChildPageBlockContent { ChildPageId = Guid.NewGuid(), Title = "Child page" },
        BlockType.LinkedPage => new Nm.LinkedPageBlockContent { LinkedPageId = Guid.NewGuid(), Title = "Linked page" },
        BlockType.Breadcrumb => new Nm.BreadcrumbBlockContent(),
        BlockType.SyncedBlockOrigin => new Nm.SyncedBlockOriginContent { SyncId = Guid.NewGuid() },
        BlockType.SyncedBlockRef => new Nm.SyncedBlockRefContent { SyncId = Guid.NewGuid(), OriginPageId = Guid.NewGuid(), OriginBlockId = Guid.NewGuid() },
        BlockType.InlineDatabase => new Nm.InlineDatabaseBlockContent { DatabaseId = Guid.NewGuid(), Title = "Inline database" },
        BlockType.LinkedDatabase => new Nm.LinkedDatabaseBlockContent { SourceDatabaseId = Guid.NewGuid(), SourcePageId = Guid.NewGuid() },
        BlockType.ColumnList => new Nm.ColumnListBlockContent { ColumnCount = 2 },
        BlockType.Column => new Nm.ColumnBlockContent { ColumnIndex = 1, WidthPercent = 50 },
        BlockType.TemplateButton => new Nm.TemplateButtonBlockContent { Label = "Template button" },
        BlockType.TableOfContents => new Nm.TableOfContentsBlockContent { MaxLevel = 3 },
        BlockType.Diagram => new Nm.DiagramBlockContent { Caption = "Diagram caption" },
        BlockType.Wireframe => new Nm.WireframeBlockContent { Caption = "Wireframe caption" },
        BlockType.Spreadsheet => new Nm.SpreadsheetBlockContent { Caption = "Spreadsheet caption" },
        BlockType.WorkItem => new Nm.WorkItemBlockContent { SourceKey = "demo", ExternalId = "DEMO-25", CachedSnapshot = new Tempo.Blazor.Abstractions.WorkItems.TmWorkItem { Title = "Work item title", StatusLabel = "Open" } },
        BlockType.ContentByLabel => new Nm.ContentByLabelBlockContent { Labels = ["export"], MaxItems = 5 },
        BlockType.IncludePage => new Nm.IncludePageBlockContent { SourcePageId = Guid.NewGuid() },
        BlockType.ChildrenDisplay => new Nm.ChildrenDisplayBlockContent { RootPageId = Guid.NewGuid(), Depth = 2 },
        BlockType.Excerpt => new Nm.ExcerptBlockContent { Html = "Excerpt text" },
        BlockType.ExcerptInclude => new Nm.ExcerptIncludeBlockContent { SourcePageId = Guid.NewGuid() },
        BlockType.PageProperties => new Nm.PagePropertiesBlockContent { Rows = [new Nm.PagePropertyRow { Key = "Owner", ValueHtml = "Docs" }] },
        BlockType.PagePropertiesReport => new Nm.PagePropertiesReportBlockContent { Labels = ["export"], Columns = ["Owner"] },
        _ => new Nm.TextBlockContent { Html = type.ToString() }
    };

    private static IEnumerable<string> GetText(Dm.DocumentBlock block)
    {
        return block.Content switch
        {
            Dm.ParagraphBlockContent paragraph => paragraph.Inlines.OfType<Dm.TextRun>().Select(run => run.Text),
            Dm.HeadingBlockContent heading => heading.Inlines.OfType<Dm.TextRun>().Select(run => run.Text),
            Dm.ListBlockContent list => list.Inlines.OfType<Dm.TextRun>().Select(run => run.Text),
            Dm.QuoteBlockContent quote => quote.Inlines.OfType<Dm.TextRun>().Select(run => run.Text),
            Dm.TableBlockContent table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks).SelectMany(GetText),
            Dm.ImageBlockContent image => [image.AltText ?? image.Caption ?? image.Url ?? image.AssetId ?? string.Empty],
            Dm.CodeBlockContent code => [code.Code],
            Dm.PageBreakBlockContent => ["Page break"],
            _ => []
        };
    }
}
