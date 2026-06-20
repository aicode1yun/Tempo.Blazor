using System.Globalization;
using System.Text.Json;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Components.DocumentEditor.CanvasEngine.Model;

/// <summary>Phase 4 tests for the canonical canvas document model converter.</summary>
public sealed class CanvasModelConverterTests
{
    [Fact]
    public void ToCanvasModel_CreatesSectionsPageSettingsBodyAndStableBlockIds()
    {
        var document = CreateRichDocument();

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(document);

        Assert.Equal("phase-4-doc", canvas.DocumentId);
        Assert.Equal(2, canvas.Sections.Count);
        Assert.Equal("section-main", canvas.Sections[0].Id);
        Assert.Equal(816, canvas.PageSettings.Width);
        Assert.Equal(1056, canvas.PageSettings.Height);
        Assert.Equal(96, canvas.PageSettings.MarginTop);
        Assert.Equal(7, canvas.Body.Blocks.Count);
        Assert.Equal("heading-1", canvas.Body.Blocks[0].Id);
        Assert.Equal(CanvasDocumentModelTypes.Heading, canvas.Body.Blocks[0].Type);
        Assert.Equal(1, canvas.Body.Blocks[0].Content.HeadingLevel);
        Assert.Equal("Intro", canvas.Body.Blocks[0].Content.Runs[0].Text);
    }

    [Fact]
    public void RoundTrip_PreservesParagraphHeadingListQuoteAndInlineMarks()
    {
        var rebuilt = RoundTrip(CreateRichDocument());

        Assert.Equal("phase-4-doc", rebuilt.DocumentId);
        Assert.Equal(7, rebuilt.Blocks.Count);

        var heading = Assert.IsType<HeadingBlockContent>(rebuilt.Blocks[0].Content);
        Assert.Equal(1, heading.Level);
        Assert.Equal("Intro", Assert.IsType<TextRun>(heading.Inlines[0]).Text);

        var paragraph = Assert.IsType<ParagraphBlockContent>(rebuilt.Blocks[1].Content);
        var text = Assert.IsType<TextRun>(paragraph.Inlines[0]);
        Assert.Equal("Body text", text.Text);
        Assert.Contains(text.Marks, mark => mark.Type == InlineMarkType.Bold);
        Assert.Contains(text.Marks, mark => mark.Type == InlineMarkType.Bookmark && mark.Value == "intro-bookmark");

        var list = Assert.IsType<ListBlockContent>(rebuilt.Blocks[2].Content);
        Assert.True(list.Ordered);
        Assert.Equal(2, list.IndentLevel);
        Assert.Equal(4, list.StartNumber);
        Assert.Equal("contract-numbering", list.NumberingId);
        Assert.Equal("contract-abstract", list.AbstractNumberingId);
        Assert.Equal("contract-list-style", list.ListStyleId);
        Assert.Equal("legal", list.NumberFormat);
        Assert.Equal("%1.%2.%3.", list.LevelText);
        Assert.Equal("tab", list.Suffix);
        Assert.Equal(36, list.LabelIndent);
        Assert.Equal(18, list.HangingIndent);
        Assert.True(list.RestartNumbering);
        Assert.Equal(4, list.NumberingValue);
        Assert.Single(rebuilt.NumberingDefinitions);
        Assert.Single(rebuilt.ListStyles);

        var quote = Assert.IsType<QuoteBlockContent>(rebuilt.Blocks[3].Content);
        Assert.Equal("Quoted", Assert.IsType<TextRun>(quote.Inlines[0]).Text);
    }

