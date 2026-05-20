using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class WysiwygPatchApplierTests
{
    private readonly WysiwygPatchApplier _applier = new();

    private static DocumentEditorDocument CreateDocument(params DocumentBlock[] blocks)
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Metadata.Title = "Test document";
        foreach (var block in blocks.Select((b, i) => { b.Order = (i + 1) * 10; return b; }))
        {
            document.Blocks.Add(block);
        }
        return document;
    }

    private static DocumentBlock Paragraph(string id, string text)
    {
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "i-" + id, Text = text }
                ]
            }
        };
    }

    private static DocumentBlock FloatingImageBlock(string id, DocumentWrapMode wrapMode)
    {
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = "https://example.test/image.png",
                AltText = "Example image",
                Size = new DocumentImageSize { Width = 160, Height = 90 },
                FloatingLayout = new DocumentFloatingLayout
                {
                    Inline = false,
                    WrapMode = wrapMode,
                    HorizontalRelativeTo = DocumentRelativePosition.Page,
                    VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                    X = 24,
                    Y = 32,
                    ZIndex = 3
                }
            }
        };
    }

    // ── Snapshot roundtrip ───────────────────────────────────────────────────

    [Fact]
    public void Snapshot_Roundtrip_KeepsBasicData()
    {
        var document = CreateDocument(
            Paragraph("b1", "Hello "),
            Paragraph("b2", "world"));

        var snapshot = new WysiwygDocumentSnapshot
        {
            ProtocolVersion = 1,
            Document = document
        };

        snapshot.Document.Metadata.Title.Should().Be("Test document");
        snapshot.Document.Blocks.Should().HaveCount(2);
        snapshot.Document.Blocks[0].Type.Should().Be(DocumentBlockType.Paragraph);
    }

    // ── InsertText ───────────────────────────────────────────────────────────

    [Fact]
    public void InsertText_AppendsToEnd()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = " world",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            }
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("Hello world");
    }

    [Fact]
    public void InsertText_InsertsInMiddle()
    {
        var document = CreateDocument(Paragraph("b1", "Hlo"));
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "el",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 1
            }
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("Hello");
    }

    [Fact]
    public void InsertText_WithHeaderRegion_UpdatesHeaderFooterBlocksOnly()
    {
        var document = CreateDocument(Paragraph("body-1", "Body text"));
        var header = AddHeaderFooter(document, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.Primary, "header-primary", "header-block", "Header");

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "InsertText",
            Data = " edited",
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "Header",
                HeaderFooterId = header.Id,
                AnchorBlockId = "header-block",
                AnchorInlineId = "i-header-block",
                AnchorOffset = 6
            }
        });

        GetInlineText(document, "body-1").Should().Be("Body text");
        GetHeaderFooterInlineText(header, "header-block").Should().Be("Header edited");
    }

    [Fact]
    public void InsertText_FirstPageHeader_DoesNotChangePrimaryHeader()
    {
        var document = CreateDocument(Paragraph("body-1", "Body text"));
        var primaryHeader = AddHeaderFooter(document, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.Primary, "header-primary", "primary-header-block", "Primary");
        var firstPageHeader = AddHeaderFooter(document, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.FirstPage, "header-first", "first-header-block", "First");

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "InsertText",
            Data = " page",
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "Header",
                PageIndex = 0,
                HeaderFooterId = firstPageHeader.Id,
                AnchorBlockId = "first-header-block",
                AnchorInlineId = "i-first-header-block",
                AnchorOffset = 5
            }
        });

        GetHeaderFooterInlineText(primaryHeader, "primary-header-block").Should().Be("Primary");
        GetHeaderFooterInlineText(firstPageHeader, "first-header-block").Should().Be("First page");
    }

    [Fact]
    public void InsertText_TableCellSelection_UpdatesOnlyTargetCellContent()
    {
        var document = CreateDocument(
            Paragraph("before", "Before"),
            new DocumentBlock
            {
                Id = "table-1",
                Type = DocumentBlockType.Table,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "cell-1", Blocks = [Paragraph("cell-block-1", "Alpha")] },
                                new TableCellContent { Id = "cell-2", Blocks = [Paragraph("cell-block-2", "Beta")] }
                            ]
                        }
                    ]
                }
            },
            Paragraph("after", "After"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "!",
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "TableCell",
                AnchorBlockId = "cell-block-2",
                AnchorInlineId = "i-cell-block-2",
                AnchorOffset = 4,
                ActiveTableCellId = "cell-2",
                TableCellPath = "table-1/row-0/cell-2"
            }
        });

        var table = (TableBlockContent)document.Blocks[1].Content;
        ReadCellText(table.Rows[0].Cells[0]).Should().Be("Alpha");
        ReadCellText(table.Rows[0].Cells[1]).Should().Be("Beta!");
        GetInlineText(document, "before").Should().Be("Before");
        GetInlineText(document, "after").Should().Be("After");
    }

    [Fact]
    public void InsertText_EmptyTableCellSelection_CreatesCellParagraphTarget()
    {
        var document = CreateDocument(new DocumentBlock
        {
            Id = "table-1",
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent { Id = "cell-1", Blocks = [] }
                        ]
                    }
                ]
            }
        });

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "First",
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "TableCell",
                AnchorBlockId = "generated-cell-block",
                AnchorInlineId = "generated-cell-inline",
                AnchorOffset = 0,
                ActiveTableCellId = "cell-1",
                TableCellPath = "table-1/row-0/cell-1"
            }
        });

        var table = (TableBlockContent)document.Blocks[0].Content;
        table.Rows[0].Cells[0].Blocks.Should().ContainSingle();
        table.Rows[0].Cells[0].Blocks[0].Id.Should().Be("generated-cell-block");
        ReadCellText(table.Rows[0].Cells[0]).Should().Be("First");
    }

    [Fact]
    public void InsertInline_SplitsTextRunAndPreservesToken()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "InsertInline",
            Inline = new TokenRun
            {
                Key = "client.name",
                DisplayName = "Client name",
                TokenType = "text"
            },
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6
            }
        };

        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1");
        inlines.Should().HaveCount(3);
        inlines[0].Should().BeOfType<TextRun>().Which.Text.Should().Be("Hello ");
        inlines[1].Should().BeOfType<TokenRun>().Which.Key.Should().Be("client.name");
        inlines[2].Should().BeOfType<TextRun>().Which.Text.Should().Be("world");
    }

    [Fact]
    public void InsertInline_HeaderFooterField_PreservesAutomaticFieldRun()
    {
        var document = CreateDocument(Paragraph("body-1", "Body"));
        var header = AddHeaderFooter(document, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.Primary, "header-primary", "header-block", "Header ");

        var patch = new WysiwygPatch
        {
            Type = "InsertInline",
            Inline = new DocumentFieldRun
            {
                FieldType = DocumentFieldType.PageXOfY,
                FallbackText = "1 / 1"
            },
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "Header",
                HeaderFooterId = header.Id,
                AnchorBlockId = "header-block",
                AnchorInlineId = "i-header-block",
                AnchorOffset = 7
            }
        };

        _applier.ApplyPatch(document, patch);

        var inlines = ((ParagraphBlockContent)header.Blocks[0].Content).Inlines;
        inlines.Should().HaveCount(2);
        inlines[0].Should().BeOfType<TextRun>().Which.Text.Should().Be("Header ");
        inlines[1].Should().BeOfType<DocumentFieldRun>().Which.FieldType.Should().Be(DocumentFieldType.PageXOfY);
    }

    // ── DeleteRange ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteRange_RemovesCharacters()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "DeleteRange",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            },
            DeleteLength = 6
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("Hello");
    }

    [Fact]
    public void DeleteRange_ClampedToTextLength()
    {
        var document = CreateDocument(Paragraph("b1", "Hi"));
        var patch = new WysiwygPatch
        {
            Type = "DeleteRange",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 1
            },
            DeleteLength = 100
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("H");
    }

    // ── DeleteContentBackward / Forward ──────────────────────────────────────

    [Fact]
    public void DeleteContentBackward_RemovesPreviousCharacter()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentBackward",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            }
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("Hell");
    }

    [Fact]
    public void DeleteContentForward_RemovesNextCharacter()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentForward",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 0
            }
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("ello");
    }

    // ── ToggleMark (Bold) ────────────────────────────────────────────────────

    [Fact]
    public void ToggleMark_Bold_AddsMarkToRange()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6,
                FocusBlockId = "b1",
                FocusInlineId = "i-b1",
                FocusOffset = 11,
                IsCollapsed = false
            }
        };

        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1");
        inlines.Should().HaveCountGreaterThanOrEqualTo(2);
        var marked = inlines.FirstOrDefault(i => i.Marks.Any(m => m.Type == InlineMarkType.Bold));
        marked.Should().NotBeNull();
        (marked as TextRun)?.Text.Should().Be("world");
    }

    [Fact]
    public void ToggleMark_Bold_SplitsPartialTextRunIntoBeforeSelectionAfter()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = Selection("b1", "i-b1", 1, 4)
        };

        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1").OfType<TextRun>().ToList();
        inlines.Should().HaveCount(3);
        inlines.Select(inline => inline.Text).Should().Equal("H", "ell", "o");
        inlines[0].Marks.Should().BeEmpty();
        inlines[1].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
        inlines[2].Marks.Should().BeEmpty();
    }

    [Fact]
    public void ToggleMark_Bold_RemovesMarkOnSecondToggle()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6,
                FocusBlockId = "b1",
                FocusInlineId = "i-b1",
                FocusOffset = 11,
                IsCollapsed = false
            }
        };

        _applier.ApplyPatch(document, patch);
        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1");
        inlines.Should().HaveCount(1);
        inlines[0].Marks.Should().BeEmpty();
    }

    [Fact]
    public void ToggleMark_ItalicAndUnderline_DoNotRemoveEachOther()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Italic",
            Selection = Selection("b1", "i-b1", 0, 5)
        });
        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Underline",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        var inline = GetInlines(document, "b1").Should().ContainSingle().Subject;
        inline.Marks.Select(mark => mark.Type).Should().BeEquivalentTo(
            [InlineMarkType.Italic, InlineMarkType.Underline]);
    }

    [Fact]
    public void ToggleMark_Italic_AddsItalicMark()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Italic",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 0,
                FocusBlockId = "b1",
                FocusInlineId = "i-b1",
                FocusOffset = 5,
                IsCollapsed = false
            }
        };

        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1");
        inlines.Should().ContainSingle();
        inlines[0].Marks.Should().ContainSingle(m => m.Type == InlineMarkType.Italic);
    }

    [Fact]
    public void ToggleMark_Underline_AddsUnderlineMark()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Underline",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6,
                FocusBlockId = "b1",
                FocusInlineId = "i-b1",
                FocusOffset = 11,
                IsCollapsed = false
            }
        };

        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1");
        inlines.Should().HaveCountGreaterThanOrEqualTo(2);
        var marked = inlines.FirstOrDefault(i => i.Marks.Any(m => m.Type == InlineMarkType.Underline));
        marked.Should().NotBeNull();
        (marked as TextRun)?.Text.Should().Be("world");
    }

    [Fact]
    public void ToggleMark_Underline_RemovesMarkOnSecondToggle()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Underline",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6,
                FocusBlockId = "b1",
                FocusInlineId = "i-b1",
                FocusOffset = 11,
                IsCollapsed = false
            }
        };

        _applier.ApplyPatch(document, patch);
        _applier.ApplyPatch(document, patch);

        var inlines = GetInlines(document, "b1");
        inlines.Should().HaveCount(1);
        inlines[0].Marks.Should().BeEmpty();
    }

    // ── InsertBlock ──────────────────────────────────────────────────────────

    [Fact]
    public void InsertBlock_AddsNewBlockAfterAnchor()
    {
        var document = CreateDocument(Paragraph("b1", "First"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Paragraph",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorOffset = 0
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Type.Should().Be(DocumentBlockType.Paragraph);
    }

    [Fact]
    public void InsertBlock_StructuralParagraph_SplitsAnchorTextAndKeepsInsertedInlineId()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Paragraph",
            RevisionType = "Structural",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6
            },
            Block = new DocumentBlock
            {
                Id = "b2",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Id = "i-b2", Text = "world" }]
                }
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        GetInlineText(document, "b1").Should().Be("Hello ");
        GetInlineText(document, "b2").Should().Be("world");
        GetInlines(document, "b2").OfType<TextRun>().Single().Id.Should().Be("i-b2");
    }

    [Fact]
    public void InsertBlock_Heading_RespectsHeadingLevel()
    {
        var document = CreateDocument(Paragraph("b1", "First"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Heading",
            HeadingLevel = 2,
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorOffset = 0
            }
        };

        _applier.ApplyPatch(document, patch);

        var heading = document.Blocks[1];
        heading.Type.Should().Be(DocumentBlockType.Heading);
        ((HeadingBlockContent)heading.Content).Level.Should().Be(2);
    }

    [Fact]
    public void InsertBlock_ImageUrl_InsertsImageBlock()
    {
        var document = CreateDocument(Paragraph("b1", "Before"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Image",
            Block = new DocumentBlock
            {
                Id = "img-1",
                Type = DocumentBlockType.Image,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = "https://example.test/image.png",
                    AltText = "Example image"
                }
            },
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "b1" }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Type.Should().Be(DocumentBlockType.Image);
        var image = document.Blocks[1].Content as ImageBlockContent;
        image.Should().NotBeNull();
        image!.Source.Should().Be(DocumentImageSource.Url);
        image.Url.Should().Be("https://example.test/image.png");
        image.AltText.Should().Be("Example image");
    }

    [Fact]
    public void InsertBlock_ImageUrl_StripsUnsafeUrl()
    {
        var document = CreateDocument(Paragraph("b1", "Before"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Image",
            Block = new DocumentBlock
            {
                Id = "img-1",
                Type = DocumentBlockType.Image,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = "javascript:alert(1)",
                    AltText = "Unsafe image"
                }
            },
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "b1" }
        };

        _applier.ApplyPatch(document, patch);

        var image = document.Blocks[1].Content as ImageBlockContent;
        image.Should().NotBeNull();
        image!.Url.Should().BeNull();
    }

    [Fact]
    public void InsertBlock_FloatingImage_CreatesParagraphAnchor()
    {
        var document = CreateDocument(Paragraph("p1", "Anchor paragraph"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Image",
            Block = FloatingImageBlock("img-1", DocumentWrapMode.Square),
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "p1" }
        };

        _applier.ApplyPatch(document, patch);

        var anchor = document.Anchors.Should().ContainSingle().Subject;
        anchor.Type.Should().Be(DocumentAnchorType.FloatingObject);
        anchor.BlockId.Should().Be("p1");
        anchor.ObjectBlockId.Should().Be("img-1");
        anchor.FloatingLayout.Should().NotBeNull();
        anchor.FloatingLayout!.WrapMode.Should().Be(DocumentWrapMode.Square);
    }

    [Fact]
    public void InsertBlock_FloatingImageAnchor_SurvivesSaveLoad()
    {
        var document = CreateDocument(Paragraph("p1", "Anchor paragraph"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Image",
            Block = FloatingImageBlock("img-1", DocumentWrapMode.Square),
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "p1" }
        };

        _applier.ApplyPatch(document, patch);

        var json = System.Text.Json.JsonSerializer.Serialize(document);
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<DocumentEditorDocument>(json)!;

        var anchor = reloaded.Anchors.Should().ContainSingle().Subject;
        anchor.BlockId.Should().Be("p1");
        anchor.ObjectBlockId.Should().Be("img-1");
        anchor.FloatingLayout!.Inline.Should().BeFalse();
        anchor.FloatingLayout.WrapMode.Should().Be(DocumentWrapMode.Square);
    }

    [Theory]
    [InlineData(DocumentRelativePosition.Page, DocumentRelativePosition.Page)]
    [InlineData(DocumentRelativePosition.Margin, DocumentRelativePosition.Margin)]
    [InlineData(DocumentRelativePosition.Paragraph, DocumentRelativePosition.Paragraph)]
    public void InsertBlock_FloatingImage_PreservesPositionReferenceFrame(
        DocumentRelativePosition horizontalRelativeTo,
        DocumentRelativePosition verticalRelativeTo)
    {
        var document = CreateDocument(Paragraph("p1", "Anchor paragraph"));
        var block = FloatingImageBlock("img-1", DocumentWrapMode.Square);
        var image = (ImageBlockContent)block.Content;
        image.FloatingLayout!.HorizontalRelativeTo = horizontalRelativeTo;
        image.FloatingLayout.VerticalRelativeTo = verticalRelativeTo;
        image.FloatingLayout.X = 36;
        image.FloatingLayout.Y = 48;

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Image",
            Block = block,
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "p1" }
        });

        var layout = document.Anchors.Single().FloatingLayout!;
        layout.HorizontalRelativeTo.Should().Be(horizontalRelativeTo);
        layout.VerticalRelativeTo.Should().Be(verticalRelativeTo);
        layout.X.Should().Be(36);
        layout.Y.Should().Be(48);
    }

    [Theory]
    [InlineData(DocumentWrapMode.Square)]
    [InlineData(DocumentWrapMode.TopBottom)]
    [InlineData(DocumentWrapMode.BehindText)]
    [InlineData(DocumentWrapMode.InFrontOfText)]
    public void UpdateBlock_FloatingImage_PreservesSupportedWrapMode(DocumentWrapMode wrapMode)
    {
        var imageBlock = FloatingImageBlock("img-1", DocumentWrapMode.Square);
        var document = CreateDocument(Paragraph("p1", "Anchor paragraph"), imageBlock);
        var updated = FloatingImageBlock("img-1", wrapMode);

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = updated,
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "p1" }
        });

        var image = (ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content;
        image.FloatingLayout!.WrapMode.Should().Be(wrapMode);
        document.Anchors.Single().FloatingLayout!.WrapMode.Should().Be(wrapMode);
    }

    [Fact]
    public void UpdateBlock_FloatingImage_LockAnchorKeepsExistingParagraphAnchor()
    {
        var imageBlock = FloatingImageBlock("img-1", DocumentWrapMode.Square);
        var document = CreateDocument(Paragraph("p1", "Original anchor"), Paragraph("p2", "New selection"), imageBlock);
        document.Anchors.Add(new DocumentAnchor
        {
            Type = DocumentAnchorType.FloatingObject,
            BlockId = "p1",
            ObjectBlockId = "img-1",
            FloatingLayout = ((ImageBlockContent)imageBlock.Content).FloatingLayout
        });
        var updated = FloatingImageBlock("img-1", DocumentWrapMode.Square);
        ((ImageBlockContent)updated.Content).FloatingLayout!.LockAnchor = true;

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = updated,
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "p2" }
        });

        document.Anchors.Single().BlockId.Should().Be("p1");
        document.Anchors.Single().ObjectBlockId.Should().Be("img-1");
        document.Anchors.Single().FloatingLayout!.LockAnchor.Should().BeTrue();
    }

    // ── UpdateBlock ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateBlock_ChangesBlockContent()
    {
        var document = CreateDocument(Paragraph("b1", "Old text"));
        var patch = new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = new DocumentBlock
            {
                Id = "b1",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = "New text" }]
                }
            }
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("New text");
    }

    [Fact]
    public void MoveBlock_MovesImageWithoutLosingMetadata()
    {
        var image = new DocumentBlock
        {
            Id = "img-1",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = "asset-1",
                Url = "/assets/1.png",
                AltText = "Evidence",
                Size = new DocumentImageSize { Width = 320, Height = 180 }
            }
        };
        var document = CreateDocument(
            Paragraph("p1", "Before"),
            image,
            Paragraph("p2", "After"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "MoveBlock",
            Block = new DocumentBlock
            {
                Id = "img-1",
                Type = DocumentBlockType.Image,
                Order = 35,
                Content = image.Content
            },
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "img-1" }
        });

        document.Blocks.Select(block => block.Id).Should().ContainInOrder("p1", "p2", "img-1");
        var moved = document.Blocks.Single(block => block.Id == "img-1");
        var content = moved.Content.Should().BeOfType<ImageBlockContent>().Subject;
        content.AssetId.Should().Be("asset-1");
        content.AltText.Should().Be("Evidence");
        content.Size.Width.Should().Be(320);
    }

    // ── RemoveBlock ──────────────────────────────────────────────────────────

    [Fact]
    public void RemoveBlock_DeletesBlock()
    {
        var document = CreateDocument(
            Paragraph("b1", "First"),
            Paragraph("b2", "Second"));
        var patch = new WysiwygPatch
        {
            Type = "RemoveBlock",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorOffset = 0
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().ContainSingle();
        document.Blocks[0].Id.Should().Be("b2");
    }

    // ── InsertParagraph ──────────────────────────────────────────────────────

    [Fact]
    public void SplitBlock_SplitsMiddleParagraphIntoTwoParagraphs()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "SplitBlock",
            Block = new DocumentBlock
            {
                Id = "b2",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Id = "i-b2", Text = string.Empty }] }
            },
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Id.Should().Be("b2");
        GetInlineText(document, "b1").Should().Be("Hello ");
        GetInlineText(document, "b2").Should().Be("world");
        GetInlines(document, "b2")[0].Id.Should().Be("i-b2");
    }

    [Fact]
    public void SplitBlock_PreservesTypingMarksButDoesNotSpillRevisionMarksToEmptyParagraph()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var run = (TextRun)GetInlines(document, "b1")[0];
        run.Marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        run.Marks.Add(new InlineMark { Type = InlineMarkType.Revision, RevisionId = "rev-1", Value = "Insertion" });
        var patch = new WysiwygPatch
        {
            Type = "SplitBlock",
            Block = new DocumentBlock
            {
                Id = "b2",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Id = "i-b2", Text = string.Empty }] }
            },
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            }
        };

        _applier.ApplyPatch(document, patch);

        var newRun = (TextRun)GetInlines(document, "b2")[0];
        newRun.Text.Should().BeEmpty();
        newRun.Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
        newRun.Marks.Should().NotContain(mark => mark.Type == InlineMarkType.Revision);
    }

    // ── InsertLineBreak ──────────────────────────────────────────────────────

    [Fact]
    public void InsertSoftBreak_KeepsOneBlockAndInsertsBreakInline()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "InsertSoftBreak",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().ContainSingle();
        GetInlineText(document, "b1").Should().Be("Hello \nworld");
    }

    [Fact]
    public void DeleteContentBackward_AtBeginningParagraph_MergesWithPreviousParagraph()
    {
        var document = CreateDocument(
            Paragraph("b1", "Hello "),
            Paragraph("b2", "world"));
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentBackward",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b2",
                AnchorInlineId = "i-b2",
                AnchorOffset = 0
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().ContainSingle();
        GetInlineText(document, "b1").Should().Be("Hello world");
    }

    [Fact]
    public void DeleteContentForward_BeforeSoftBreak_RemovesOnlySoftBreak()
    {
        var document = CreateDocument(Paragraph("b1", "Hello\nworld"));
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentForward",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().ContainSingle();
        GetInlineText(document, "b1").Should().Be("Helloworld");
    }

    // ── Protocol versioning ──────────────────────────────────────────────────

    [Fact]
    public void Patch_UnsupportedVersion_Throws()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "!",
            ProtocolVersion = 999,
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            }
        };

        var act = () => _applier.ApplyPatch(document, patch);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*not supported*");
    }

    [Fact]
    public void Patch_OlderVersion_UpgradesGracefully()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "!",
            ProtocolVersion = 0,
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 5
            }
        };

        _applier.ApplyPatch(document, patch);

        GetInlineText(document, "b1").Should().Be("Hello!");
    }

    [Fact]
    public void Patch_NullDocument_Throws()
    {
        var act = () => _applier.ApplyPatch(null!, new WysiwygPatch { Type = "InsertText" });
        act.Should().Throw<ArgumentNullException>().WithParameterName("document");
    }

    [Fact]
    public void Patch_NullPatch_Throws()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var act = () => _applier.ApplyPatch(document, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("patch");
    }

    [Fact]
    public void Patch_UnknownType_Throws()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var patch = new WysiwygPatch { Type = "UnknownType" };
        var act = () => _applier.ApplyPatch(document, patch);
        act.Should().Throw<ArgumentException>().WithMessage("*Unknown patch type*");
    }

    // ── Phase 13: Table patches ──────────────────────────────────────────────

    [Fact]
    public void InsertBlock_Table_InsertsTableBlock()
    {
        var document = CreateDocument(Paragraph("b1", "Before"));
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Table",
            Block = new DocumentBlock
            {
                Id = "tbl-1",
                Type = DocumentBlockType.Table,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "c1", ColumnSpan = 1, RowSpan = 1, Blocks = [Paragraph("bp1", "A1")] },
                                new TableCellContent { Id = "c2", ColumnSpan = 1, RowSpan = 1, Blocks = [Paragraph("bp2", "B1")] }
                            ]
                        }
                    ]
                }
            },
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "b1" }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Type.Should().Be(DocumentBlockType.Table);
        var table = document.Blocks[1].Content as TableBlockContent;
        table!.Rows.Should().HaveCount(1);
        table.Rows[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateBlock_Table_UpdatesTableContent()
    {
        var document = CreateDocument(new DocumentBlock
        {
            Id = "tbl-1",
            Type = DocumentBlockType.Table,
            Order = 10,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent { Id = "c1", ColumnSpan = 1, RowSpan = 1, Blocks = [Paragraph("bp1", "A1")] }
                        ]
                    }
                ]
            }
        });

        var patch = new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = new DocumentBlock
            {
                Id = "tbl-1",
                Type = DocumentBlockType.Table,
                Order = 0,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "c1", ColumnSpan = 2, RowSpan = 1, Blocks = [Paragraph("bp1", "Merged")] }
                            ]
                        }
                    ]
                }
            }
        };

        _applier.ApplyPatch(document, patch);

        var table = document.Blocks[0].Content as TableBlockContent;
        table!.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
        var para = table.Rows[0].Cells[0].Blocks[0].Content as ParagraphBlockContent;
        var textRun = para!.Inlines[0] as TextRun;
        textRun!.Text.Should().Be("Merged");
    }

    [Fact]
    public void UpdateBlock_Table_WithZeroOrder_PreservesExistingOrder()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "tbl-1",
            Type = DocumentBlockType.Table,
            Order = 42,
            Content = new TableBlockContent
            {
                Rows = [new TableRowContent { Cells = [new TableCellContent { Id = "c1", Blocks = [] }] }]
            }
        });

        var patch = new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = new DocumentBlock
            {
                Id = "tbl-1",
                Type = DocumentBlockType.Table,
                Order = 0,
                Content = new TableBlockContent
                {
                    Rows = [new TableRowContent { Cells = [new TableCellContent { Id = "c1", Blocks = [] }] }]
                }
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks[0].Order.Should().Be(42);
    }

    [Fact]
    public void UpdateBlock_Table_WhenInsertPatchIsLate_InsertsTableBlock()
    {
        var document = CreateDocument(Paragraph("b1", "Before"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = new DocumentBlock
            {
                Id = "tbl-late",
                Type = DocumentBlockType.Table,
                Order = 0,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "c1", Blocks = [Paragraph("cell-1", "A1")] }
                            ]
                        }
                    ]
                }
            }
        });

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Id.Should().Be("tbl-late");
        document.Blocks[1].Order.Should().BeGreaterThan(document.Blocks[0].Order);
    }

    [Fact]
    public void InsertBlock_Table_WhenUpdatePatchAlreadyInsertedBlock_DoesNotDuplicate()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "tbl-1",
            Type = DocumentBlockType.Table,
            Order = 42,
            Content = new TableBlockContent
            {
                Rows = [new TableRowContent { Cells = [new TableCellContent { Id = "c1", Blocks = [Paragraph("cell-1", "A1")] }] }]
            }
        });

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "InsertBlock",
            BlockType = "Table",
            Block = new DocumentBlock
            {
                Id = "tbl-1",
                Type = DocumentBlockType.Table,
                Order = 0,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "c1", Blocks = [Paragraph("cell-1", "A1")] },
                                new TableCellContent { Id = "c2", Blocks = [Paragraph("cell-2", "B1")] }
                            ]
                        }
                    ]
                }
            }
        });

        document.Blocks.Should().ContainSingle(block => block.Id == "tbl-1");
        document.Blocks[0].Order.Should().Be(42);
        var table = (TableBlockContent)document.Blocks[0].Content;
        table.Rows[0].Cells.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateBlock_Table_RowAndColumnInsertPreservesExistingCellContent()
    {
        var document = CreateDocument(new DocumentBlock
        {
            Id = "tbl-1",
            Type = DocumentBlockType.Table,
            Order = 10,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent { Id = "a1", Blocks = [Paragraph("a1-block", "A1")] },
                            new TableCellContent { Id = "b1", Blocks = [Paragraph("b1-block", "B1")] }
                        ]
                    }
                ]
            }
        });

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "UpdateBlock",
            Block = new DocumentBlock
            {
                Id = "tbl-1",
                Type = DocumentBlockType.Table,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "a1", Blocks = [Paragraph("a1-block", "A1")] },
                                new TableCellContent { Id = "new-col-1", Blocks = [Paragraph("new-col-1-block", "")] },
                                new TableCellContent { Id = "b1", Blocks = [Paragraph("b1-block", "B1")] }
                            ]
                        },
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "a2", Blocks = [Paragraph("a2-block", "")] },
                                new TableCellContent { Id = "new-col-2", Blocks = [Paragraph("new-col-2-block", "")] },
                                new TableCellContent { Id = "b2", Blocks = [Paragraph("b2-block", "")] }
                            ]
                        }
                    ]
                }
            }
        });

        var table = (TableBlockContent)document.Blocks[0].Content;
        table.Rows.Should().HaveCount(2);
        table.Rows[0].Cells.Should().HaveCount(3);
        ReadCellText(table.Rows[0].Cells[0]).Should().Be("A1");
        ReadCellText(table.Rows[0].Cells[2]).Should().Be("B1");
    }

    [Fact]
    public void RemoveBlock_Table_RemovesTableBlock()
    {
        var document = CreateDocument(new DocumentBlock
        {
            Id = "tbl-1",
            Type = DocumentBlockType.Table,
            Order = 10,
            Content = new TableBlockContent
            {
                Rows = [new TableRowContent { Cells = [new TableCellContent { Id = "c1", Blocks = [] }] }]
            }
        });

        var patch = new WysiwygPatch
        {
            Type = "RemoveBlock",
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "tbl-1" }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void SetMarks_FontFamily_AppliesValueOnlyToSelection()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontFamily),
            Data = "Georgia, serif",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        var inlines = GetInlines(document, "b1").OfType<TextRun>().ToList();
        inlines.Should().HaveCount(2);
        inlines[0].Text.Should().Be("Hello");
        inlines[0].Marks.Should().ContainSingle(mark =>
            mark.Type == InlineMarkType.FontFamily && mark.Value == "Georgia, serif");
        inlines[1].Text.Should().Be(" world");
        inlines[1].Marks.Should().BeEmpty();
    }

    [Fact]
    public void SetMarks_FontSize_ReplacesExistingSizeValue()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontSize),
            Data = "12pt",
            Selection = Selection("b1", "i-b1", 0, 5)
        });
        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontSize),
            Data = "18pt",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        var run = GetInlines(document, "b1").OfType<TextRun>().Single();
        run.Marks.Should().ContainSingle(mark =>
            mark.Type == InlineMarkType.FontSize && mark.Value == "18pt");
    }

    [Fact]
    public void SetMarks_FontSize_PreservesExistingFontFamily()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontFamily),
            Data = "Georgia, serif",
            Selection = Selection("b1", "i-b1", 0, 5)
        });
        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontSize),
            Data = "24pt",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        var run = GetInlines(document, "b1").OfType<TextRun>().Single();
        run.Marks.Should().Contain(mark =>
            mark.Type == InlineMarkType.FontFamily && mark.Value == "Georgia, serif");
        run.Marks.Should().Contain(mark =>
            mark.Type == InlineMarkType.FontSize && mark.Value == "24pt");
    }

    [Fact]
    public void SetMarks_FontSize_UsesBlockOffsetsAfterInlineSplit()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontFamily),
            Data = "Georgia, serif",
            Selection = Selection("b1", "i-b1", 3, 8)
        });

        var markedRun = GetInlines(document, "b1")
            .OfType<TextRun>()
            .Single(run => run.Text == "lo wo");

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.FontSize),
            Data = "24pt",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = markedRun.Id,
                AnchorOffset = 0,
                AnchorBlockOffset = 3,
                FocusBlockId = "b1",
                FocusInlineId = markedRun.Id,
                FocusOffset = markedRun.Text.Length,
                FocusBlockOffset = 8,
                IsCollapsed = false
            }
        });

        markedRun = GetInlines(document, "b1")
            .OfType<TextRun>()
            .Single(run => run.Text == "lo wo");
        markedRun.Marks.Should().Contain(mark =>
            mark.Type == InlineMarkType.FontFamily && mark.Value == "Georgia, serif");
        markedRun.Marks.Should().Contain(mark =>
            mark.Type == InlineMarkType.FontSize && mark.Value == "24pt");
        GetInlines(document, "b1").OfType<TextRun>().Single(run => run.Text == "Hel").Marks.Should().BeEmpty();
        GetInlines(document, "b1").OfType<TextRun>().Single(run => run.Text == "rld").Marks.Should().BeEmpty();
    }

    [Fact]
    public void SetMarks_Link_PersistsSafeHrefAndTitle()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.Link),
            Data = " https://example.test/doc ",
            LinkTitle = "Document",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        var run = GetInlines(document, "b1").OfType<TextRun>().Single();
        run.Marks.Should().ContainSingle(mark =>
            mark.Type == InlineMarkType.Link
            && mark.Link != null
            && mark.Link.Href == "https://example.test/doc"
            && mark.Link.Title == "Document");
    }

    [Fact]
    public void SetMarks_Link_RejectsUnsafeHref()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = nameof(InlineMarkType.Link),
            Data = "javascript:alert(1)",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        var run = GetInlines(document, "b1").OfType<TextRun>().Single();
        run.Marks.Should().NotContain(mark => mark.Type == InlineMarkType.Link);
    }

    [Fact]
    public void ClearFormatting_RemovesInlineFormattingAndLinkButKeepsRevision()
    {
        var document = CreateDocument(Paragraph("b1", "Hello"));
        var run = GetInlines(document, "b1").OfType<TextRun>().Single();
        run.Marks =
        [
            new InlineMark { Type = InlineMarkType.Bold },
            new InlineMark { Type = InlineMarkType.FontFamily, Value = "Georgia, serif" },
            new InlineMark { Type = InlineMarkType.TextColor, Value = "#123456" },
            new InlineMark { Type = InlineMarkType.Highlight, Value = "#fff59d" },
            new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = "https://example.test" } },
            new InlineMark { Type = InlineMarkType.Revision, RevisionId = "rev-1", Value = "Insertion" }
        ];

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "ClearFormatting",
            Selection = Selection("b1", "i-b1", 0, 5)
        });

        run = GetInlines(document, "b1").OfType<TextRun>().Single();
        var removedTypes = new[]
        {
            InlineMarkType.Bold,
            InlineMarkType.FontFamily,
            InlineMarkType.TextColor,
            InlineMarkType.Highlight,
            InlineMarkType.Link
        };
        run.Marks.Should().NotContain(mark => removedTypes.Contains(mark.Type));
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Revision);
    }

    [Fact]
    public void SetParagraphProperties_Alignment_ChangesOnlyActiveBlock()
    {
        var document = CreateDocument(
            Paragraph("b1", "First"),
            Paragraph("b2", "Second"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetParagraphProperties",
            ParagraphProperties = new DocumentParagraphPropertiesPatch
            {
                Alignment = DocumentTextAlignment.Center
            },
            Selection = Selection("b1", "i-b1", 0, 0)
        });

        document.Blocks[0].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Center);
        document.Blocks[1].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Left);
    }

    [Fact]
    public void SetParagraphProperties_Alignment_AppliesToMultiBlockSelection()
    {
        var document = CreateDocument(
            Paragraph("b1", "First"),
            Paragraph("b2", "Second"),
            Paragraph("b3", "Third"));

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetParagraphProperties",
            ParagraphProperties = new DocumentParagraphPropertiesPatch
            {
                Alignment = DocumentTextAlignment.Right
            },
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 0,
                FocusBlockId = "b2",
                FocusInlineId = "i-b2",
                FocusOffset = 3,
                IsCollapsed = false
            }
        });

        document.Blocks[0].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Right);
        document.Blocks[1].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Right);
        document.Blocks[2].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Left);
    }

    [Fact]
    public void SetParagraphProperties_WithFooterRegion_UpdatesFooterParagraphOnly()
    {
        var document = CreateDocument(Paragraph("body-1", "Body text"));
        var footer = AddHeaderFooter(document, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.Primary, "footer-primary", "footer-block", "Footer");

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetParagraphProperties",
            ParagraphProperties = new DocumentParagraphPropertiesPatch
            {
                Alignment = DocumentTextAlignment.Center
            },
            Selection = new WysiwygSelectionSnapshot
            {
                Region = "Footer",
                HeaderFooterId = footer.Id,
                AnchorBlockId = "footer-block",
                AnchorInlineId = "i-footer-block",
                AnchorOffset = 0
            }
        });

        document.Blocks[0].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Left);
        footer.Blocks[0].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Center);
    }

    [Fact]
    public void SetParagraphProperties_LineSpacingSpacingAndIndent_AreClampedAndApplied()
    {
        var document = CreateDocument(Paragraph("b1", "First"));
        document.Blocks[0].ParagraphProperties.LeftIndent = 18;

        _applier.ApplyPatch(document, new WysiwygPatch
        {
            Type = "SetParagraphProperties",
            ParagraphProperties = new DocumentParagraphPropertiesPatch
            {
                LineSpacing = 1.5,
                SpacingBefore = 12,
                SpacingAfter = 999,
                LeftIndentDelta = 36,
                FirstLineIndent = -24
            },
            Selection = Selection("b1", "i-b1", 0, 0)
        });

        document.Blocks[0].ParagraphProperties.LineSpacing.Should().Be(1.5);
        document.Blocks[0].ParagraphProperties.SpacingBefore.Should().Be(12);
        document.Blocks[0].ParagraphProperties.SpacingAfter.Should().Be(144);
        document.Blocks[0].ParagraphProperties.LeftIndent.Should().Be(54);
        document.Blocks[0].ParagraphProperties.FirstLineIndent.Should().Be(-24);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetInlineText(DocumentEditorDocument document, string blockId)
    {
        var block = document.Blocks.FirstOrDefault(b => b.Id == blockId);
        var inlines = block?.Content switch
        {
            ParagraphBlockContent p => p.Inlines,
            HeadingBlockContent h => h.Inlines,
            ListBlockContent l => l.Inlines,
            QuoteBlockContent q => q.Inlines,
            _ => null
        };
        return string.Concat(inlines?.OfType<TextRun>().Select(t => t.Text) ?? Array.Empty<string>());
    }

    private static List<InlineContent> GetInlines(DocumentEditorDocument document, string blockId)
    {
        var block = document.Blocks.FirstOrDefault(b => b.Id == blockId);
        return block?.Content switch
        {
            ParagraphBlockContent p => p.Inlines,
            HeadingBlockContent h => h.Inlines,
            ListBlockContent l => l.Inlines,
            QuoteBlockContent q => q.Inlines,
            _ => new List<InlineContent>()
        };
    }

    private static string ReadCellText(TableCellContent cell)
        => string.Concat(cell.Blocks.Select(block => block.Content switch
        {
            ParagraphBlockContent p => string.Concat(p.Inlines.OfType<TextRun>().Select(run => run.Text)),
            HeadingBlockContent h => string.Concat(h.Inlines.OfType<TextRun>().Select(run => run.Text)),
            ListBlockContent l => string.Concat(l.Inlines.OfType<TextRun>().Select(run => run.Text)),
            QuoteBlockContent q => string.Concat(q.Inlines.OfType<TextRun>().Select(run => run.Text)),
            _ => string.Empty
        }));

    private static WysiwygSelectionSnapshot Selection(string blockId, string inlineId, int start, int end)
        => new()
        {
            AnchorBlockId = blockId,
            AnchorInlineId = inlineId,
            AnchorOffset = start,
            FocusBlockId = blockId,
            FocusInlineId = inlineId,
            FocusOffset = end,
            IsCollapsed = start == end
        };

    private static DocumentHeaderFooter AddHeaderFooter(
        DocumentEditorDocument document,
        DocumentHeaderFooterType type,
        DocumentHeaderFooterScope scope,
        string id,
        string blockId,
        string text)
    {
        var headerFooter = new DocumentHeaderFooter
        {
            Id = id,
            Type = type,
            Scope = scope,
            SectionId = document.Sections[0].Id,
            Blocks = [Paragraph(blockId, text)]
        };
        document.HeadersFooters.Add(headerFooter);
        document.Sections[0].Properties.HeaderFooterReferences.Add(new DocumentHeaderFooterReference
        {
            HeaderFooterId = id,
            Type = type,
            Scope = scope
        });
        return headerFooter;
    }

    private static string GetHeaderFooterInlineText(DocumentHeaderFooter headerFooter, string blockId)
    {
        var block = headerFooter.Blocks.FirstOrDefault(b => b.Id == blockId);
        var inlines = block?.Content switch
        {
            ParagraphBlockContent p => p.Inlines,
            HeadingBlockContent h => h.Inlines,
            ListBlockContent l => l.Inlines,
            QuoteBlockContent q => q.Inlines,
            _ => null
        };
        return string.Concat(inlines?.OfType<TextRun>().Select(t => t.Text) ?? Array.Empty<string>());
    }
}
