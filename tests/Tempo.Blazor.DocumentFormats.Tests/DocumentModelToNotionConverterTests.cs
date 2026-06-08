using Dm = Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Markdown;
using Tempo.Blazor.DocumentFormats.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class DocumentModelToNotionConverterTests
{
    [Fact]
    public void ConvertDocument_MapsTextHeadingsListsTablesImagesAndDividers()
    {
        var pageId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var document = CreateDocument();

        var result = DocumentModelToNotionConverter.ConvertDocument(document, pageId);

        result.Warnings.Should().BeEmpty();
        result.Blocks.Should().Contain(block => block.Type == BlockType.Paragraph && ((TextBlockContent)block.Content).Html.Contains("<strong>bold</strong>", StringComparison.Ordinal));
        result.Blocks.Should().Contain(block => block.Type == BlockType.Heading2 && ((HeadingBlockContent)block.Content).Html == "Imported Heading");
        result.Blocks.Should().Contain(block => block.Type == BlockType.BulletList && ((ListBlockContent)block.Content).IndentLevel == 1);
        result.Blocks.Should().Contain(block => block.Type == BlockType.NumberedList);
        result.Blocks.Should().Contain(block => block.Type == BlockType.TodoItem && ((TodoBlockContent)block.Content).IsChecked);
        result.Blocks.Should().Contain(block => block.Type == BlockType.Divider);

        var table = result.Blocks.Single(block => block.Type == BlockType.Table);
        ((TableBlockContent)table.Content).ColumnCount.Should().Be(2);
        ((TableBlockContent)table.Content).HasHeaderRow.Should().BeTrue();
        var rows = result.Blocks.Where(block => block.Type == BlockType.TableRow).OrderBy(block => block.Order).ToList();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.ParentBlockId == table.Id);
        ((TableRowBlockContent)rows[1].Content).Cells.Should().Contain("Ready");

        var image = (ImageBlockContent)result.Blocks.Single(block => block.Type == BlockType.Image).Content;
        image.Url.Should().Be("https://example.test/diagram.png");
        image.AltText.Should().Be("Diagram");
        image.Caption.Should().Be("Architecture");
        image.Width.Should().Be(480);
    }

    [Theory]
    [InlineData(Dm.DocumentBlockType.Paragraph)]
    [InlineData(Dm.DocumentBlockType.Heading)]
    [InlineData(Dm.DocumentBlockType.List)]
    [InlineData(Dm.DocumentBlockType.Quote)]
    [InlineData(Dm.DocumentBlockType.Table)]
    [InlineData(Dm.DocumentBlockType.Image)]
    [InlineData(Dm.DocumentBlockType.PageBreak)]
    public void ConvertDocument_MapsEveryDocumentBlockType(Dm.DocumentBlockType type)
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Blocks.Add(BlockFor(type));

        var result = DocumentModelToNotionConverter.ConvertDocument(document, Guid.NewGuid());

        result.Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public void ConvertDocument_ClipboardImageWithoutAssetFallsBackWithWarning()
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Blocks.Add(new Dm.DocumentBlock
        {
            Type = Dm.DocumentBlockType.Image,
            Content = new Dm.ImageBlockContent
            {
                Source = Dm.DocumentImageSource.Clipboard,
                AltText = "Clipboard import"
            }
        });

        var result = DocumentModelToNotionConverter.ConvertDocument(document, Guid.NewGuid());

        result.Blocks.Should().ContainSingle(block => block.Type == BlockType.Paragraph);
        result.Warnings.Should().ContainSingle(warning => warning.Code == "document.block.approximate");
    }

    [Fact]
    public void MarkdownImporter_ReadsDocumentStructuresForNotionBridge()
    {
        const string markdown = """
        # Imported Markdown

        Paragraph with **bold** and [link](https://example.test).

        - [x] Published
        - Nested item

        | Name | Status |
        | --- | --- |
        | CF26 | Ready |

        ![Diagram](https://example.test/diagram.png)
        """;

        var document = new DocumentMarkdownImporter().Import(markdown);
        var result = DocumentModelToNotionConverter.ConvertDocument(document, Guid.NewGuid());

        result.Blocks.Should().Contain(block => block.Type == BlockType.Heading1);
        result.Blocks.Should().Contain(block => block.Type == BlockType.TodoItem);
        result.Blocks.Should().Contain(block => block.Type == BlockType.Table);
        result.Blocks.Should().Contain(block => block.Type == BlockType.Image);
    }

    private static Dm.DocumentEditorDocument CreateDocument()
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Metadata.Title = "CF26 Import Bridge";
        document.Blocks =
        [
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Paragraph,
                Order = 0,
                Content = new Dm.ParagraphBlockContent
                {
                    Inlines =
                    [
                        new Dm.TextRun { Text = "Intro " },
                        new Dm.TextRun { Text = "bold", Marks = [new Dm.InlineMark { Type = Dm.InlineMarkType.Bold }] }
                    ]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Heading,
                Order = 1,
                Content = new Dm.HeadingBlockContent
                {
                    Level = 2,
                    Inlines = [new Dm.TextRun { Text = "Imported Heading" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.List,
                Order = 2,
                Content = new Dm.ListBlockContent
                {
                    IndentLevel = 1,
                    Inlines = [new Dm.TextRun { Text = "Bullet item" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.List,
                Order = 3,
                Content = new Dm.ListBlockContent
                {
                    Ordered = true,
                    Inlines = [new Dm.TextRun { Text = "Ordered item" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.List,
                Order = 4,
                Content = new Dm.ListBlockContent
                {
                    Inlines = [new Dm.TextRun { Text = "[x] Done item" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Table,
                Order = 5,
                Content = new Dm.TableBlockContent
                {
                    Rows =
                    [
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                Cell("Name", true),
                                Cell("Status", true)
                            ]
                        },
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                Cell("CF26", false),
                                Cell("Ready", false)
                            ]
                        }
                    ]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Image,
                Order = 6,
                Content = new Dm.ImageBlockContent
                {
                    Source = Dm.DocumentImageSource.Url,
                    Url = "https://example.test/diagram.png",
                    AltText = "Diagram",
                    Caption = "Architecture",
                    Size = new Dm.DocumentImageSize { Width = 480 }
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.PageBreak,
                Order = 7,
                Content = new Dm.PageBreakBlockContent()
            }
        ];
        return document;
    }

    private static Dm.TableCellContent Cell(string text, bool isHeader) => new()
    {
        IsHeader = isHeader,
        Blocks =
        [
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Paragraph,
                Content = new Dm.ParagraphBlockContent { Inlines = [new Dm.TextRun { Text = text }] }
            }
        ]
    };

    private static Dm.DocumentBlock BlockFor(Dm.DocumentBlockType type) => type switch
    {
        Dm.DocumentBlockType.Paragraph => new Dm.DocumentBlock { Type = type, Content = new Dm.ParagraphBlockContent { Inlines = [new Dm.TextRun { Text = "Paragraph" }] } },
        Dm.DocumentBlockType.Heading => new Dm.DocumentBlock { Type = type, Content = new Dm.HeadingBlockContent { Level = 1, Inlines = [new Dm.TextRun { Text = "Heading" }] } },
        Dm.DocumentBlockType.List => new Dm.DocumentBlock { Type = type, Content = new Dm.ListBlockContent { Inlines = [new Dm.TextRun { Text = "List" }] } },
        Dm.DocumentBlockType.Quote => new Dm.DocumentBlock { Type = type, Content = new Dm.QuoteBlockContent { Inlines = [new Dm.TextRun { Text = "Quote" }] } },
        Dm.DocumentBlockType.Table => new Dm.DocumentBlock { Type = type, Content = new Dm.TableBlockContent { Rows = [new Dm.TableRowContent { Cells = [Cell("Cell", true)] }] } },
        Dm.DocumentBlockType.Image => new Dm.DocumentBlock { Type = type, Content = new Dm.ImageBlockContent { Url = "https://example.test/image.png", AltText = "Image" } },
        Dm.DocumentBlockType.PageBreak => new Dm.DocumentBlock { Type = type, Content = new Dm.PageBreakBlockContent() },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
