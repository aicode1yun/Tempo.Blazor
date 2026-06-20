using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentEditorModelTests
{
    [Fact]
    public void Empty_CreatesDocumentWithSchemaVersionAndDefaultSection()
    {
        var document = DocumentEditorDocument.Empty("doc-1");

        document.DocumentId.Should().Be("doc-1");
        document.SchemaVersion.Should().Be(DocumentEditorDocument.CurrentSchemaVersion);
        document.Metadata.Status.Should().Be(DocumentEditorStatus.Draft);
        document.PageSettings.Size.Name.Should().Be("A4");
        document.PageSettings.Margins.Left.Should().Be(72);
        document.Theme.BodyFontFamily.Should().Contain("Aptos");
        document.Theme.BodyFontSize.Should().Be(11);
        document.Theme.BodyLineHeight.Should().Be(1.15);
        document.Theme.ParagraphSpacingAfter.Should().Be(8);
        document.Sections.Should().ContainSingle();
        document.Sections[0].Properties.NoteNumbering.RestartEachSection.Should().BeTrue();
    }

    [Fact]
    public void DocumentJson_RoundtripsThemeDefaults()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Georgia, serif",
            BodyFontSize = 12,
            BodyLineHeight = 1.35,
            ParagraphSpacingAfter = 10
        };

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        json.Should().Contain("Theme");
        restored.Theme.BodyFontFamily.Should().Be("Georgia, serif");
        restored.Theme.BodyFontSize.Should().Be(12);
        restored.Theme.BodyLineHeight.Should().Be(1.35);
        restored.Theme.ParagraphSpacingAfter.Should().Be(10);
    }

    [Fact]
    public void Document_SerializesBlockContentWithTypeDiscriminator()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Hello" },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" }
                ]
            }
        });

        var json = JsonSerializer.Serialize(document);
        var restored = JsonSerializer.Deserialize<DocumentEditorDocument>(json);

        json.Should().Contain("\"$type\":\"paragraph\"");
        json.Should().Contain("\"$type\":\"text\"");
        json.Should().Contain("\"$type\":\"token\"");
        restored!.Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>();
        ((ParagraphBlockContent)restored.Blocks[0].Content).Inlines[1].Should().BeOfType<TokenRun>();
    }

    [Fact]
    public void DocumentJson_RoundtripsParagraphProperties()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Justify,
                LineSpacing = 1.5,
                SpacingBefore = 6,
                SpacingAfter = 12,
                LeftIndent = 36,
                RightIndent = 18,
                FirstLineIndent = 12
            },
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Hello" }]
            }
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        json.Should().Contain("ParagraphProperties");
        restored.Blocks[0].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Justify);
        restored.Blocks[0].ParagraphProperties.LineSpacing.Should().Be(1.5);
        restored.Blocks[0].ParagraphProperties.LeftIndent.Should().Be(36);
        restored.Blocks[0].ParagraphProperties.FirstLineIndent.Should().Be(12);
    }

    [Fact]
    public void DocumentJson_RoundtripsDrawingRunObjectLayoutAndRestrictedMarkers()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.IsProtected = true;
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "inline-1", Text = "Editable protected text" },
                    new DocumentDrawingRun
                    {
                        Id = "drawing-inline-1",
                        ObjectId = "image-1",
                        Source = DocumentImageSource.Url,
                        Url = "/image.png",
                        Layout = new DocumentObjectLayout
                        {
                            Kind = DocumentObjectLayoutKind.Anchored,
                            Anchor = new DocumentObjectAnchor
                            {
                                BlockId = "paragraph-1",
                                InlineIndex = 0,
                                Offset = 4,
                                Region = DocumentRenditionAnchorScope.Body,
                                PageIndex = 2,
                                LockAnchor = true
                            },
                            Position = new DocumentObjectPosition
                            {
                                HorizontalAlignment = DocumentImageHorizontalPosition.Right
                            },
                            Wrap = new DocumentObjectWrap
                            {
                                Mode = DocumentWrapMode.Square,
                                DistanceLeft = 12,
                                DistanceRight = 4,
                                DistanceTop = 2,
                                DistanceBottom = 8
                            }
                        }
                    }
                ]
            }
        });
        document.RestrictedMarkers.Add(new DocumentRestrictedMarker
        {
            Id = "marker-1",
            StartBlockId = "paragraph-1",
            StartOffset = 0,
            EndBlockId = "paragraph-1",
            EndOffset = 8,
            Label = "Editable"
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        restored.IsProtected.Should().BeTrue();
        restored.RestrictedMarkers.Should().ContainSingle(marker =>
            marker.Id == "marker-1"
            && marker.StartBlockId == "paragraph-1"
            && marker.EndOffset == 8
            && marker.Label == "Editable");
        restored.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var restoredImage = ((ParagraphBlockContent)restored.Blocks.Single().Content).Inlines
            .OfType<DocumentDrawingRun>()
            .Single();
        json.Should().Contain("\"Layout\"");
        json.Should().Contain("\"$type\":\"drawing\"");
        json.Should().NotContain("FloatingLayout");
        restoredImage.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Anchored);
        restoredImage.Layout.Anchor.BlockId.Should().Be("paragraph-1");
        restoredImage.Layout.Anchor.InlineIndex.Should().Be(0);
        restoredImage.Layout.Anchor.Offset.Should().Be(4);
        restoredImage.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Body);
        restoredImage.Layout.Anchor.PageIndex.Should().BeNull();
        restoredImage.Layout.Anchor.LockAnchor.Should().BeTrue();
        restoredImage.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        restoredImage.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Right);
        restoredImage.Layout.Wrap.DistanceLeft.Should().Be(12);
        restoredImage.Layout.Wrap.DistanceRight.Should().Be(4);
        json.Should().NotContain("PageIndex");
    }

    [Fact]
    public void DocumentDrawingRun_DefaultLayout_IsInlineWithSeparatedNaturalAndTransformSizes()
    {
        var image = new DocumentDrawingRun
        {
            NaturalSize = new DocumentImageSize { Width = 640, Height = 360 },
            Layout = DocumentObjectLayout.Inline()
        };

        image.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Inline);
        image.Layout.IsInline.Should().BeTrue();
        image.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Inline);
        image.Layout.Anchor.MoveWithText.Should().BeTrue();
        image.Layout.Transform.LockAspectRatio.Should().BeTrue();
        image.NaturalSize.Width.Should().Be(640);
        image.Layout.Transform.Width.Should().BeNull();
    }

    [Fact]
    public void DocumentObjectLayout_AnchoredSquareLeft_StoresPositionWrapAndTransform()
    {
        var layout = DocumentObjectLayout.Anchored(DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left);
        layout.Transform.Width = 220;
        layout.Transform.Height = 124;
        layout.Wrap.DistanceRight = 16;

        layout.Kind.Should().Be(DocumentObjectLayoutKind.Anchored);
        layout.IsInline.Should().BeFalse();
        layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Left);
        layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        layout.Wrap.DistanceRight.Should().Be(16);
        layout.Transform.Width.Should().Be(220);
        layout.Transform.Height.Should().Be(124);
    }

    [Fact]
    public void DocumentObjectLayout_FixedBehindText_StoresFixedAnchorAndStacking()
    {
        var layout = DocumentObjectLayout.Fixed();
        layout.Wrap.Mode = DocumentWrapMode.BehindText;
        layout.Stacking.ZIndex = 3;
        layout.Stacking.AllowOverlap = true;

        layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        layout.Anchor.MoveWithText.Should().BeFalse();
        layout.Anchor.FixedOnPage.Should().BeTrue();
        layout.Wrap.Mode.Should().Be(DocumentWrapMode.BehindText);
        layout.Stacking.ZIndex.Should().Be(3);
        layout.Stacking.AllowOverlap.Should().BeTrue();
    }

    [Fact]
    public void Phase19_DocumentJson_RoundtripsDrawingRunTableAndCellPropertiesWithoutViewOnlyState()
    {
        var document = DocumentEditorDocument.Empty("phase19-model");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-with-drawing",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Architecture " },
                    new DocumentDrawingRun
                    {
                        ObjectId = "image-1",
                        Source = DocumentImageSource.Url,
                        Url = "https://cdn.test/image.png",
                        AltText = "Diagram",
                        Caption = "Architecture caption",
                        LinkUrl = "https://example.test/diagram",
                        Size = new DocumentImageSize { Width = 320, Height = 180, LockAspectRatio = true },
                        Layout = new DocumentObjectLayout
                        {
                            Kind = DocumentObjectLayoutKind.Anchored,
                            Position = new DocumentObjectPosition { HorizontalAlignment = DocumentImageHorizontalPosition.Right },
                            Wrap = new DocumentObjectWrap
                            {
                                Mode = DocumentWrapMode.Square,
                                DistanceLeft = 12,
                                DistanceRight = 6
                            }
                        }
                    }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "table-1",
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Layout = new TableLayoutContent
                {
                    Width = 420,
                    Alignment = TableHorizontalAlignment.Center,
                    CellPadding = 8,
                    BackgroundColor = "#f8fafc",
                    Borders = new TableCellBorders { Top = "1px solid #111827", Bottom = "1px solid #111827" }
                },
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "cell-1",
                                IsHeader = true,
                                Width = 140,
                                BackgroundColor = "#ffef9a",
                                VerticalAlignment = TableCellVerticalAlignment.Middle,
                                Padding = 10,
                                Borders = new TableCellBorders { Left = "2px solid #0f172a" },
                                Blocks = [CreateParagraph("cell-p", "Cell text")]
                            }
                        ]
                    }
                ]
            }
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        restored.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var image = ((ParagraphBlockContent)restored.Blocks[0].Content).Inlines.OfType<DocumentDrawingRun>().Single();
        image.Caption.Should().Be("Architecture caption");
        image.LinkUrl.Should().Be("https://example.test/diagram");
        image.Size.Width.Should().Be(320);
        image.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        image.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Right);

        var table = restored.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Layout.Width.Should().Be(420);
        table.Layout.Alignment.Should().Be(TableHorizontalAlignment.Center);
        table.Layout.CellPadding.Should().Be(8);
        table.Layout.BackgroundColor.Should().Be("#f8fafc");
        var cell = table.Rows[0].Cells[0];
        cell.IsHeader.Should().BeTrue();
        cell.Width.Should().Be(140);
        cell.BackgroundColor.Should().Be("#ffef9a");
        cell.VerticalAlignment.Should().Be(TableCellVerticalAlignment.Middle);
        cell.Padding.Should().Be(10);

        json.Should().NotContain("showNonPrintingCharacters");
        json.Should().NotContain("ShowNonPrintingCharacters");
    }

    [Fact]
    public void Phase19_DocumentJson_NormalizeRepairsNullRestrictedMarkersAndExcludesTransientMarkersFromContentJson()
    {
        const string json = """
            {
              "SchemaVersion": 1,
              "DocumentId": "phase19-null-markers",
              "RestrictedMarkers": null,
              "Blocks": [
                {
                  "Type": 0,
                  "Content": { "$type": "paragraph", "Inlines": [] }
                }
              ]
            }
            """;
        var store = new DocumentMarkerStore();
        store.Add(new DocumentMarker
        {
            Id = "search-transient",
            Type = DocumentMarkerType.Search,
            AffectsData = false,
            Source = DocumentMarkerSource.Transient,
            Range = DocumentMarkerRange.InBlock("block-1", 0, 4)
        });
        store.Add(new DocumentMarker
        {
            Id = "comment-data",
            Type = DocumentMarkerType.Comment,
            AffectsData = true,
            Range = DocumentMarkerRange.InBlock("block-1", 0, 4)
        });

        var restored = DocumentEditorJson.Deserialize(json);
        var normalizedJson = DocumentEditorJson.Normalize(json);

        restored.RestrictedMarkers.Should().BeEmpty();
        normalizedJson.Should().NotContain("search-transient");
        store.GetPersistentMarkers().Select(marker => marker.Id).Should().Equal("comment-data");
    }

    [Fact]
    public void DocumentJson_RoundtripsEditingAndExportMetadata()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos",
            BodyFontSize = 12,
            BodyLineHeight = 1.25,
            ParagraphSpacingAfter = 9
        };
        document.Sections[0].Id = "section-1";
        document.Sections[0].Properties.DifferentFirstPage = true;
        document.Sections[0].Properties.HeaderFooterReferences =
        [
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "header-1",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.FirstPage
            },
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "footer-1",
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary
            }
        ];
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            SectionId = "section-1",
            Type = DocumentBlockType.Paragraph,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Center,
                LineSpacing = 1.5,
                SpacingAfter = 12
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "inline-1",
                        Text = "Styled revision",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.FontFamily, Value = "Georgia" },
                            new InlineMark { Type = InlineMarkType.FontSize, Value = "18pt" },
                            new InlineMark { Type = InlineMarkType.Revision, RevisionId = "revision-1", Value = "Insertion" }
                        ]
                    }
                ]
            }
        });
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines.Add(new DocumentDrawingRun
        {
            Id = "drawing-inline-1",
            ObjectId = "image-1",
            Source = DocumentImageSource.Asset,
            AssetId = "asset-1",
            AltText = "Diagram",
            Caption = "Architecture diagram",
            Size = new DocumentImageSize { Width = 240, Height = 120 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Position = new DocumentObjectPosition
                {
                    HorizontalRelativeTo = DocumentRelativePosition.Margin,
                    VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                    X = 36,
                    Y = 18
                },
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square },
                Stacking = new DocumentObjectStacking { ZIndex = 2 }
            }
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "header-1",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.FirstPage,
            Blocks = [CreateParagraph("header-block-1", "Header text")]
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "footer-1",
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks = [CreateParagraph("footer-block-1", "Footer text")]
        });
        document.Revisions.Add(new DocumentRevision
        {
            Id = "revision-1",
            Type = DocumentRevisionType.Insertion,
            Range = new DocumentRevisionRange { BlockId = "paragraph-1", StartInlineIndex = 0, EndInlineIndex = 0, StartOffset = 0, EndOffset = 15 },
            Author = new DocumentRevisionAuthor { Id = "author-1", DisplayName = "Reviewer" },
            CreatedAt = DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
            Action = DocumentRevisionAction.Pending
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        restored.Theme.BodyFontFamily.Should().Be("Aptos");
        restored.Sections[0].Properties.HeaderFooterReferences.Should().HaveCount(2);
        restored.Blocks[0].ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Center);
        var run = ((ParagraphBlockContent)restored.Blocks[0].Content).Inlines.OfType<TextRun>().Single();
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontFamily && mark.Value == "Georgia");
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.FontSize && mark.Value == "18pt");
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == "revision-1");
        restored.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var image = ((ParagraphBlockContent)restored.Blocks[0].Content).Inlines.OfType<DocumentDrawingRun>().Single();
        image.AssetId.Should().Be("asset-1");
        image.Size.Width.Should().Be(240);
        image.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        restored.HeadersFooters.Should().HaveCount(2);
        restored.Revisions.Should().ContainSingle(revision => revision.Id == "revision-1");
    }

    [Fact]
    public void DocumentJson_UpgradesLegacySnapshotAndRepairsMissingCollections()
    {
        const string json = """
            {
              "SchemaVersion": 0,
              "DocumentId": "legacy-doc",
              "Metadata": null,
              "PageSettings": null,
              "Theme": null,
              "Sections": null,
              "Blocks": [
                {
                  "Id": "",
                  "Type": 0,
                  "Order": 1,
                  "Content": { "$type": "paragraph", "Inlines": [] }
                }
              ],
              "Comments": null,
              "Notes": null,
              "HeadersFooters": null,
              "Revisions": null,
              "Assets": null,
              "Anchors": null
            }
            """;

        var document = DocumentEditorJson.Deserialize(json);
        var normalized = DocumentEditorJson.Normalize(json);

        document.SchemaVersion.Should().Be(DocumentEditorDocument.CurrentSchemaVersion);
        document.DocumentId.Should().Be("legacy-doc");
        document.Metadata.Should().NotBeNull();
        document.PageSettings.Should().NotBeNull();
        document.Theme.Should().NotBeNull();
        document.Theme.BodyFontSize.Should().Be(11);
        document.Sections.Should().ContainSingle();
        document.Blocks.Should().ContainSingle();
        document.Blocks[0].Id.Should().NotBeNullOrWhiteSpace();
        document.Blocks[0].Content.Should().BeOfType<ParagraphBlockContent>();
        document.Comments.Should().BeEmpty();
        document.Notes.Should().BeEmpty();
        document.HeadersFooters.Should().BeEmpty();
        document.Revisions.Should().BeEmpty();
        document.Assets.Should().BeEmpty();
        document.Anchors.Should().BeEmpty();
        normalized.Should().Contain("\"SchemaVersion\":1");
    }

    [Fact]
    public void DocumentJson_RejectsFutureSchemaVersion()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        var json = DocumentEditorJson.Serialize(document)
            .Replace("\"SchemaVersion\":1", "\"SchemaVersion\":999", StringComparison.Ordinal);

        FluentActions.Invoking(() => DocumentEditorJson.Deserialize(json))
            .Should()
            .Throw<JsonException>()
            .WithMessage("*Unsupported document editor schema version 999*");
    }

    [Fact]
    public void DocumentJson_RejectsUnknownBlockContentDiscriminator()
    {
        const string json = """
            {
              "SchemaVersion": 1,
              "DocumentId": "doc-1",
              "Blocks": [
                {
                  "Type": 0,
                  "Content": { "$type": "mystery", "Text": "Unsupported" }
                }
              ]
            }
            """;

        FluentActions.Invoking(() => DocumentEditorJson.Deserialize(json))
            .Should()
            .Throw<JsonException>();
    }

    [Fact]
    public void WysiwygSelectionSnapshot_SerializesRegionAndBlockOffsets()
    {
        var snapshot = new WysiwygSelectionSnapshot
        {
            Region = "TableCell",
            PageIndex = 2,
            HeaderFooterId = "header-a",
            AnchorBlockId = "block-1",
            AnchorInlineId = "inline-1",
            AnchorOffset = 3,
            AnchorBlockOffset = 17,
            FocusBlockId = "block-1",
            FocusInlineId = "inline-2",
            FocusOffset = 5,
            FocusBlockOffset = 25,
            IsCollapsed = false,
            ActiveTableCellId = "cell-a",
            TableCellPath = "table-a/row-0/cell-a"
        };

        var json = JsonSerializer.Serialize(snapshot, DocumentEditorJson.Options);
        var restored = JsonSerializer.Deserialize<WysiwygSelectionSnapshot>(json, DocumentEditorJson.Options);

        json.Should().Contain("TableCellPath");
        restored.Should().NotBeNull();
        restored!.Region.Should().Be("TableCell");
        restored.PageIndex.Should().Be(2);
        restored.HeaderFooterId.Should().Be("header-a");
        restored.AnchorBlockOffset.Should().Be(17);
        restored.FocusBlockOffset.Should().Be(25);
        restored.TableCellPath.Should().Be("table-a/row-0/cell-a");
    }

    [Fact]
    public void SectionProperties_CanReferenceHeadersFootersAndIndependentPageSettings()
    {
        var section = new DocumentSection
        {
            Id = "section-1",
            Properties = new DocumentSectionProperties
            {
                DifferentFirstPage = true,
                DifferentOddAndEvenPages = true,
                PageSettings = new DocumentPageSettings
                {
                    Size = DocumentPageSize.Letter,
                    Landscape = true
                },
                HeaderFooterReferences =
                [
                    new DocumentHeaderFooterReference
                    {
                        HeaderFooterId = "header-1",
                        Type = DocumentHeaderFooterType.Header,
                        Scope = DocumentHeaderFooterScope.FirstPage
                    }
                ]
            }
        };

        section.Properties.PageSettings.Size.Name.Should().Be("Letter");
        section.Properties.PageSettings.Landscape.Should().BeTrue();
        section.Properties.HeaderFooterReferences.Should().ContainSingle(reference =>
            reference.HeaderFooterId == "header-1" &&
            reference.Scope == DocumentHeaderFooterScope.FirstPage);
    }

    [Fact]
    public void InlineContent_StoresTextFormattingLinksTokensAndCommentAnchors()
    {
        var text = new TextRun
        {
            Text = "Agreement",
            Marks =
            [
                new InlineMark { Type = InlineMarkType.Bold },
                new InlineMark { Type = InlineMarkType.Italic },
                new InlineMark { Type = InlineMarkType.Underline },
                new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = "https://example.test", Title = "Example" } },
                new InlineMark
                {
                    Type = InlineMarkType.CommentAnchor,
                    CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" }
                }
            ]
        };

        var token = new TokenRun
        {
            Key = "case.number",
            DisplayName = "Case number",
            FallbackText = "N/A"
        };

        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);
        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Italic);
        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Underline);
        text.Marks.Should().Contain(mark => mark.Link != null && mark.Link.Href == "https://example.test" && mark.Link.Title == "Example");
        text.Marks.Should().Contain(mark => mark.CommentAnchor != null && mark.CommentAnchor.CommentId == "comment-1");
        token.Key.Should().Be("case.number");
    }

    [Theory]
    [InlineData("https://example.test", true)]
    [InlineData("mailto:user@example.test", true)]
    [InlineData("/documents/1", true)]
    [InlineData("#section", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,boom", false)]
    public void DocumentLinkUtility_ValidatesSafeHref(string href, bool expected)
    {
        DocumentLinkUtility.IsSafeHref(href).Should().Be(expected);
    }

    [Fact]
    public void TokenRun_StoresTypeMetadataAndSerializes()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TokenRun
                    {
                        Key = "client.name",
                        DisplayName = "Client name",
                        TokenType = "text",
                        TypeLabel = "Text",
                        ColorClass = "token-client",
                        Description = "Client full name"
                    }
                ]
            }
        });

        var json = JsonSerializer.Serialize(document);
        var restored = JsonSerializer.Deserialize<DocumentEditorDocument>(json);
        var token = ((ParagraphBlockContent)restored!.Blocks[0].Content).Inlines.OfType<TokenRun>().Single();

        token.TokenType.Should().Be("text");
        token.TypeLabel.Should().Be("Text");
        token.ColorClass.Should().Be("token-client");
        token.Description.Should().Be("Client full name");
    }

    [Fact]
    public void DocumentTokenHelper_ExtractsAndValidatesTokenKeys()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TokenRun { Key = "client.name", DisplayName = "Client name" },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" },
                    new TokenRun { Key = "case.number", DisplayName = "Case number" }
                ]
            }
        });

        var result = DocumentTokenHelper.ValidateTokens(document, ["client.name"]);

        result.TokenKeys.Should().BeEquivalentTo(["client.name", "case.number"]);
        result.DuplicateTokenKeys.Should().ContainSingle("client.name");
        result.MissingTokenKeys.Should().ContainSingle("case.number");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task TemplatePreview_ReplacesTokensWithValuesAndKeepsSourceUnchanged()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Dear " },
                    new TokenRun { Key = "client.name", DisplayName = "Client name" },
                    new TextRun { Text = ", ref " },
                    new TokenRun { Key = "case.number", DisplayName = "Case number", FallbackText = "missing case" }
                ]
            }
        });
        var provider = new TestTokenValueProvider(new Dictionary<string, DocumentTokenValue>
        {
            ["client.name"] = DocumentTokenValue.Resolved("client.name", "ACME Ltd.")
        });

        var preview = await new DocumentTemplatePreviewService(provider).CreatePreviewAsync(
            document,
            new DocumentTokenResolutionContext { DocumentId = "doc-1" });

        GetParagraphText(preview).Should().Be("Dear ACME Ltd., ref missing case");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines.Should().Contain(inline => inline is TokenRun);
        GetParagraphText(document).Should().Be("Dear Client name, ref Case number");
    }

    [Fact]
    public void DocumentTokenHelper_CreatesRunFromExistingTokenProviderToken()
    {
        var token = DocumentTokenHelper.FromToken(new TestToken(
            "client.name",
            "Client name",
            "Client full name",
            "Client",
            "Text"));

        token.Key.Should().Be("client.name");
        token.DisplayName.Should().Be("Client name");
        token.Description.Should().Be("Client full name");
        token.TypeLabel.Should().Be("Text");
        token.TokenType.Should().Be("text");
    }

    [Fact]
    public void Phase14_DocumentJson_RoundtripsAllDrawingRunLayoutVariants()
    {
        var document = DocumentEditorDocument.Empty("phase14-image-roundtrip");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase14-anchor",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Anchor text for every image layout variant. " },
                    CreateDrawing("phase14-inline", 20, DocumentObjectLayoutKind.Inline, DocumentWrapMode.Inline, null, moveWithText: true, fixedOnPage: false, zIndex: 0, rotation: 0),
                    CreateDrawing("phase14-square-left", 30, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, moveWithText: true, fixedOnPage: false, zIndex: 1, rotation: 0),
                    CreateDrawing("phase14-square-right", 40, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Right, moveWithText: true, fixedOnPage: false, zIndex: 2, rotation: 0),
                    CreateDrawing("phase14-top-bottom", 50, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.TopBottom, DocumentImageHorizontalPosition.Center, moveWithText: true, fixedOnPage: false, zIndex: 3, rotation: 3),
                    CreateDrawing("phase14-behind", 60, DocumentObjectLayoutKind.Anchored, DocumentWrapMode.BehindText, DocumentImageHorizontalPosition.Left, moveWithText: true, fixedOnPage: false, zIndex: 4, rotation: 7),
                    CreateDrawing("phase14-front", 70, DocumentObjectLayoutKind.Fixed, DocumentWrapMode.InFrontOfText, DocumentImageHorizontalPosition.Right, moveWithText: false, fixedOnPage: true, zIndex: 9, rotation: 11)
                ]
            }
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        restored.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        json.Should().Contain("\"Layout\"");
        json.Should().Contain("\"$type\":\"drawing\"");
        json.Should().Contain("\"Rotation\":11");
        json.Should().Contain("\"FixedOnPage\":true");
        json.Should().NotContain("FloatingLayout");
        var restoredInlines = ((ParagraphBlockContent)restored.Blocks.Single().Content).Inlines;
        AssertDrawing(restoredInlines, "phase14-inline", DocumentObjectLayoutKind.Inline, DocumentWrapMode.Inline, null, true, false, 0, 0);
        AssertDrawing(restoredInlines, "phase14-square-left", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left, true, false, 1, 0);
        AssertDrawing(restoredInlines, "phase14-square-right", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.Square, DocumentImageHorizontalPosition.Right, true, false, 2, 0);
        AssertDrawing(restoredInlines, "phase14-top-bottom", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.TopBottom, DocumentImageHorizontalPosition.Center, true, false, 3, 3);
        AssertDrawing(restoredInlines, "phase14-behind", DocumentObjectLayoutKind.Anchored, DocumentWrapMode.BehindText, DocumentImageHorizontalPosition.Left, true, false, 4, 7);
        AssertDrawing(restoredInlines, "phase14-front", DocumentObjectLayoutKind.Fixed, DocumentWrapMode.InFrontOfText, DocumentImageHorizontalPosition.Right, false, true, 9, 11);

        static DocumentDrawingRun CreateDrawing(
            string id,
            int order,
            DocumentObjectLayoutKind kind,
            DocumentWrapMode wrapMode,
            DocumentImageHorizontalPosition? horizontalPosition,
            bool moveWithText,
            bool fixedOnPage,
            int zIndex,
            double rotation)
            => new()
            {
                Id = $"{id}-inline",
                ObjectId = id,
                Source = DocumentImageSource.Url,
                Url = "/favicon.png",
                AltText = id,
                Caption = $"{id} caption",
                Size = new DocumentImageSize { Width = 220, Height = 124 },
                NaturalSize = new DocumentImageSize { Width = 220, Height = 124 },
                Layout = new DocumentObjectLayout
                {
                    Kind = kind,
                    Anchor = new DocumentObjectAnchor
                    {
                        BlockId = "phase14-anchor",
                        MoveWithText = moveWithText,
                        FixedOnPage = fixedOnPage,
                        LockAnchor = fixedOnPage
                    },
                    Position = new DocumentObjectPosition
                    {
                        HorizontalRelativeTo = DocumentRelativePosition.Page,
                        VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                        HorizontalAlignment = horizontalPosition,
                        X = order / 10d,
                        Y = order / 20d
                    },
                    Wrap = new DocumentObjectWrap
                    {
                        Mode = wrapMode,
                        DistanceLeft = 5,
                        DistanceRight = 6,
                        DistanceTop = 7,
                        DistanceBottom = 8
                    },
                    Transform = new DocumentObjectTransform
                    {
                        Width = 220,
                        Height = 124,
                        NaturalWidth = 220,
                        NaturalHeight = 124,
                        LockAspectRatio = true,
                        Rotation = rotation
                    },
                    Stacking = new DocumentObjectStacking
                    {
                        ZIndex = zIndex,
                        AllowOverlap = wrapMode is DocumentWrapMode.BehindText or DocumentWrapMode.InFrontOfText
                    }
                }
            };

        static void AssertDrawing(
            IEnumerable<InlineContent> inlines,
            string id,
            DocumentObjectLayoutKind kind,
            DocumentWrapMode wrapMode,
            DocumentImageHorizontalPosition? horizontalPosition,
            bool moveWithText,
            bool fixedOnPage,
            int zIndex,
            double rotation)
        {
            var image = inlines.OfType<DocumentDrawingRun>().Single(run => run.ObjectId == id);
            image.Caption.Should().Be($"{id} caption");
            image.Layout.Kind.Should().Be(kind);
            image.Layout.Anchor.BlockId.Should().Be("phase14-anchor");
            image.Layout.Anchor.MoveWithText.Should().Be(moveWithText);
            image.Layout.Anchor.FixedOnPage.Should().Be(fixedOnPage);
            image.Layout.Anchor.LockAnchor.Should().Be(fixedOnPage);
            image.Layout.Wrap.Mode.Should().Be(wrapMode);
            image.Layout.Wrap.DistanceLeft.Should().Be(5);
            image.Layout.Wrap.DistanceRight.Should().Be(6);
            image.Layout.Wrap.DistanceTop.Should().Be(7);
            image.Layout.Wrap.DistanceBottom.Should().Be(8);
            image.Layout.Position.HorizontalAlignment.Should().Be(horizontalPosition);
            image.Layout.Transform.Width.Should().Be(220);
            image.Layout.Transform.Height.Should().Be(124);
            image.Layout.Transform.Rotation.Should().Be(rotation);
            image.Layout.Stacking.ZIndex.Should().Be(zIndex);
        }
    }

    private static string GetParagraphText(DocumentEditorDocument document)
    {
        return string.Concat(((ParagraphBlockContent)document.Blocks[0].Content).Inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => token.DisplayName,
            _ => string.Empty
        }));
    }

    private static DocumentBlock CreateParagraph(string id, string text)
    {
        return new DocumentBlock
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = text }]
            }
        };
    }

    private sealed class TestTokenValueProvider : IDocumentTokenValueProvider
    {
        private readonly IReadOnlyDictionary<string, DocumentTokenValue> _values;

        public TestTokenValueProvider(IReadOnlyDictionary<string, DocumentTokenValue> values)
        {
            _values = values;
        }

        public Task<IReadOnlyDictionary<string, DocumentTokenValue>> ResolveTokenValuesAsync(
            DocumentTokenResolutionContext context,
            IReadOnlyList<TokenRun> tokens,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values);
        }
    }

    private sealed record TestToken(
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string? TypeLabel) : IToken
    {
        public string? Icon => null;

        public string? ColorClass => null;
    }
}
