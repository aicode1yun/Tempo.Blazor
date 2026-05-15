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
    public void InsertParagraph_CreatesNewParagraphWithText()
    {
        var document = CreateDocument(Paragraph("b1", "First"));
        var patch = new WysiwygPatch
        {
            Type = "InsertParagraph",
            Data = "Second",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorOffset = 0
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        GetInlineText(document, document.Blocks[1].Id).Should().Be("Second");
    }

    // ── InsertLineBreak ──────────────────────────────────────────────────────

    [Fact]
    public void InsertLineBreak_SplitsBlockAtOffset()
    {
        var document = CreateDocument(Paragraph("b1", "Hello world"));
        var patch = new WysiwygPatch
        {
            Type = "InsertLineBreak",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i-b1",
                AnchorOffset = 6
            }
        };

        _applier.ApplyPatch(document, patch);

        document.Blocks.Should().HaveCount(2);
        GetInlineText(document, "b1").Should().Be("Hello ");
        GetInlineText(document, document.Blocks[1].Id).Should().Be("world");
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
}
