using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Tests;

internal static class DocumentFormatTestData
{
    public const string TransparentPngDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    public static DocumentEditorDocument CreateDocument()
    {
        var document = DocumentEditorDocument.Empty("format-test");
        document.Metadata.Title = "Format test";
        document.Metadata.Author = new DocumentEditorAuthor { DisplayName = "Tester" };
        document.Sections[0].Properties.PageSettings.Landscape = true;
        document.Sections[0].Properties.PageSettings.Margins = new DocumentPageMargins { Top = 36, Right = 48, Bottom = 36, Left = 48 };
        document.Blocks =
        [
            new()
            {
                Type = DocumentBlockType.Heading,
                Order = 0,
                Content = new HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new TextRun { Text = "Agreement" }]
                }
            },
            new()
            {
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "Bold", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
                        new TextRun { Text = " and " },
                        new TextRun { Text = "link", Marks = [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = "https://example.test" } }] }
                    ]
                }
            },
            new()
            {
                Type = DocumentBlockType.List,
                Order = 2,
                Content = new ListBlockContent
                {
                    Ordered = true,
                    Inlines = [new TextRun { Text = "Numbered item" }]
                }
            },
            new()
            {
                Type = DocumentBlockType.Table,
                Order = 3,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent
                                {
                                    ColumnSpan = 2,
                                    RowSpan = 1,
                                    Blocks = [Paragraph("Merged")]
                                },
                                new TableCellContent { Blocks = [Paragraph("B1")] }
                            ]
                        },
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Blocks = [Paragraph("A2")] },
                                new TableCellContent { Blocks = [Paragraph("B2")] }
                            ]
                        }
                    ]
                }
            },
            new()
            {
                Type = DocumentBlockType.Image,
                Order = 4,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = TransparentPngDataUrl,
                    AltText = "Tiny image",
                    Caption = "Image caption",
                    FloatingLayout = new DocumentFloatingLayout { Inline = false, WrapMode = DocumentWrapMode.Square }
                }
            },
            new()
            {
                Type = DocumentBlockType.PageBreak,
                Order = 5,
                Content = new PageBreakBlockContent()
            }
        ];
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Header,
            Blocks = [Paragraph("Header text")]
        });
        document.Notes.Add(new DocumentNote
        {
            Id = "1",
            Type = DocumentNoteType.Footnote,
            Blocks = [Paragraph("Footnote text")]
        });
        document.Comments.Add(new DocumentComment
        {
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.Block, BlockId = document.Blocks[0].Id },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Reviewer" },
                    Text = "Comment text"
                }
            ]
        });
        document.Revisions.Add(new DocumentRevision
        {
            Type = DocumentRevisionType.Insertion,
            Author = new DocumentRevisionAuthor { DisplayName = "Reviewer" }
        });
        return document;
    }

    public static DocumentBlock Paragraph(string text)
    {
        return new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = text }]
            }
        };
    }
}