    [Fact]
    public void PhaseE6_AdvancedCharacterMarksRoundTripThroughCanvasModel()
    {
        var source = DocumentEditorDocument.Empty("phase-e6-advanced-char-roundtrip");
        var sectionId = source.Sections[0].Id;
        source.Blocks =
        [
            new DocumentBlock
            {
                Id = "advanced-char-paragraph",
                SectionId = sectionId,
                Type = DocumentBlockType.Paragraph,
                Order = 10,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        Text("h-run", "H", InlineMarkType.SmallCaps),
                        Text("sub-run", "2", InlineMarkType.Subscript),
                        Text("o-run", "O", InlineMarkType.AllCaps),
                        Text("power-run", "x2", InlineMarkType.Superscript),
                        Text("strike-run", "double strike", InlineMarkType.DoubleStrikethrough),
                        new TextRun
                        {
                            Id = "spacing-run",
                            Text = "scaled spacing",
                            Marks =
                            [
                                new InlineMark { Type = InlineMarkType.CharacterSpacing, Value = "2.5" },
                                new InlineMark { Type = InlineMarkType.CharacterScale, Value = "125" },
                                new InlineMark { Type = InlineMarkType.Kerning, Value = "false" },
                                new InlineMark { Type = InlineMarkType.FontSize, Value = "13pt" }
                            ]
                        }
                    ]
                }
            }
        ];

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);

        var paragraph = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single(block => block.Id == "advanced-char-paragraph").Content);
        AssertMark(paragraph, "h-run", InlineMarkType.SmallCaps, null);
        AssertMark(paragraph, "sub-run", InlineMarkType.Subscript, null);
        AssertMark(paragraph, "o-run", InlineMarkType.AllCaps, null);
        AssertMark(paragraph, "power-run", InlineMarkType.Superscript, null);
        AssertMark(paragraph, "strike-run", InlineMarkType.DoubleStrikethrough, null);
        AssertMark(paragraph, "spacing-run", InlineMarkType.CharacterSpacing, "2.5");
        AssertMark(paragraph, "spacing-run", InlineMarkType.CharacterScale, "125");
        AssertMark(paragraph, "spacing-run", InlineMarkType.Kerning, "false");
        AssertMark(paragraph, "spacing-run", InlineMarkType.FontSize, "13pt");
    }

    [Fact]
    public void DocumentEditorJson_PreservesNumberingDefinitionsAndListStyles()
    {
        var source = CreateRichDocument();

        var restored = DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(source));

        Assert.Single(restored.NumberingDefinitions);
        Assert.Single(restored.ListStyles);
        Assert.Equal("contract-numbering", restored.NumberingDefinitions[0].Id);
        Assert.Equal("contract-list-style", restored.ListStyles[0].Id);
    }

    [Fact]
    public void PhaseE12_HyphenationAndPageBackgroundRoundTripThroughCanvasModel()
    {
        var source = CreateRichDocument();
        source.Hyphenation = new DocumentHyphenationOptions
        {
            Enabled = true,
            Mode = "manual",
            ConsecutiveLimit = 2,
            MinPrefix = 3,
            MinSuffix = 3,
            Zone = 24
        };
        source.PageBackground = new DocumentPageBackgroundOptions
        {
            Color = "#f8fafc",
            Watermark = new DocumentWatermarkOptions
            {
                Enabled = true,
                Kind = "text",
                Text = "E12",
                Color = "rgba(37, 99, 235, 0.46)",
                Opacity = 0.18,
                Rotation = -32
            },
            Border = new DocumentPageBorderOptions
            {
                Enabled = true,
                Color = "#2563eb",
                Width = 2,
                Margin = 18,
                AlignTo = "page",
                Dash = [8, 4]
            }
        };

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);

        Assert.True(canvas.Hyphenation.Enabled);
        Assert.Equal("manual", canvas.Hyphenation.Mode);
        Assert.Equal("#f8fafc", canvas.PageBackground.Color);
        Assert.Equal("E12", canvas.PageBackground.Watermark.Text);
        Assert.Equal("#2563eb", canvas.PageBackground.Border.Color);
        Assert.True(restored.Hyphenation.Enabled);
        Assert.Equal("manual", restored.Hyphenation.Mode);
        Assert.Equal("#f8fafc", restored.PageBackground.Color);
        Assert.Equal("E12", restored.PageBackground.Watermark.Text);
        Assert.Equal("#2563eb", restored.PageBackground.Border.Color);
        Assert.Equal([8, 4], restored.PageBackground.Border.Dash);
    }

    [Fact]
    public void PhaseE4_Styles_SaveReloadThroughCanvasModel()
    {
        var source = CreateRichDocument();
        source.Styles.Add(new DocumentStyleDefinition
        {
            Id = "contract-heading",
            Name = "Contract Heading",
            Type = DocumentStyleType.Paragraph,
            BasedOn = "heading-1",
            Next = "normal",
            IsQuickStyle = true,
            IsPrimary = true,
            HeadingLevel = 2,
            OutlineLevel = 2,
            ParagraphFormat = { ["spacingAfter"] = 16, ["keepWithNext"] = true },
            CharacterFormat = { ["fontSize"] = 18, ["fontWeight"] = "700" }
        });

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);
        var style = Assert.Single(restored.Styles);

        Assert.Equal("contract-heading", style.Id);
        Assert.Equal("Contract Heading", style.Name);
        Assert.Equal(DocumentStyleType.Paragraph, style.Type);
        Assert.Equal("heading-1", style.BasedOn);
        Assert.Equal("normal", style.Next);
        Assert.True(style.IsQuickStyle);
        Assert.True(style.IsPrimary);
        Assert.Equal(2, style.HeadingLevel);
        Assert.Equal(2, style.OutlineLevel);
        Assert.Equal(16, JsonElementNumber(style.ParagraphFormat["spacingAfter"]));
        Assert.Equal("700", style.CharacterFormat["fontWeight"]?.ToString());
    }

    [Fact]
    public void PhaseE2_TabStops_SaveReloadThroughCanvasModel()
    {
        var source = CreateRichDocument();
        source.Blocks[1].ParagraphProperties.DefaultTabWidth = 42;
        source.Blocks[1].ParagraphProperties.TabStops =
        [
            new DocumentTabStop
            {
                Position = 180,
                Alignment = DocumentTabStopAlignment.Decimal,
                Leader = DocumentTabStopLeader.Dots
            },
            new DocumentTabStop
            {
                Position = 260,
                Alignment = DocumentTabStopAlignment.Right,
                Leader = DocumentTabStopLeader.Underline
            }
        ];

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);
        var props = restored.Blocks[1].ParagraphProperties;

        Assert.Equal(42, props.DefaultTabWidth);
        Assert.Equal(2, props.TabStops.Count);
        Assert.Equal(180, props.TabStops[0].Position);
        Assert.Equal(DocumentTabStopAlignment.Decimal, props.TabStops[0].Alignment);
        Assert.Equal(DocumentTabStopLeader.Dots, props.TabStops[0].Leader);
        Assert.Equal(260, props.TabStops[1].Position);
        Assert.Equal(DocumentTabStopAlignment.Right, props.TabStops[1].Alignment);
        Assert.Equal(DocumentTabStopLeader.Underline, props.TabStops[1].Leader);
    }

    [Fact]
    public void PhaseE5_FieldsCaptionsAndBibliography_SaveReloadThroughCanvasModel()
    {
        var source = DocumentEditorDocument.Empty("phase-e5-roundtrip");
        source.BibliographySources.Add(new DocumentBibliographySource
        {
            Id = "source-a",
            SourceType = "article",
            Author = "Jane Smith",
            Title = "Reliable Canvas Editors",
            Container = "Tempo Review",
            Year = 2026,
            Url = "https://example.test/source-a",
            Metadata = { ["doi"] = "10.0000/tempo.e5" }
        });
        source.Citations.Add(new DocumentCitationReference
        {
            Id = "citation-a",
            SourceId = "source-a",
            RunId = "citation-run",
            Locator = "p. 14",
            DisplayText = "(Smith, 2026)"
        });
        source.Blocks =
        [
            new DocumentBlock
            {
                Id = "caption-block",
                Type = DocumentBlockType.Paragraph,
                Order = 10,
                Content = new ParagraphBlockContent
                {
                    Caption = new DocumentCaptionMetadata
                    {
                        Id = "caption-a",
                        Kind = "figure",
                        Label = "Figure",
                        Text = "Architecture",
                        Number = 1,
                        NumberLabel = "Figure 1"
                    },
                    Inlines =
                    [
                        new DocumentFieldRun
                        {
                            Id = "seq-run",
                            FieldType = DocumentFieldType.Seq,
                            InstrText = "SEQ Figure",
                            CachedResult = "Figure 1",
                            DisplayText = "Figure 1",
                            TargetId = "caption-a",
                            ReferenceKind = "figure",
                            SequenceId = "figure",
                            SequenceLabel = "Figure",
                            Metadata = { ["locked"] = "false" }
                        },
                        new TextRun { Id = "caption-text", Text = " Architecture" }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "fields-block",
                Type = DocumentBlockType.Paragraph,
                Order = 20,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentFieldRun
                        {
                            Id = "xref-run",
                            FieldType = DocumentFieldType.Ref,
                            InstrText = "REF caption-a",
                            CachedResult = "Figure 1 Architecture",
                            DisplayText = "Figure 1 Architecture",
                            TargetId = "caption-a",
                            ReferenceKind = "caption",
                            ReferenceFormat = "full"
                        },
                        new DocumentFieldRun
                        {
                            Id = "tof-run",
                            FieldType = DocumentFieldType.TableOfFigures,
                            InstrText = "TOF figure",
                            CachedResult = "Figure 1 Architecture\t1",
                            DisplayText = "Figure 1 Architecture\t1",
                            TargetId = "figure",
                            ReferenceKind = "figure"
                        },
                        new DocumentFieldRun
                        {
                            Id = "citation-run",
                            FieldType = DocumentFieldType.Citation,
                            InstrText = "CITATION source-a",
                            CachedResult = "(Smith, 2026)",
                            DisplayText = "(Smith, 2026)",
                            TargetId = "source-a",
                            CitationId = "source-a"
                        }
                    ]
                }
            }
        ];

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);

        var captionContent = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single(block => block.Id == "caption-block").Content);
        Assert.NotNull(captionContent.Caption);
        Assert.Equal("caption-a", captionContent.Caption.Id);
        Assert.Equal("Figure 1", captionContent.Caption.NumberLabel);
        var seq = Assert.IsType<DocumentFieldRun>(captionContent.Inlines[0]);
        Assert.Equal(DocumentFieldType.Seq, seq.FieldType);
        Assert.Equal("SEQ Figure", seq.InstrText);
        Assert.Equal("Figure 1", seq.CachedResult);
        Assert.Equal("figure", seq.SequenceId);
        Assert.Equal("false", seq.Metadata["locked"]);

        var restoredFields = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single(block => block.Id == "fields-block").Content)
            .Inlines
            .OfType<DocumentFieldRun>()
            .ToArray();
        Assert.Contains(restoredFields, field => field.FieldType == DocumentFieldType.Ref && field.TargetId == "caption-a" && field.ReferenceFormat == "full");
        Assert.Contains(restoredFields, field => field.FieldType == DocumentFieldType.TableOfFigures && field.CachedResult == "Figure 1 Architecture\t1");
        Assert.Contains(restoredFields, field => field.FieldType == DocumentFieldType.Citation && field.CitationId == "source-a");
        Assert.Single(restored.BibliographySources);
        Assert.Equal("10.0000/tempo.e5", restored.BibliographySources[0].Metadata["doi"]);
        Assert.Single(restored.Citations);
        Assert.Equal("citation-run", restored.Citations[0].RunId);
    }

    [Fact]
    public void RoundTrip_PreservesTableSpansLayoutAndNestedBlocks()
    {
        var rebuilt = RoundTrip(CreateRichDocument());

        var table = Assert.IsType<TableBlockContent>(rebuilt.Blocks[4].Content);
        Assert.Equal(TableHorizontalAlignment.Center, table.Layout.Alignment);
        Assert.Equal(520, table.Layout.Width);
        Assert.Equal("#f8fafc", table.Layout.BackgroundColor);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Rows[0].Cells[0].ColumnSpan);
        Assert.True(table.Rows[0].Cells[0].IsHeader);
        Assert.Equal(TableCellVerticalAlignment.Middle, table.Rows[0].Cells[0].VerticalAlignment);
        Assert.Equal("#e0f2fe", table.Rows[0].Cells[0].BackgroundColor);
        Assert.Equal("Header", CellText(table.Rows[0].Cells[0]));
        Assert.Equal(2, table.Rows[1].Cells[0].RowSpan);
        Assert.Equal("Body cell", CellText(table.Rows[1].Cells[0]));
    }

    [Fact]
    public void RoundTrip_PreservesImageInlineDrawingAndPageBreakGeometry()
    {
        var rebuilt = RoundTrip(CreateRichDocument());

        var paragraph = Assert.IsType<ParagraphBlockContent>(rebuilt.Blocks[1].Content);
        var drawing = Assert.IsType<DocumentDrawingRun>(paragraph.Inlines[1]);
        Assert.Equal("drawing-1", drawing.ObjectId);
        Assert.Equal(DocumentImageSource.Asset, drawing.Source);
        Assert.Equal("asset-inline", drawing.AssetId);
        Assert.Equal(DocumentWrapMode.Square, drawing.Layout.Wrap.Mode);
        Assert.Equal(144, drawing.Layout.Transform.Width);
        Assert.Equal(88, drawing.Layout.Transform.Height);
        Assert.Equal(12, drawing.Layout.Stacking.ZIndex);

        var image = Assert.IsType<ImageBlockContent>(rebuilt.Blocks[5].Content);
        Assert.Equal(DocumentImageSource.Asset, image.Source);
        Assert.Equal("asset-standalone", image.AssetId);
        Assert.Equal("Standalone image", image.AltText);
        Assert.Equal(DocumentWrapMode.TopBottom, image.Layout.Wrap.Mode);
        Assert.Equal(260, image.Layout.Transform.Width);

        var pageBreak = Assert.IsType<PageBreakBlockContent>(rebuilt.Blocks[6].Content);
        Assert.Equal(DocumentSectionBreakType.NextPage, pageBreak.BreakType);
        Assert.Equal("section-appendix", pageBreak.NextSectionId);
    }

    [Fact]
    public void RoundTrip_PreservesHeaderFooterFieldsNotesCommentsRevisionsAndSectionSettings()
    {
        var rebuilt = RoundTrip(CreateRichDocument());

        Assert.Equal(2, rebuilt.Sections.Count);
        Assert.True(rebuilt.Sections[0].Properties.DifferentFirstPage);
        Assert.Equal(2, rebuilt.Sections[0].Properties.Columns.Count);
        Assert.True(rebuilt.Sections[0].Properties.Columns.SeparatorLine);
        Assert.True(rebuilt.Sections[0].Properties.Columns.Balance);
        Assert.Equal(DocumentLineNumberingRestart.Section, rebuilt.Sections[0].Properties.LineNumbering.Restart);
        Assert.Equal(7, rebuilt.Sections[0].Properties.LineNumbering.StartAt);
        Assert.Equal("decimal", rebuilt.Sections[0].Properties.NoteNumbering.Style);
        Assert.Equal(2, rebuilt.Sections[0].Properties.HeaderFooterReferences.Count);

        var header = Assert.Single(rebuilt.HeadersFooters, item => item.Type == DocumentHeaderFooterType.Header);
        var headerParagraph = Assert.IsType<ParagraphBlockContent>(header.Blocks[0].Content);
        var field = Assert.IsType<DocumentFieldRun>(headerParagraph.Inlines[0]);
        Assert.Equal(DocumentFieldType.PageNumber, field.FieldType);
        Assert.Equal("1", field.DisplayText);

        var note = Assert.Single(rebuilt.Notes);
        Assert.Equal(DocumentNoteType.Footnote, note.Type);
        Assert.Equal("note-ref-1", Assert.Single(note.ReferenceIds));
        Assert.Equal("Footnote text", CellOrParagraphText(note.Blocks[0]));

        var comment = Assert.Single(rebuilt.Comments);
        Assert.Equal("comment-1", comment.Id);
        Assert.Equal(DocumentCommentStatus.Open, comment.Status);
        Assert.Equal("Body text", comment.Entries[0].Text);

        var revision = Assert.Single(rebuilt.Revisions);
        Assert.Equal("revision-1", revision.Id);
        Assert.Equal(DocumentRevisionType.Formatting, revision.Type);
        Assert.Equal(DocumentRevisionAction.Pending, revision.Action);
        Assert.Equal("paragraph-1", revision.Range.BlockId);
        Assert.Equal("""{"mark":"bold"}""", revision.PayloadJson);
    }

    [Fact]
    public void FromCanvasModel_PreservesReviewAndDocumentChromeWhenRuntimeSnapshotOmitsThem()
    {
        var source = CreateRichDocument();
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        canvas.Comments.Clear();
        canvas.Revisions.Clear();
        canvas.Notes.Clear();
        canvas.HeadersFooters.Clear();
        canvas.Assets.Clear();

        var rebuilt = CanvasDocumentModelConverter.FromCanvasModel(canvas, source);

        Assert.Single(rebuilt.Comments);
        Assert.Single(rebuilt.Revisions);
        Assert.Single(rebuilt.Notes);
        Assert.Equal(2, rebuilt.HeadersFooters.Count);
        Assert.Equal(2, rebuilt.Assets.Count);
        Assert.Equal("comment-1", rebuilt.Comments[0].Id);
        Assert.Equal("revision-1", rebuilt.Revisions[0].Id);
    }

    [Fact]
    public void CanvasModelJsonSaveReload_RoundTripsWithoutLosingStructuredDocumentData()
    {
        var source = CreateRichDocument();
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;

        var restoredDocument = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);
        var normalizedSource = DocumentEditorJson.Normalize(DocumentEditorJson.Serialize(source));
        var normalizedRestored = DocumentEditorJson.Normalize(DocumentEditorJson.Serialize(restoredDocument));

        Assert.Equal(normalizedSource, normalizedRestored);
    }

    [Fact]
    public void FromCanvasModel_UsesPreservedStandaloneImagePayloadWhenCanvasImageIsPartial()
    {
        var source = CreateRichDocument();
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var imageBlock = canvas.Body.Blocks.Single(block => block.Id == "image-1");
        imageBlock.Content.Image = null;

        var restored = CanvasDocumentModelConverter.FromCanvasModel(canvas, source);

        var image = Assert.IsType<ImageBlockContent>(restored.Blocks.Single(block => block.Id == "image-1").Content);
        Assert.Equal(DocumentImageSource.Asset, image.Source);
        Assert.Equal("asset-standalone", image.AssetId);
        Assert.Equal("Standalone image", image.AltText);
        Assert.Equal(260, image.Layout.Transform.Width);
        Assert.Equal(140, image.Layout.Transform.Height);
    }

    [Fact]
    public void FromCanvasModel_RestoresPreservedStandaloneImageWhenRuntimeSendsEmptyParagraph()
    {
        var source = CreateRichDocument();
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var imageBlock = canvas.Body.Blocks.Single(block => block.Id == "image-1");
        imageBlock.Type = CanvasDocumentModelTypes.Paragraph;
        imageBlock.Content = new CanvasBlockContent
        {
            Type = CanvasDocumentModelTypes.Paragraph,
            Runs = [new CanvasInlineRun { Id = "image-1-empty-run", Text = string.Empty }]
        };
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;

        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas, source);

        var image = Assert.IsType<ImageBlockContent>(restored.Blocks.Single(block => block.Id == "image-1").Content);
        Assert.Equal(DocumentImageSource.Asset, image.Source);
        Assert.Equal("asset-standalone", image.AssetId);
        Assert.Equal("Standalone image", image.AltText);
        Assert.Equal(DocumentWrapMode.TopBottom, image.Layout.Wrap.Mode);
    }

    [Fact]
    public void Phase10_HeadingParagraphPropertiesAndInlineFormatting_SaveReloadThroughCanvasModel()
    {
        var source = DocumentEditorDocument.Empty("phase-10-roundtrip");
        source.Blocks =
        [
            new DocumentBlock
            {
                Id = "phase-10-heading",
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                ParagraphProperties = new DocumentParagraphProperties
                {
                    Alignment = DocumentTextAlignment.Left,
                    LineSpacing = 1,
                    SpacingBefore = 0,
                    SpacingAfter = 0,
                    LeftIndent = 0
                },
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Id = "phase-10-heading-run",
                            Text = "Canvas heading",
                            Marks = [new InlineMark { Type = InlineMarkType.Bold }]
                        }
                    ]
                }
            }
        ];

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var block = canvas.Body.Blocks[0];
        block.Type = CanvasDocumentModelTypes.Heading;
        block.Content.Type = CanvasDocumentModelTypes.Heading;
        block.Content.HeadingLevel = 2;
        block.Content.StyleId = "heading-2";
        block.Content.StyleName = "Heading 2";
        block.Content.OutlineLevel = 2;
        block.ParagraphProperties.Alignment = DocumentTextAlignment.Center;
        block.ParagraphProperties.LineSpacing = 1.5;
        block.ParagraphProperties.SpacingBefore = 12;
        block.ParagraphProperties.SpacingAfter = 18;
        block.ParagraphProperties.LeftIndent = 18;
        canvas.OutlineRevision = 1;
        canvas.TableOfContentsRevision = 1;

        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restoredDocument = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas, source);

        var restoredBlock = Assert.Single(restoredDocument.Blocks);
        var heading = Assert.IsType<HeadingBlockContent>(restoredBlock.Content);
        Assert.Equal(2, heading.Level);
        Assert.Equal(DocumentTextAlignment.Center, restoredBlock.ParagraphProperties.Alignment);
        Assert.Equal(1.5, restoredBlock.ParagraphProperties.LineSpacing);
        Assert.Equal(12, restoredBlock.ParagraphProperties.SpacingBefore);
        Assert.Equal(18, restoredBlock.ParagraphProperties.SpacingAfter);
        Assert.Equal(18, restoredBlock.ParagraphProperties.LeftIndent);
        var run = Assert.IsType<TextRun>(Assert.Single(heading.Inlines));
        Assert.Equal("Canvas heading", run.Text);
        Assert.Contains(run.Marks, mark => mark.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void Phase14_TableEdits_SaveReloadThroughCanvasModel()
    {
        var source = CreateRichDocument();
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var tableBlock = canvas.Body.Blocks.Single(block => block.Id == "table-1");
        var table = tableBlock.Content.Table!;
        var bodyCell = table.Rows[1].Cells[0];
        bodyCell.Blocks[0].Content.Runs[0].Text = "Body cell typed";
        bodyCell.BackgroundColor = "#ecfdf5";
        bodyCell.VerticalAlignment = TableCellVerticalAlignment.Middle;
        bodyCell.Blocks[0].ParagraphProperties.Alignment = DocumentTextAlignment.Center;
        table.Rows.Add(new CanvasTableRow
        {
            Id = "table-1-row-3",
            Cells =
            [
                CanvasCell("table-1-r3c1", "Inserted row", TableCellVerticalAlignment.Top),
                CanvasCell("table-1-r3c2", "Inserted value", TableCellVerticalAlignment.Bottom)
            ]
        });
        foreach (var row in table.Rows)
        {
            row.Cells.Add(CanvasCell($"{row.Id}-inserted-column", "Inserted column", TableCellVerticalAlignment.Top));
        }

        var json = JsonSerializer.Serialize(canvas, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var restoredDocument = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas, source);
        var serializedDocument = DocumentEditorJson.Serialize(restoredDocument);

        Assert.Contains("Body cell typed", serializedDocument, StringComparison.Ordinal);
        var restoredTable = Assert.IsType<TableBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "table-1").Content);
        Assert.Equal(3, restoredTable.Rows.Count);
        Assert.Equal(TableCellVerticalAlignment.Middle, restoredTable.Rows[1].Cells[0].VerticalAlignment);
        Assert.Equal(DocumentTextAlignment.Center, restoredTable.Rows[1].Cells[0].Blocks[0].ParagraphProperties.Alignment);
        Assert.Equal("Inserted column", CellText(restoredTable.Rows[0].Cells.Last()));
    }

    [Fact]
    public void Phase15_ImageAndDrawingLayoutMetadata_SaveReloadThroughCanvasModel()
    {
        var source = CreateRichDocument();
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var imageBlock = canvas.Body.Blocks.Single(block => block.Id == "image-1");
        var image = imageBlock.Content.Image!;
        image.AltText = "Phase 15 standalone image";
        image.Caption = "Phase 15 persisted caption";
        image.Layout = new DocumentObjectLayout
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = { BlockId = "paragraph-1", Offset = 4, MoveWithText = true },
            Wrap = { Mode = DocumentWrapMode.Square, DistanceLeft = 14, DistanceRight = 16, DistanceTop = 6, DistanceBottom = 10 },
            Position = { X = 48, Y = 36, HorizontalAlignment = DocumentImageHorizontalPosition.Left },
            Transform = { Width = 192, Height = 108, LockAspectRatio = true },
            Stacking = { ZIndex = 7, AllowOverlap = true }
        };
        image.Size = new DocumentImageSize { Width = 192, Height = 108, LockAspectRatio = true };

        var drawingRun = canvas.Body.Blocks
            .Single(block => block.Id == "paragraph-1")
            .Content
            .Runs
            .Single(run => run.Type == CanvasDocumentModelTypes.DrawingRun);
        drawingRun.Drawing!.AltText = "Phase 15 drawing run";
        drawingRun.Drawing.Caption = "Phase 15 drawing caption";
        drawingRun.Drawing.Layout.Transform.Width = 156;
        drawingRun.Drawing.Layout.Transform.Height = 96;
        drawingRun.Drawing.Layout.Position.X = 24;
        drawingRun.Drawing.Layout.Position.Y = 18;
        drawingRun.Drawing.Layout.Stacking.ZIndex = 11;

        var json = JsonSerializer.Serialize(canvas, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var restoredDocument = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas, source);

        var restoredImage = Assert.IsType<ImageBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "image-1").Content);
        Assert.Equal("Phase 15 standalone image", restoredImage.AltText);
        Assert.Equal("Phase 15 persisted caption", restoredImage.Caption);
        Assert.Equal(DocumentWrapMode.Square, restoredImage.Layout.Wrap.Mode);
        Assert.Equal(48, restoredImage.Layout.Position.X);
        Assert.Equal(192, restoredImage.Layout.Transform.Width);
        Assert.Equal(7, restoredImage.Layout.Stacking.ZIndex);
        Assert.True(restoredImage.Layout.Stacking.AllowOverlap);

        var restoredParagraph = Assert.IsType<ParagraphBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "paragraph-1").Content);
        var restoredDrawing = restoredParagraph.Inlines.OfType<DocumentDrawingRun>().Single();
        Assert.Equal("Phase 15 drawing run", restoredDrawing.AltText);
        Assert.Equal("Phase 15 drawing caption", restoredDrawing.Caption);
        Assert.Equal(156, restoredDrawing.Layout.Transform.Width);
        Assert.Equal(24, restoredDrawing.Layout.Position.X);
        Assert.Equal(11, restoredDrawing.Layout.Stacking.ZIndex);
    }

    [Fact]
    public void PhaseE7_DrawingShapeTextBoxLineAndChartRoundTripThroughCanvasModel()
    {
        var source = DocumentEditorDocument.Empty("phase-e7-roundtrip");
        var layout = new DocumentObjectLayout
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = { BlockId = "anchor", Offset = 0 },
            Wrap = { Mode = DocumentWrapMode.InFrontOfText, DistanceLeft = 8, DistanceRight = 8 },
            Position = { X = 24, Y = 40 },
            Transform = { Width = 240, Height = 120, Rotation = 22.5, Flip = new DocumentObjectFlip { Horizontal = true } },
            Stacking = { ZIndex = 9 }
        };
        source.Blocks =
        [
            new DocumentBlock
            {
                Id = "anchor",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "Anchor" }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "drawing-p",
                Type = DocumentBlockType.Paragraph,
                Order = 2,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentDrawingRun
                        {
                            Id = "drawing-run",
                            ObjectId = "e7-textbox-chart",
                            Kind = DocumentDrawingKind.Chart,
                            Layout = layout,
                            Size = new DocumentImageSize { Width = 240, Height = 120 },
                            Shape = new DocumentDrawingShape
                            {
                                Preset = "roundRectangle",
                                Fill = new DocumentDrawingFill { Color = "#dbeafe", Opacity = 0.88 },
                                Stroke = new DocumentDrawingStroke { Color = "#2563eb", Width = 2, EndArrow = "triangle" }
                            },
                            TextBody = new DocumentDrawingTextBody
                            {
                                Paragraphs =
                                [
                                    new DocumentDrawingTextParagraph
                                    {
                                        Text = "Drawing text",
                                        Alignment = "center",
                                        Style = new DocumentDrawingTextStyle { FontSize = 15, Bold = true }
                                    }
                                ]
                            },
                            Chart = new DocumentDrawingChart
                            {
                                Type = "bar",
                                Title = "Trend",
                                Categories = ["Q1", "Q2"],
                                Series =
                                [
                                    new DocumentDrawingChartSeries { Name = "Actual", Values = [3, 7], Color = "#16a34a" }
                                ]
                            }
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "connector-p",
                Type = DocumentBlockType.Paragraph,
                Order = 3,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentDrawingRun
                        {
                            Id = "connector-run",
                            ObjectId = "e7-connector",
                            Kind = DocumentDrawingKind.Connector,
                            Layout = CloneDocumentObjectLayout(layout),
                            Size = new DocumentImageSize { Width = 280, Height = 60 },
                            Shape = new DocumentDrawingShape
                            {
                                Preset = "bentConnector",
                                Fill = new DocumentDrawingFill { Type = "none" },
                                Stroke = new DocumentDrawingStroke { Color = "#16a34a", Width = 2, EndArrow = "triangle" },
                                StartConnection = new DocumentDrawingConnection { ObjectId = "source-shape", Site = "right" },
                                EndConnection = new DocumentDrawingConnection { ObjectId = "target-shape", Site = "left" },
                                Adjustments = { ["bend"] = 0.5 }
                            },
                            Docx = new DocumentDocxDrawingMetadata
                            {
                                DocPrId = 42,
                                PresetGeometry = "bentConnector",
                                RawDrawingXml = "<w:drawing><wp:anchor/></w:drawing>"
                            }
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "group-p",
                Type = DocumentBlockType.Paragraph,
                Order = 5,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentDrawingRun
                        {
                            Id = "group-run",
                            ObjectId = "e7-group",
                            Kind = DocumentDrawingKind.Group,
                            Layout = CloneDocumentObjectLayout(layout),
                            Size = new DocumentImageSize { Width = 320, Height = 180 },
                            Group = new DocumentDrawingGroup
                            {
                                ChildObjectIds = ["source-shape", "target-shape"],
                                Origin = new DocumentDrawingPoint { X = 12, Y = 18 },
                                Size = new DocumentDrawingPoint { X = 320, Y = 180 }
                            },
                            Metadata = { ["source"] = "canvas-e7-group" }
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "line-p",
                Type = DocumentBlockType.Paragraph,
                Order = 4,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentDrawingRun
                        {
                            Id = "line-run",
                            ObjectId = "e7-line",
                            Kind = DocumentDrawingKind.Line,
                            Layout = CloneDocumentObjectLayout(layout),
                            Size = new DocumentImageSize { Width = 184, Height = 34 },
                            Shape = new DocumentDrawingShape
                            {
                                Preset = "line",
                                Fill = new DocumentDrawingFill { Type = "none" },
                                Stroke = new DocumentDrawingStroke
                                {
                                    Color = "#0f766e",
                                    Width = 3,
                                    StartArrow = "oval",
                                    EndArrow = "triangle",
                                    Dash = "dash"
                                }
                            }
                        }
                    ]
                }
            }
        ];

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restoredDocument = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);
        var restoredDrawing = Assert.IsType<ParagraphBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "drawing-p").Content)
            .Inlines
            .OfType<DocumentDrawingRun>()
            .Single();

        Assert.Equal(DocumentDrawingKind.Chart, restoredDrawing.Kind);
        Assert.Equal("roundRectangle", restoredDrawing.Shape?.Preset);
        Assert.Equal("#dbeafe", restoredDrawing.Shape?.Fill.Color);
        Assert.Equal("triangle", restoredDrawing.Shape?.Stroke.EndArrow);
        Assert.Equal("Drawing text", restoredDrawing.TextBody?.Paragraphs.Single().Text);
        Assert.Equal("Trend", restoredDrawing.Chart?.Title);
        Assert.Equal(7, restoredDrawing.Chart?.Series.Single().Values[1]);
        Assert.Equal(9, restoredDrawing.Layout.Stacking.ZIndex);
        Assert.Equal(22.5, restoredDrawing.Layout.Transform.Rotation);
        Assert.True(restoredDrawing.Layout.Transform.Flip?.Horizontal);

        var restoredConnector = Assert.IsType<ParagraphBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "connector-p").Content)
            .Inlines
            .OfType<DocumentDrawingRun>()
            .Single();
        Assert.Equal(DocumentDrawingKind.Connector, restoredConnector.Kind);
        Assert.Equal("bentConnector", restoredConnector.Shape?.Preset);
        Assert.Equal("source-shape", restoredConnector.Shape?.StartConnection?.ObjectId);
        Assert.Equal("right", restoredConnector.Shape?.StartConnection?.Site);
        Assert.Equal("target-shape", restoredConnector.Shape?.EndConnection?.ObjectId);
        Assert.Equal("triangle", restoredConnector.Shape?.Stroke.EndArrow);
        Assert.Equal(0.5, restoredConnector.Shape?.Adjustments["bend"]);
        Assert.Equal(42u, restoredConnector.Docx?.DocPrId);
        Assert.Equal("<w:drawing><wp:anchor/></w:drawing>", restoredConnector.Docx?.RawDrawingXml);

        var restoredLine = Assert.IsType<ParagraphBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "line-p").Content)
            .Inlines
            .OfType<DocumentDrawingRun>()
            .Single();
        Assert.Equal(DocumentDrawingKind.Line, restoredLine.Kind);
        Assert.Equal("line", restoredLine.Shape?.Preset);
        Assert.Equal("#0f766e", restoredLine.Shape?.Stroke.Color);
        Assert.Equal("oval", restoredLine.Shape?.Stroke.StartArrow);
        Assert.Equal("triangle", restoredLine.Shape?.Stroke.EndArrow);
        Assert.Equal("dash", restoredLine.Shape?.Stroke.Dash);

        var restoredGroup = Assert.IsType<ParagraphBlockContent>(restoredDocument.Blocks.Single(block => block.Id == "group-p").Content)
            .Inlines
            .OfType<DocumentDrawingRun>()
            .Single();
        Assert.Equal(DocumentDrawingKind.Group, restoredGroup.Kind);
        Assert.Equal(["source-shape", "target-shape"], restoredGroup.Group?.ChildObjectIds);
        Assert.Equal(12, restoredGroup.Group?.Origin.X);
        Assert.Equal(180, restoredGroup.Group?.Size.Y);
        Assert.Equal("canvas-e7-group", restoredGroup.Metadata["source"]);
    }

    [Fact]
    public void PhaseE8_MathEquationRoundTripsThroughCanvasModel()
    {
        var source = DocumentEditorDocument.Empty("phase-e8-roundtrip");
        source.Blocks =
        [
            new DocumentBlock
            {
                Id = "math-p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "Equation: " },
                        new DocumentMathRun
                        {
                            Id = "math-run",
                            MathId = "math-e8",
                            DisplayMode = DocumentMathDisplayMode.Display,
                            AltText = "fraction a over b plus x squared",
                            Content = new DocumentMathContent
                            {
                                Elements =
                                [
                                    new DocumentMathElement
                                    {
                                        Type = "fraction",
                                        Numerator = MathContent("a"),
                                        Denominator = MathContent("b")
                                    },
                                    new DocumentMathElement { Type = "run", Text = "+" },
                                    new DocumentMathElement
                                    {
                                        Type = "sup",
                                        Base = MathContent("x"),
                                        Superscript = MathContent("2")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "radical",
                                        Radicand = MathContent("y")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "nary",
                                        Operator = "∑",
                                        LowerLimit = MathContent("i=1"),
                                        UpperLimit = MathContent("n"),
                                        Base = MathContent("i")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "matrix",
                                        Rows =
                                        [
                                            new DocumentMathMatrixRow { Cells = [MathContent("1"), MathContent("0")] },
                                            new DocumentMathMatrixRow { Cells = [MathContent("0"), MathContent("1")] }
                                        ]
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "preSubSup",
                                        Base = MathContent("T"),
                                        Subscript = MathContent("i"),
                                        Superscript = MathContent("j")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "accent",
                                        Accent = "̂",
                                        Base = MathContent("x")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "groupChar",
                                        Position = "under",
                                        Base = MathContent("a+b")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "limit",
                                        Base = MathContent("lim"),
                                        LowerLimit = MathContent("x→0"),
                                        Content = MathContent("f(x)")
                                    },
                                    new DocumentMathElement
                                    {
                                        Type = "borderBox",
                                        Content = MathContent("x+y")
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
        ];

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restoredDocument = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);
        var restoredMath = Assert.IsType<ParagraphBlockContent>(restoredDocument.Blocks.Single().Content)
            .Inlines
            .OfType<DocumentMathRun>()
            .Single();

        Assert.Equal("math-e8", restoredMath.MathId);
        Assert.Equal(DocumentMathDisplayMode.Display, restoredMath.DisplayMode);
        Assert.Equal("fraction", restoredMath.Content.Elements[0].Type);
        Assert.Equal("a", restoredMath.Content.Elements[0].Numerator?.Elements.Single().Text);
        Assert.Equal("sup", restoredMath.Content.Elements[2].Type);
        Assert.Equal("radical", restoredMath.Content.Elements[3].Type);
        Assert.Equal("∑", restoredMath.Content.Elements[4].Operator);
        Assert.Equal(2, restoredMath.Content.Elements[5].Rows.Count);
        Assert.Equal("1", restoredMath.Content.Elements[5].Rows[0].Cells[0].Elements.Single().Text);
        Assert.Equal("preSubSup", restoredMath.Content.Elements[6].Type);
        Assert.Equal("j", restoredMath.Content.Elements[6].Superscript?.Elements.Single().Text);
        Assert.Equal("accent", restoredMath.Content.Elements[7].Type);
        Assert.Equal("̂", restoredMath.Content.Elements[7].Accent);
        Assert.Equal("groupChar", restoredMath.Content.Elements[8].Type);
        Assert.Equal("under", restoredMath.Content.Elements[8].Position);
        Assert.Equal("limit", restoredMath.Content.Elements[9].Type);
        Assert.Equal("x→0", restoredMath.Content.Elements[9].LowerLimit?.Elements.Single().Text);
        Assert.Equal("borderBox", restoredMath.Content.Elements[10].Type);
        Assert.Equal("x+y", restoredMath.Content.Elements[10].Content?.Elements.Single().Text);
    }

    [Fact]
    public void PhaseE8_MathEquationRoundTripsThroughDocumentEditorJson()
    {
        var source = DocumentEditorDocument.Empty("phase-e8-json");
        source.Blocks =
        [
            new DocumentBlock
            {
                Id = "math-json-p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentMathRun
                        {
                            Id = "math-json-run",
                            MathId = "math-json",
                            Content = new DocumentMathContent
                            {
                                Elements =
                                [
                                    new DocumentMathElement
                                    {
                                        Type = "fraction",
                                        Numerator = MathContent("a"),
                                        Denominator = MathContent("b")
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
        ];

        var restored = DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(source));
        var restoredMath = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single().Content)
            .Inlines
            .OfType<DocumentMathRun>()
            .Single();

        Assert.Equal("math-json", restoredMath.MathId);
        Assert.Equal("fraction", restoredMath.Content.Elements.Single().Type);
    }

    [Fact]
    public async Task PhaseE8_DemoSeedLoadsMathRunsIntoCanvasModel()
    {
        var provider = new DemoDocumentEditorProvider(null);
        provider.SeedCanvasMathEquationsDocument();

        var result = await provider.LoadAsync(DemoDocumentEditorProvider.CanvasMathEquationsDocumentId);
        Assert.NotNull(result.Document);
        var document = result.Document;
        var canvas = CanvasDocumentModelConverter.ToCanvasModel(document);
        var mathRuns = canvas.Body.Blocks
            .SelectMany(block => block.Content.Runs)
            .Where(run => run.Type == CanvasDocumentModelTypes.MathRun)
            .ToList();

        Assert.True(result.Found);
        Assert.Equal(DemoDocumentEditorProvider.CanvasMathEquationsDocumentId, canvas.DocumentId);
        Assert.True(mathRuns.Count >= 3);
        Assert.Contains(mathRuns, run => run.Math?.Content.Elements.Any(element => element.Type == "fraction") == true);
        Assert.Contains(mathRuns, run => run.Math?.Content.Elements.Any(element => element.Type == "matrix") == true);
    }

    [Fact]
    public void PhaseE9_ContentControlsRoundTripThroughCanvasModel()
    {
        var source = CreateContentControlsDocument("phase-e9-roundtrip");

        var canvas = CanvasDocumentModelConverter.ToCanvasModel(source);
        var json = JsonSerializer.Serialize(canvas, DocumentEditorJson.Options);
        var restoredCanvas = JsonSerializer.Deserialize<CanvasDocumentModel>(json, DocumentEditorJson.Options)!;
        var restored = CanvasDocumentModelConverter.FromCanvasModel(restoredCanvas);

        var paragraph = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single(block => block.Id == "form-p").Content);
        var name = Assert.IsType<DocumentContentControlRun>(paragraph.Inlines[0]);
        var approved = Assert.IsType<DocumentContentControlRun>(paragraph.Inlines[1]);
        var plan = Assert.IsType<DocumentContentControlRun>(paragraph.Inlines[2]);
        var block = Assert.IsType<ContentControlBlockContent>(restored.Blocks.Single(block => block.Id == "address-content-control").Content);

        Assert.Equal("customer-name", name.Control.ControlId);
        Assert.Equal(DocumentContentControlKind.PlainText, name.Control.Kind);
        Assert.Equal(DocumentContentControlScope.Inline, name.Control.Scope);
        Assert.True(name.Control.IsRequired);
        Assert.True(name.Control.LockDeletion);
        Assert.Equal("Ada", name.Control.Value.Text);
        Assert.Equal("approved", approved.Control.ControlId);
        Assert.True(approved.Control.Value.Checked);
        Assert.Equal("plan", plan.Control.ControlId);
        Assert.Equal(DocumentContentControlKind.DropDown, plan.Control.Kind);
        Assert.Equal("enterprise", plan.Control.Value.SelectedValue);
        Assert.Equal("Enterprise", plan.Control.Items.Single(item => item.Value == "enterprise").DisplayText);
        Assert.Equal("address-section", block.Control.ControlId);
        Assert.Equal(DocumentContentControlScope.Block, block.Control.Scope);
        Assert.Single(block.Blocks);
    }

    [Fact]
    public void PhaseE9_ContentControlsRoundTripThroughDocumentEditorJson()
    {
        var source = CreateContentControlsDocument("phase-e9-json");

        var restored = DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(source));

        var paragraph = Assert.IsType<ParagraphBlockContent>(restored.Blocks.Single(block => block.Id == "form-p").Content);
        var picture = Assert.IsType<DocumentContentControlRun>(paragraph.Inlines[3]);
        var repeating = Assert.IsType<ContentControlBlockContent>(restored.Blocks.Single(block => block.Id == "address-content-control").Content);

        Assert.Equal(DocumentContentControlKind.Picture, picture.Control.Kind);
        Assert.Equal("asset-photo", picture.Control.Value.AssetId);
        Assert.Equal(DocumentContentControlKind.RepeatingSection, repeating.Control.Kind);
        Assert.Equal("address", repeating.Control.Tag);
        Assert.Equal("Address", repeating.Control.Alias);
        Assert.Single(repeating.Blocks);
    }

    private static DocumentEditorDocument RoundTrip(DocumentEditorDocument document)
        => CanvasDocumentModelConverter.FromCanvasModel(CanvasDocumentModelConverter.ToCanvasModel(document));

    private static DocumentEditorDocument CreateContentControlsDocument(string documentId)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "form-p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new DocumentContentControlRun
                        {
                            Id = "customer-name-run",
                            Control = new DocumentContentControl
                            {
                                ControlId = "customer-name",
                                Kind = DocumentContentControlKind.PlainText,
                                Scope = DocumentContentControlScope.Inline,
                                Alias = "Customer name",
                                Tag = "customer.name",
                                PlaceholderText = "Customer name",
                                IsRequired = true,
                                LockDeletion = true,
                                Value = new DocumentContentControlValue { Text = "Ada" }
                            }
                        },
                        new DocumentContentControlRun
                        {
                            Id = "approved-run",
                            Control = new DocumentContentControl
                            {
                                ControlId = "approved",
                                Kind = DocumentContentControlKind.Checkbox,
                                Scope = DocumentContentControlScope.Inline,
                                Value = new DocumentContentControlValue { Checked = true }
                            }
                        },
                        new DocumentContentControlRun
                        {
                            Id = "plan-run",
                            Control = new DocumentContentControl
                            {
                                ControlId = "plan",
                                Kind = DocumentContentControlKind.DropDown,
                                Scope = DocumentContentControlScope.Inline,
                                Value = new DocumentContentControlValue { SelectedValue = "enterprise" },
                                Items =
                                [
                                    new DocumentContentControlItem { DisplayText = "Basic", Value = "basic" },
                                    new DocumentContentControlItem { DisplayText = "Enterprise", Value = "enterprise" }
                                ]
                            }
                        },
                        new DocumentContentControlRun
                        {
                            Id = "picture-run",
                            Control = new DocumentContentControl
                            {
                                ControlId = "profile-photo",
                                Kind = DocumentContentControlKind.Picture,
                                Scope = DocumentContentControlScope.Inline,
                                PlaceholderText = "Photo",
                                Value = new DocumentContentControlValue { AssetId = "asset-photo" }
                            }
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "address-content-control",
                Type = DocumentBlockType.ContentControl,
                Content = new ContentControlBlockContent
                {
                    Control = new DocumentContentControl
                    {
                        ControlId = "address-section",
                        Kind = DocumentContentControlKind.RepeatingSection,
                        Scope = DocumentContentControlScope.Block,
                        Alias = "Address",
                        Tag = "address"
                    },
                    Blocks =
                    [
                        Paragraph("address-line", new TextRun { Text = "Main Street" })
                    ]
                }
            }
        ];

        return document;
    }

    private static DocumentMathContent MathContent(string text)
        => new()
        {
            Elements = [new DocumentMathElement { Type = "run", Text = text }]
        };

    private static CanvasTableCell CanvasCell(
        string id,
        string text,
        TableCellVerticalAlignment verticalAlignment)
        => new()
        {
            Id = id,
            VerticalAlignment = verticalAlignment,
            Merge = new TableCellMerge { IsOrigin = true },
            Blocks =
            [
                new CanvasDocumentBlock
                {
                    Id = $"{id}-p",
                    Type = CanvasDocumentModelTypes.Paragraph,
                    Order = 1,
                    ParagraphProperties = new DocumentParagraphProperties { Alignment = DocumentTextAlignment.Left },
                    Content = new CanvasBlockContent
                    {
                        Type = CanvasDocumentModelTypes.Paragraph,
                        Runs =
                        [
                            new CanvasInlineRun
                            {
                                Id = $"{id}-run",
                                Type = CanvasDocumentModelTypes.TextRun,
                                Text = text
                            }
                        ]
                    }
                }
            ]
        };

    private static DocumentObjectLayout CloneDocumentObjectLayout(DocumentObjectLayout layout)
        => JsonSerializer.Deserialize<DocumentObjectLayout>(
            JsonSerializer.Serialize(layout, DocumentEditorJson.Options),
            DocumentEditorJson.Options)!;

    private static double JsonElementNumber(object? value)
        => value is JsonElement element ? element.GetDouble() : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static DocumentEditorDocument CreateRichDocument()
    {
        var document = DocumentEditorDocument.Empty("phase-4-doc");
        document.Metadata.Title = "Phase 4";
        document.PageSettings = new DocumentPageSettings
        {
            Size = DocumentPageSize.Letter,
            Margins = new DocumentPageMargins { Top = 72, Right = 54, Bottom = 72, Left = 54 },
            HeaderDistanceFromTop = 30,
            FooterDistanceFromBottom = 30
        };
        document.Sections =
        [
            new DocumentSection
            {
                Id = "section-main",
                Order = 0,
                Title = "Main",
                Properties = new DocumentSectionProperties
                {
                    DifferentFirstPage = true,
                    PageSettings = new DocumentPageSettings
                    {
                        Size = DocumentPageSize.Letter,
                        Margins = new DocumentPageMargins { Top = 72, Right = 54, Bottom = 72, Left = 54 }
                    },
                    Columns = new DocumentSectionColumns
                    {
                        Count = 2,
                        Spacing = 24,
                        SeparatorLine = true,
                        Balance = true,
                        Preset = "two"
                    },
                    LineNumbering = new DocumentLineNumbering
                    {
                        Enabled = true,
                        StartAt = 7,
                        Increment = 2,
                        Restart = DocumentLineNumberingRestart.Section
                    },
                    NoteNumbering = new DocumentNoteNumbering { Style = "decimal", StartAt = 1, RestartEachSection = false },
                    HeaderFooterReferences =
                    [
                        new DocumentHeaderFooterReference { HeaderFooterId = "header-1", Type = DocumentHeaderFooterType.Header, Scope = DocumentHeaderFooterScope.Primary },
                        new DocumentHeaderFooterReference { HeaderFooterId = "footer-1", Type = DocumentHeaderFooterType.Footer, Scope = DocumentHeaderFooterScope.Primary }
                    ]
                }
            },
            new DocumentSection { Id = "section-appendix", Order = 1, Title = "Appendix" }
        ];
        document.NumberingDefinitions.Add(new DocumentNumberingDefinition
        {
            Id = "contract-numbering",
            AbstractId = "contract-abstract",
            Name = "Contract clauses",
            StyleId = "contract-list-style",
            Levels =
            [
                new DocumentNumberingLevel { Level = 0, Format = "decimal", Text = "%1.", StartAt = 1, Suffix = "tab", Indent = 0, Hanging = 18 },
                new DocumentNumberingLevel { Level = 1, Format = "decimal", Text = "%1.%2.", StartAt = 1, Suffix = "tab", Indent = 18, Hanging = 18 },
                new DocumentNumberingLevel { Level = 2, Format = "decimal", Text = "%1.%2.%3.", StartAt = 1, Suffix = "tab", Indent = 36, Hanging = 18 }
            ]
        });
        document.ListStyles.Add(new DocumentListStyle
        {
            Id = "contract-list-style",
            Name = "Contract clauses",
            NumberingId = "contract-numbering",
            IsQuickStyle = true
        });
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "heading-1",
                SectionId = "section-main",
                Type = DocumentBlockType.Heading,
                Order = 0,
                Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Id = "heading-run", Text = "Intro" }] }
            },
            new DocumentBlock
            {
                Id = "paragraph-1",
                SectionId = "section-main",
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                ParagraphProperties = new DocumentParagraphProperties { Alignment = DocumentTextAlignment.Justify, LineSpacing = 1.15, SpacingAfter = 8 },
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Id = "text-1",
                            Text = "Body text",
                            Marks =
                            [
                                new InlineMark { Type = InlineMarkType.Bold },
                                new InlineMark { Type = InlineMarkType.Bookmark, Value = "intro-bookmark" },
                                new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1", AnchorId = "anchor-comment-1" } },
                                new InlineMark { Type = InlineMarkType.Revision, RevisionId = "revision-1" }
                            ]
                        },
                        new DocumentDrawingRun
                        {
                            Id = "drawing-run-1",
                            ObjectId = "drawing-1",
                            Source = DocumentImageSource.Asset,
                            AssetId = "asset-inline",
                            Url = "/images/inline.png",
                            AltText = "Inline drawing",
                            Caption = "Inline caption",
                            Layout = new DocumentObjectLayout
                            {
                                Wrap = { Mode = DocumentWrapMode.Square, DistanceLeft = 6, DistanceRight = 6 },
                                Transform = { Width = 144, Height = 88 },
                                Position = { X = 22, Y = 11 },
                                Stacking = { ZIndex = 12 }
                            },
                            LinkUrl = "https://example.test/inline"
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "list-1",
                SectionId = "section-main",
                Type = DocumentBlockType.List,
                Order = 2,
                Content = new ListBlockContent
                {
                    Ordered = true,
                    IndentLevel = 2,
                    StartNumber = 4,
                    NumberingId = "contract-numbering",
                    AbstractNumberingId = "contract-abstract",
                    ListStyleId = "contract-list-style",
                    NumberFormat = "legal",
                    LevelText = "%1.%2.%3.",
                    Suffix = "tab",
                    LabelIndent = 36,
                    HangingIndent = 18,
                    RestartNumbering = true,
                    NumberingValue = 4,
                    Inlines = [new TextRun { Text = "List item" }]
                }
            },
            new DocumentBlock
            {
                Id = "quote-1",
                SectionId = "section-main",
                Type = DocumentBlockType.Quote,
                Order = 3,
                Content = new QuoteBlockContent { Inlines = [new TextRun { Text = "Quoted" }] }
            },
            new DocumentBlock
            {
                Id = "table-1",
                SectionId = "section-main",
                Type = DocumentBlockType.Table,
                Order = 4,
                Content = CreateTable()
            },
            new DocumentBlock
            {
                Id = "image-1",
                SectionId = "section-main",
                Type = DocumentBlockType.Image,
                Order = 5,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Asset,
                    AssetId = "asset-standalone",
                    Url = "/images/standalone.png",
                    AltText = "Standalone image",
                    Caption = "Standalone caption",
                    LinkUrl = "https://example.test/standalone",
                    Layout = new DocumentObjectLayout
                    {
                        Wrap = { Mode = DocumentWrapMode.TopBottom },
                        Transform = { Width = 260, Height = 140 },
                        Position = { X = 35, Y = 48 },
                        Stacking = { ZIndex = 5 }
                    }
                }
            },
            new DocumentBlock
            {
                Id = "page-break-1",
                SectionId = "section-main",
                Type = DocumentBlockType.PageBreak,
                Order = 6,
                Content = new PageBreakBlockContent
                {
                    BreakType = DocumentSectionBreakType.NextPage,
                    NextSectionId = "section-appendix"
                }
            }
        ];
        document.HeadersFooters =
        [
            new DocumentHeaderFooter
            {
                Id = "header-1",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary,
                SectionId = "section-main",
                Blocks =
                [
                    Paragraph("header-p", new DocumentFieldRun { Id = "field-page", FieldType = DocumentFieldType.PageNumber, DisplayText = "1" })
                ]
            },
            new DocumentHeaderFooter
            {
                Id = "footer-1",
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary,
                SectionId = "section-main",
                Blocks = [Paragraph("footer-p", new TextRun { Text = "Footer" })]
            }
        ];
        document.Notes =
        [
            new DocumentNote
            {
                Id = "note-1",
                Type = DocumentNoteType.Footnote,
                SectionId = "section-main",
                Marker = "1",
                ReferenceIds = ["note-ref-1"],
                Blocks = [Paragraph("note-p", new TextRun { Text = "Footnote text" })]
            }
        ];
        document.Comments =
        [
            new DocumentComment
            {
                Id = "comment-1",
                Status = DocumentCommentStatus.Open,
                Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.TextRange, BlockId = "paragraph-1", StartInlineIndex = 0, StartOffset = 0, EndInlineIndex = 0, EndOffset = 9 },
                Entries =
                [
                    new DocumentCommentEntry
                    {
                        Id = "comment-entry-1",
                        Text = "Body text",
                        Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Reviewer" },
                        CreatedAt = DateTimeOffset.Parse("2026-06-04T10:00:00+00:00")
                    }
                ]
            }
        ];
        document.Revisions =
        [
            new DocumentRevision
            {
                Id = "revision-1",
                Type = DocumentRevisionType.Formatting,
                Range = new DocumentRevisionRange { BlockId = "paragraph-1", StartInlineIndex = 0, StartOffset = 0, EndInlineIndex = 0, EndOffset = 9 },
                Author = new DocumentRevisionAuthor { Id = "author-1", DisplayName = "Reviewer" },
                CreatedAt = DateTimeOffset.Parse("2026-06-04T11:00:00+00:00"),
                Action = DocumentRevisionAction.Pending,
                PayloadJson = """{"mark":"bold"}"""
            }
        ];
        document.Assets =
        [
            new DocumentImageAsset { Id = "asset-inline", FileName = "inline.png", ContentType = "image/png", Url = "/images/inline.png" },
            new DocumentImageAsset { Id = "asset-standalone", FileName = "standalone.png", ContentType = "image/png", Url = "/images/standalone.png" }
        ];
        return document;
    }

    private static TableBlockContent CreateTable()
        => new()
        {
            Layout = new TableLayoutContent
            {
                Width = 520,
                Alignment = TableHorizontalAlignment.Center,
                BackgroundColor = "#f8fafc",
                CellPadding = 8
            },
            Rows =
            [
                new TableRowContent
                {
                    Cells =
                    [
                        new TableCellContent
                        {
                            Id = "cell-header",
                            ColumnSpan = 2,
                            IsHeader = true,
                            BackgroundColor = "#e0f2fe",
                            VerticalAlignment = TableCellVerticalAlignment.Middle,
                            Blocks = [Paragraph("cell-header-p", new TextRun { Text = "Header" })]
                        }
                    ]
                },
                new TableRowContent
                {
                    Cells =
                    [
                        new TableCellContent
                        {
                            Id = "cell-body",
                            RowSpan = 2,
                            Width = 180,
                            Blocks = [Paragraph("cell-body-p", new TextRun { Text = "Body cell" })]
                        }
                    ]
                }
            ]
        };

    private static DocumentBlock Paragraph(string id, InlineContent inline)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [inline] }
        };

    private static TextRun Text(string id, string text, InlineMarkType markType)
        => new()
        {
            Id = id,
            Text = text,
            Marks = [new InlineMark { Type = markType }]
        };

    private static void AssertMark(ParagraphBlockContent paragraph, string runId, InlineMarkType markType, string? value)
    {
        var run = Assert.IsType<TextRun>(paragraph.Inlines.Single(inline => inline.Id == runId));
        var mark = Assert.Single(run.Marks, candidate => candidate.Type == markType);
        Assert.Equal(value, mark.Value);
    }

    private static string CellText(TableCellContent cell)
        => CellOrParagraphText(cell.Blocks[0]);

    private static string CellOrParagraphText(DocumentBlock block)
    {
        var paragraph = Assert.IsType<ParagraphBlockContent>(block.Content);
        return Assert.IsType<TextRun>(paragraph.Inlines[0]).Text;
    }
}
