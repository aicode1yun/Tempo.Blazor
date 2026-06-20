using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.E2E;

/// <summary>Phase 4 provider-boundary round-trip coverage for the canonical canvas document model.</summary>
[TestClass]
[TestCategory("DocumentEditor")]
[TestCategory("DocumentEditor:CanvasEngine")]
public sealed class DocumentEditorCanvasModelRoundtripE2ETests
{
    [TestMethod]
    public async Task Phase4_CanvasModelRoundtrip_SaveReloadPreservesStructuredSnapshot()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var source = CreateProviderSeedDocument();

        var firstSave = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = source.DocumentId,
            Document = source,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        });
        Assert.IsTrue(firstSave.Success);

        var baseline = await provider.LoadAsync(source.DocumentId);
        Assert.IsTrue(baseline.Found);
        Assert.IsNotNull(baseline.Document);
        Assert.IsNotNull(baseline.JsonSnapshot);

        var canvasModel = CanvasDocumentModelConverter.ToCanvasModel(baseline.Document);
        var rebuiltDocument = CanvasDocumentModelConverter.FromCanvasModel(canvasModel);
        var secondSave = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = rebuiltDocument.DocumentId,
            JsonSnapshot = DocumentEditorJson.Serialize(rebuiltDocument),
            BaseConcurrencyToken = baseline.ConcurrencyToken,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Optional,
            NormalizeJson = true
        });
        Assert.IsTrue(secondSave.Success);

        var reloaded = await provider.LoadAsync(source.DocumentId);
        Assert.IsTrue(reloaded.Found);
        Assert.IsNotNull(reloaded.JsonSnapshot);

        var baselineJson = DocumentEditorJson.Normalize(baseline.JsonSnapshot);
        var reloadedJson = DocumentEditorJson.Normalize(reloaded.JsonSnapshot);
        Assert.AreEqual(baselineJson, reloadedJson);
    }

    private static DocumentEditorDocument CreateProviderSeedDocument()
    {
        var document = DocumentEditorDocument.Empty("phase-4-provider-roundtrip");
        document.Metadata.Title = "Canvas model provider boundary";
        document.Metadata.CreatedAt = DateTimeOffset.Parse("2026-06-04T08:00:00+00:00");
        document.PageSettings = new DocumentPageSettings
        {
            Size = DocumentPageSize.Letter,
            Margins = new DocumentPageMargins { Top = 72, Right = 54, Bottom = 72, Left = 54 }
        };
        document.Sections[0].Id = "section-main";
        document.Sections[0].Properties.HeaderFooterReferences =
        [
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "header-primary",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary
            }
        ];
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "heading",
                SectionId = "section-main",
                Type = DocumentBlockType.Heading,
                Order = 0,
                Content = new HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new TextRun { Id = "heading-run", Text = "Provider Boundary" }]
                }
            },
            new DocumentBlock
            {
                Id = "paragraph",
                SectionId = "section-main",
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Id = "paragraph-text",
                            Text = "Round-trip body",
                            Marks =
                            [
                                new InlineMark { Type = InlineMarkType.Bold },
                                new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-provider" } },
                                new InlineMark { Type = InlineMarkType.Revision, RevisionId = "revision-provider" }
                            ]
                        },
                        new DocumentDrawingRun
                        {
                            Id = "drawing-inline-run",
                            ObjectId = "drawing-inline",
                            Source = DocumentImageSource.Asset,
                            AssetId = "asset-inline",
                            Url = "/images/provider-inline.png",
                            AltText = "Provider inline drawing",
                            Layout = new DocumentObjectLayout
                            {
                                Wrap = { Mode = DocumentWrapMode.Square },
                                Transform = { Width = 120, Height = 90 },
                                Stacking = { ZIndex = 4 }
                            }
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "table",
                SectionId = "section-main",
                Type = DocumentBlockType.Table,
                Order = 2,
                Content = new TableBlockContent
                {
                    Layout = new TableLayoutContent { Width = 420, Alignment = TableHorizontalAlignment.Center },
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent
                                {
                                    Id = "cell-a",
                                    ColumnSpan = 2,
                                    IsHeader = true,
                                    Blocks =
                                    [
                                        new DocumentBlock
                                        {
                                            Id = "cell-a-p",
                                            Type = DocumentBlockType.Paragraph,
                                            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "A" }] }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        ];
        document.HeadersFooters =
        [
            new DocumentHeaderFooter
            {
                Id = "header-primary",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary,
                SectionId = "section-main",
                Blocks =
                [
                    new DocumentBlock
                    {
                        Id = "header-p",
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent
                        {
                            Inlines = [new DocumentFieldRun { Id = "page-field", FieldType = DocumentFieldType.PageNumber, DisplayText = "1" }]
                        }
                    }
                ]
            }
        ];
        document.Notes =
        [
            new DocumentNote
            {
                Id = "note-provider",
                Type = DocumentNoteType.Footnote,
                ReferenceIds = ["note-ref-provider"],
                Blocks =
                [
                    new DocumentBlock
                    {
                        Id = "note-p",
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Provider footnote" }] }
                    }
                ]
            }
        ];
        document.Comments =
        [
            new DocumentComment
            {
                Id = "comment-provider",
                Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.TextRange, BlockId = "paragraph", StartOffset = 0, EndOffset = 15 },
                Entries =
                [
                    new DocumentCommentEntry
                    {
                        Id = "comment-entry-provider",
                        Text = "Provider comment",
                        Author = new DocumentEditorAuthor { Id = "reviewer", DisplayName = "Reviewer" },
                        CreatedAt = DateTimeOffset.Parse("2026-06-04T09:00:00+00:00")
                    }
                ]
            }
        ];
        document.Revisions =
        [
            new DocumentRevision
            {
                Id = "revision-provider",
                Type = DocumentRevisionType.Formatting,
                Range = new DocumentRevisionRange { BlockId = "paragraph", StartOffset = 0, EndOffset = 15 },
                Author = new DocumentRevisionAuthor { Id = "reviewer", DisplayName = "Reviewer" },
                CreatedAt = DateTimeOffset.Parse("2026-06-04T09:30:00+00:00"),
                PayloadJson = """{"mark":"bold"}"""
            }
        ];
        document.Assets =
        [
            new DocumentImageAsset
            {
                Id = "asset-inline",
                DocumentId = document.DocumentId,
                Url = "/images/provider-inline.png",
                FileName = "provider-inline.png",
                ContentType = "image/png"
            }
        ];
        return document;
    }
}
