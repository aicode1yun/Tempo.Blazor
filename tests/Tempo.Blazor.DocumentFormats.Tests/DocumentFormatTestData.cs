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

    public static DocumentEditorDocument CreateImageLayoutParityDocument()
    {
        var document = DocumentEditorDocument.Empty("image-layout-format-parity");
        document.Metadata.Title = "Image layout parity";
        document.Blocks =
        [
            Paragraph("Before images"),
            ImageBlock("Inline image", 1, new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Inline,
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Inline },
                Transform = new DocumentObjectTransform { Width = 120, Height = 72, NaturalWidth = 240, NaturalHeight = 144, LockAspectRatio = true }
            }),
            ImageBlock("Square left image", 2, new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Anchor = new DocumentObjectAnchor { BlockId = "anchor-paragraph", InlineIndex = 1, Offset = 4, Region = DocumentRenditionAnchorScope.Body, MoveWithText = true, LockAnchor = true },
                Position = new DocumentObjectPosition { HorizontalRelativeTo = DocumentRelativePosition.Margin, VerticalRelativeTo = DocumentRelativePosition.Paragraph, X = 36, Y = 48, HorizontalAlignment = DocumentImageHorizontalPosition.Left },
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square, DistanceLeft = 8, DistanceRight = 4, DistanceTop = 2, DistanceBottom = 3 },
                Transform = new DocumentObjectTransform { Width = 160, Height = 90, NaturalWidth = 320, NaturalHeight = 180, LockAspectRatio = true, Rotation = 7.5 },
                Stacking = new DocumentObjectStacking { ZIndex = 7, AllowOverlap = true }
            }),
            ImageBlock("Square right image", 3, new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Anchor = new DocumentObjectAnchor { BlockId = "anchor-paragraph", Offset = 12, MoveWithText = true },
                Position = new DocumentObjectPosition { HorizontalRelativeTo = DocumentRelativePosition.Margin, VerticalRelativeTo = DocumentRelativePosition.Paragraph, X = 12, Y = 16, HorizontalAlignment = DocumentImageHorizontalPosition.Right },
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square, DistanceLeft = 6, DistanceRight = 10, DistanceTop = 1, DistanceBottom = 5 },
                Transform = new DocumentObjectTransform { Width = 144, Height = 88, NaturalWidth = 288, NaturalHeight = 176, LockAspectRatio = false, Rotation = -4 },
                Stacking = new DocumentObjectStacking { ZIndex = 5 }
            }),
            ImageBlock("Top bottom image", 4, new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Anchor = new DocumentObjectAnchor { BlockId = "anchor-paragraph", MoveWithText = true },
                Position = new DocumentObjectPosition { HorizontalRelativeTo = DocumentRelativePosition.Page, VerticalRelativeTo = DocumentRelativePosition.Paragraph, X = 0, Y = 28, HorizontalAlignment = DocumentImageHorizontalPosition.Center },
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.TopBottom, DistanceTop = 9, DistanceBottom = 11 },
                Transform = new DocumentObjectTransform { Width = 180, Height = 100, NaturalWidth = 360, NaturalHeight = 200, LockAspectRatio = true },
                Stacking = new DocumentObjectStacking { ZIndex = 3 }
            }),
            ImageBlock("Behind image", 5, new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Anchor = new DocumentObjectAnchor { BlockId = "anchor-paragraph", MoveWithText = true },
                Position = new DocumentObjectPosition { HorizontalRelativeTo = DocumentRelativePosition.Page, VerticalRelativeTo = DocumentRelativePosition.Page, X = 44, Y = 52 },
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.BehindText, DistanceLeft = 2, DistanceRight = 2, DistanceTop = 2, DistanceBottom = 2 },
                Transform = new DocumentObjectTransform { Width = 210, Height = 120, NaturalWidth = 420, NaturalHeight = 240, LockAspectRatio = true, Rotation = 12 },
                Stacking = new DocumentObjectStacking { ZIndex = 1, AllowOverlap = true }
            }),
            ImageBlock("Fixed front image", 6, new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Fixed,
                Anchor = new DocumentObjectAnchor { BlockId = "anchor-paragraph", Region = DocumentRenditionAnchorScope.Body, MoveWithText = false, FixedOnPage = true, LockAnchor = true },
                Position = new DocumentObjectPosition { HorizontalRelativeTo = DocumentRelativePosition.Page, VerticalRelativeTo = DocumentRelativePosition.Page, X = 72, Y = 96, HorizontalAlignment = DocumentImageHorizontalPosition.Right, VerticalAlignment = DocumentObjectVerticalAlignment.Bottom },
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.InFrontOfText, DistanceLeft = 5, DistanceRight = 7, DistanceTop = 9, DistanceBottom = 11 },
                Transform = new DocumentObjectTransform { Width = 130, Height = 84, NaturalWidth = 260, NaturalHeight = 168, LockAspectRatio = false, Rotation = -10 },
                Stacking = new DocumentObjectStacking { ZIndex = 9, AllowOverlap = true }
            })
        ];

        return document;
    }

    public static void AssertImageLayoutParity(DocumentEditorDocument imported)
    {
        var expected = CreateImageLayoutParityDocument().Blocks
            .Select(block => block.Content)
            .OfType<ImageBlockContent>()
            .ToDictionary(image => image.AltText!, StringComparer.Ordinal);
        var actual = imported.Blocks
            .Select(block => block.Content)
            .OfType<ImageBlockContent>()
            .ToDictionary(image => image.AltText!, StringComparer.Ordinal);

        actual.Keys.Should().Contain(expected.Keys);
        foreach (var (altText, expectedImage) in expected)
        {
            AssertLayout(actual[altText].Layout, expectedImage.Layout);
        }
    }

    private static DocumentBlock ImageBlock(string altText, int order, DocumentObjectLayout layout)
    {
        return new DocumentBlock
        {
            Id = $"image-layout-{order}",
            Type = DocumentBlockType.Image,
            Order = order,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = TransparentPngDataUrl,
                AltText = altText,
                Caption = $"{altText} caption",
                Size = new DocumentImageSize { Width = layout.Transform.Width, Height = layout.Transform.Height },
                Layout = layout
            }
        };
    }

    private static void AssertLayout(DocumentObjectLayout actual, DocumentObjectLayout expected)
    {
        actual.Kind.Should().Be(expected.Kind);
        actual.Anchor.BlockId.Should().Be(expected.Anchor.BlockId);
        actual.Anchor.InlineIndex.Should().Be(expected.Anchor.InlineIndex);
        actual.Anchor.Offset.Should().Be(expected.Anchor.Offset);
        actual.Anchor.Region.Should().Be(expected.Anchor.Region);
        actual.Anchor.MoveWithText.Should().Be(expected.Anchor.MoveWithText);
        actual.Anchor.FixedOnPage.Should().Be(expected.Anchor.FixedOnPage);
        actual.Anchor.LockAnchor.Should().Be(expected.Anchor.LockAnchor);
        actual.Position.HorizontalRelativeTo.Should().Be(expected.Position.HorizontalRelativeTo);
        actual.Position.VerticalRelativeTo.Should().Be(expected.Position.VerticalRelativeTo);
        actual.Position.X.Should().BeApproximately(expected.Position.X, 0.1);
        actual.Position.Y.Should().BeApproximately(expected.Position.Y, 0.1);
        actual.Position.HorizontalAlignment.Should().Be(expected.Position.HorizontalAlignment);
        actual.Position.VerticalAlignment.Should().Be(expected.Position.VerticalAlignment);
        actual.Wrap.Mode.Should().Be(expected.Wrap.Mode);
        actual.Wrap.DistanceLeft.Should().BeApproximately(expected.Wrap.DistanceLeft, 0.1);
        actual.Wrap.DistanceRight.Should().BeApproximately(expected.Wrap.DistanceRight, 0.1);
        actual.Wrap.DistanceTop.Should().BeApproximately(expected.Wrap.DistanceTop, 0.1);
        actual.Wrap.DistanceBottom.Should().BeApproximately(expected.Wrap.DistanceBottom, 0.1);
        AssertNullableDouble(actual.Transform.Width, expected.Transform.Width);
        AssertNullableDouble(actual.Transform.Height, expected.Transform.Height);
        AssertNullableDouble(actual.Transform.NaturalWidth, expected.Transform.NaturalWidth);
        AssertNullableDouble(actual.Transform.NaturalHeight, expected.Transform.NaturalHeight);
        actual.Transform.LockAspectRatio.Should().Be(expected.Transform.LockAspectRatio);
        actual.Transform.Rotation.Should().BeApproximately(expected.Transform.Rotation, 0.01);
        actual.Stacking.ZIndex.Should().Be(expected.Stacking.ZIndex);
        actual.Stacking.AllowOverlap.Should().Be(expected.Stacking.AllowOverlap);
    }

    private static void AssertNullableDouble(double? actual, double? expected)
    {
        if (expected.HasValue)
        {
            actual.Should().NotBeNull();
            actual!.Value.Should().BeApproximately(expected.Value, 0.1);
        }
        else
        {
            actual.Should().BeNull();
        }
    }
}
