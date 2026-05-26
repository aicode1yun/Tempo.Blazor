using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo document editor store.</summary>
public class DemoDocumentEditorStore : InMemoryDocumentEditorProvider
{
    private const string DemoImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAUAAAAC0CAYAAADl5PURAAACsUlEQVR42u3ZzQ2CQBCAUYuwFLuxDjuwBDqgAK9UZfwB9ECC04JGdJd5hzeZ80C+y252+9MMkNHGEQABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEBAAB0CEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAgAIDOD6nGSAjAQQEEEAAAQQQQAABBBBAAAEEEKDiAA4xADISQEAAAQQQQAABBBBAAAEEEEAAAQQQQAB/pWk74MsEUABBAAVQAEEABVAAQQAFUABBAAUQYBUBfMQCkJAAAgIIIIAAAggggAACCCCAAAIIUHEA+xgAGQkgIIAAAliJ8zgDhRNAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBHIE8D5Oc438XFC+0jsigIAACiAggAIICKAAAgIogIAACiAggAIICKAAAgIogIAACiAggAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKICAAAogIIACCAjgGwG8xViT7fGwiLXdCag4gEIHpAig2AECKHSAAIodkDiAPh6Q9hEEQAABPg7gEAtAQgIICCCAAAIIIIAAAggggAACCCCAAAIIIIAAAgjgMpq2g2KIiQAKIAKIAAogAogACiACiAAKIAJIgQG8xgDISAABAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQIC/BrCPBSAhAQQEEEAAAQQQQAABBBBAAAEEEKDiAF5iAGQkgIAAAgggQBIvAt6vRwtbqO0AAAAASUVORK5CYII=";
    private const string ContractAssetId = "contract-evidence-asset";
    private const string ExhibitAssetId = "exhibit-provider-asset";
    private static readonly DateTimeOffset CanonicalDemoTimestamp = new(2026, 5, 22, 6, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<string, StoredDocumentImage> _images = [];
    private readonly InMemoryDocumentRenditionProvider _renditionProvider;

    /// <summary>Creates a seeded server-side document store.</summary>
    public DemoDocumentEditorStore()
    {
        _renditionProvider = new InMemoryDocumentRenditionProvider(this, this);
        SeedDemoDocuments();
    }

    /// <summary>Resets the demo store back to its seeded documents.</summary>
    public void Reset()
    {
        ClearStore();
        _images.Clear();
        SeedDemoDocuments();
    }

    private void SeedDemoDocuments()
    {
        var contract = SeedContractDocument("contract-demo");
        var filing = SeedFilingDocument("filing-demo");
        var exhibits = CreateExhibitsDocument("exhibits-demo");
        var table = CreateTablePropertiesDocument("table-demo");
        var recovery = SeedRecoveryDocument();
        var onlyOfficeParity = SeedOnlyOfficeParityDocument();

        SeedProviderImages();
        PrepareContractDemo(contract);
        StoreDocument(contract, "contract-demo-canonical-v1");
        StoreVersion(CreateCanonicalContractVersion(contract));

        _ = SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = filing.DocumentId,
            Document = filing,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        _ = SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = exhibits.DocumentId,
            Document = exhibits,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        _ = SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = table.DocumentId,
            Document = table,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        StoreDocument(recovery, "recovery-2026-05-23-canonical-v1");
        StoreDocument(onlyOfficeParity, "onlyoffice-parity-2026-05-24-canonical-v1");
    }

    private void SeedProviderImages()
    {
        var bytes = DecodeDataUri(DemoImageUrl);
        _images[ContractAssetId] = new StoredDocumentImage(ContractAssetId, "contract-provider-evidence.png", "image/png", bytes);
        _images[ExhibitAssetId] = new StoredDocumentImage(ExhibitAssetId, "exhibit-provider-evidence.png", "image/png", bytes);
    }

    private static void PrepareContractDemo(DocumentEditorDocument contract)
    {
        contract.Metadata.CreatedAt = CanonicalDemoTimestamp;
        contract.Metadata.ModifiedAt = CanonicalDemoTimestamp;
        contract.Metadata.Author = DemoAuthor;
        contract.Metadata.Status = DocumentEditorStatus.Review;
        contract.Metadata.Description = "Stable engine quality demo document.";

        contract.Assets =
        [
            CreateImageAsset(ContractAssetId, contract.DocumentId, "contract-provider-evidence.png", "Provider-managed exhibit", "Image resolved through IDocumentImageUrlResolver"),
            CreateImageAsset(ExhibitAssetId, contract.DocumentId, "exhibit-provider-evidence.png", "Provider exhibit", "Provider-backed exhibit image")
        ];

        var clientToken = contract.Blocks
            .SelectMany(GetInlineContent)
            .FirstOrDefault(inline => inline.Id == "contract-client-token");
        if (clientToken is not null)
        {
            clientToken.Marks.Add(new InlineMark
            {
                Type = InlineMarkType.CommentAnchor,
                CommentAnchor = new CommentAnchorMarkData
                {
                    CommentId = "contract-comment-client-token",
                    AnchorId = "contract-comment-client-token-anchor"
                }
            });
        }

        contract.Blocks.Add(CreateParagraph(
            "contract-normal-overview",
            28,
            "The agreement keeps a compact first page with realistic contract text, review markup, image wrapping, captions, an accessibility warning, and a small pricing table. Every block uses stable identifiers so E2E tests can compare the canonical reset without being disturbed by random demo data.",
            spacingAfter: 14));

        contract.Blocks.Add(CreateImage(
            "contract-left-wrap-image",
            31,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Asset evidence preview",
            "Evidence preview loaded from the demo image provider",
            148,
            84,
            DocumentImageAlignment.Start,
            CreateLeftWrappedImageLayout(148, 84, "contract-left-wrap-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-left-wrap-text",
            32,
            "This paragraph demonstrates a left positioned evidence preview. Text must start beside the image, wrap around its square contour, remain editable on every visual line, and continue below the object without colliding with the caption.",
            spacingAfter: 16));

        contract.Blocks.Add(CreateImage(
            "contract-right-wrap-image",
            41,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Right aligned appendix preview",
            "Right wrapped exhibit preview",
            148,
            84,
            DocumentImageAlignment.End,
            CreateRightWrappedImageLayout(148, 84, "contract-right-wrap-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-right-wrap-text",
            42,
            "This paragraph proves the opposite wrap direction. The image is anchored to the same paragraph on the right, while the text remains readable and clickable on the left. The demo intentionally keeps enough words here to exercise multiple wrapped lines.",
            spacingAfter: 16));

        contract.Blocks.Add(CreateImage(
            "contract-top-bottom-image",
            50,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Provider-managed exhibit",
            "Image resolved through IDocumentImageUrlResolver",
            220,
            124,
            DocumentImageAlignment.Center,
            CreateTopBottomImageLayout(220, 124, "contract-top-bottom-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-top-bottom-text",
            51,
            "Top and bottom wrapping should reserve the full object band. No text line is allowed to slide horizontally through this image because that would make the page feel unpredictable.",
            spacingAfter: 16));

        contract.Blocks.Add(CreatePageBreak("contract-engine-scenarios-page-break", 55));

        contract.Blocks.Add(CreateImage(
            "contract-inline-image",
            60,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Inline evidence thumbnail",
            "Inline evidence image with caption",
            156,
            88,
            DocumentImageAlignment.Center,
            DocumentObjectLayout.Inline()));

        contract.Blocks.Add(CreateImage(
            "contract-missing-alt-image",
            70,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            null,
            "Accessibility sample: missing alt text",
            156,
            88,
            DocumentImageAlignment.Center,
            DocumentObjectLayout.Inline()));

        contract.Blocks.Add(CreateContractTable());
        contract.Comments.Add(CreateCanonicalComment());
        AddCanonicalDeletionRevision(contract);
        DocumentImagePersistence.ConvertImageBlocksToDrawingRuns(contract);
        DocumentImagePersistence.Sanitize(contract);
    }

    private static DocumentObjectLayout CreateLeftWrappedImageLayout(double width, double height, string? anchorBlockId = null) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = true,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Left
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceRight = 16,
                DistanceBottom = 12
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            }
        };

    private static DocumentObjectLayout CreateRightWrappedImageLayout(double width, double height, string? anchorBlockId = null) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = true,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Right
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceLeft = 16,
                DistanceBottom = 12
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            }
        };

    private static DocumentObjectLayout CreateTopBottomImageLayout(double width, double height, string? anchorBlockId = null) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = true,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Center
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.TopBottom,
                DistanceTop = 10,
                DistanceBottom = 12
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            }
        };

    private static DocumentBlock CreateParagraph(string id, double order, string text, double spacingAfter = 10, DocumentTextAlignment alignment = DocumentTextAlignment.Left) =>
        new()
        {
            Id = id,
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Paragraph,
            Order = order,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = alignment,
                LineSpacing = 1.25,
                SpacingAfter = spacingAfter
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = $"{id}-text",
                        Text = text
                    }
                ]
            }
        };

    private static DocumentBlock CreatePageBreak(string id, double order) =>
        new()
        {
            Id = id,
            SectionId = "contract-section-main",
            Type = DocumentBlockType.PageBreak,
            Order = order
        };

    private static DocumentBlock CreateImage(
        string id,
        double order,
        DocumentImageSource source,
        string? url,
        string? assetId,
        string? altText,
        string caption,
        double width,
        double height,
        DocumentImageAlignment alignment,
        DocumentObjectLayout layout,
        string? sectionId = "contract-section-main")
    {
        var drawing = CreateImageDrawingRun(id, source, url, assetId, altText, caption, width, height, layout);
        return new DocumentBlock
        {
            Id = id,
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Order = order,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = ToTextAlignment(alignment),
                LineSpacing = 1.25,
                SpacingAfter = 9
            },
            Content = new ParagraphBlockContent
            {
                Inlines = [drawing]
            }
        };
    }

    private static DocumentDrawingRun CreateImageDrawingRun(
        string objectId,
        DocumentImageSource source,
        string? url,
        string? assetId,
        string? altText,
        string caption,
        double width,
        double height,
        DocumentObjectLayout layout)
    {
        layout.Anchor ??= new DocumentObjectAnchor();
        layout.Position ??= new DocumentObjectPosition();
        layout.Wrap ??= new DocumentObjectWrap();
        layout.Transform ??= new DocumentObjectTransform();
        layout.Stacking ??= new DocumentObjectStacking();
        layout.Anchor.BlockId = string.IsNullOrWhiteSpace(layout.Anchor.BlockId) ? objectId : layout.Anchor.BlockId;
        layout.Anchor.InlineIndex ??= 0;
        layout.Anchor.Offset ??= 0;
        layout.Transform.Width ??= width;
        layout.Transform.Height ??= height;
        layout.Transform.NaturalWidth ??= width;
        layout.Transform.NaturalHeight ??= height;

        var drawing = new DocumentDrawingRun
        {
            Id = $"{objectId}-drawing",
            ObjectId = objectId,
            Source = source,
            Url = source == DocumentImageSource.Url ? url : null,
            AssetId = source == DocumentImageSource.Asset ? assetId : null,
            AltText = altText,
            Caption = caption,
            Size = new DocumentImageSize { Width = width, Height = height },
            NaturalSize = new DocumentImageSize { Width = width, Height = height },
            Layout = layout
        };
        DocumentImagePersistence.Sanitize(drawing);
        return drawing;
    }

    private static DocumentTextAlignment ToTextAlignment(DocumentImageAlignment alignment)
        => alignment switch
        {
            DocumentImageAlignment.Center => DocumentTextAlignment.Center,
            DocumentImageAlignment.End => DocumentTextAlignment.Right,
            _ => DocumentTextAlignment.Left
        };

    private static DocumentBlock CreateContractTable() =>
        new()
        {
            Id = "contract-pricing-table",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Table,
            Order = 80,
            Content = new TableBlockContent
            {
                Layout = new TableLayoutContent
                {
                    Width = 420,
                    Alignment = TableHorizontalAlignment.Center,
                    CellPadding = 7,
                    BackgroundColor = "#ffffff",
                    Borders = new TableCellBorders
                    {
                        Top = "1px solid #cbd5e1",
                        Right = "1px solid #cbd5e1",
                        Bottom = "1px solid #cbd5e1",
                        Left = "1px solid #cbd5e1"
                    }
                },
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Item", isHeader: true, backgroundColor: "#eef2ff", id: "contract-pricing-table-h-item"),
                            CreateTableCell("Responsibility", isHeader: true, backgroundColor: "#eef2ff", id: "contract-pricing-table-h-responsibility"),
                            CreateTableCell("Status", isHeader: true, backgroundColor: "#eef2ff", id: "contract-pricing-table-h-status")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Implementation", id: "contract-pricing-table-r1-item"),
                            CreateTableCell("Provider", id: "contract-pricing-table-r1-responsibility"),
                            CreateTableCell("Ready for review", id: "contract-pricing-table-r1-status")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Client data", id: "contract-pricing-table-r2-item"),
                            CreateTableCell("Client", id: "contract-pricing-table-r2-responsibility"),
                            CreateTableCell("Pending confirmation", id: "contract-pricing-table-r2-status")
                        ]
                    }
                ]
            }
        };

    private static DocumentComment CreateCanonicalComment() =>
        new()
        {
            Id = "contract-comment-client-token",
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "contract-intro",
                StartInlineIndex = 1,
                EndInlineIndex = 1,
                StartOffset = 0,
                EndOffset = "Client name".Length,
                ExternalAnchorId = "contract-comment-client-token-anchor"
            },
            Visibility = DocumentCommentVisibility.Internal,
            Entries =
            [
                new DocumentCommentEntry
                {
                    Id = "contract-comment-client-token-entry-1",
                    Author = DemoAuthor,
                    Text = "Check whether the client token is resolved before export.",
                    CreatedAt = CanonicalDemoTimestamp
                }
            ]
        };

    private static void AddCanonicalDeletionRevision(DocumentEditorDocument contract)
    {
        var scope = contract.Blocks.FirstOrDefault(block => block.Id == "contract-scope");
        if (scope?.Content is not ParagraphBlockContent paragraph)
        {
            return;
        }

        paragraph.Inlines.Add(new TextRun
        {
            Id = "contract-scope-deleted-run",
            Text = " Legacy onboarding language will be removed.",
            Marks =
            [
                new InlineMark
                {
                    Type = InlineMarkType.Revision,
                    RevisionId = "contract-revision-deletion",
                    Value = "Deletion"
                }
            ]
        });

        contract.Revisions.Add(new DocumentRevision
        {
            Id = "contract-revision-deletion",
            Type = DocumentRevisionType.Deletion,
            Range = new DocumentRevisionRange
            {
                BlockId = "contract-scope",
                StartInlineIndex = paragraph.Inlines.Count - 1,
                EndInlineIndex = paragraph.Inlines.Count - 1,
                StartOffset = 0,
                EndOffset = " Legacy onboarding language will be removed.".Length
            },
            Author = new DocumentRevisionAuthor
            {
                Id = "demo-reviewer",
                DisplayName = "Demo Reviewer",
                Email = "reviewer@example.local"
            },
            CreatedAt = CanonicalDemoTimestamp.AddMinutes(5),
            Action = DocumentRevisionAction.Pending,
            PayloadJson = "Legacy onboarding language will be removed."
        });
    }

    private static DocumentImageAsset CreateImageAsset(
        string id,
        string documentId,
        string fileName,
        string altText,
        string caption)
    {
        var bytes = DecodeDataUri(DemoImageUrl);
        return new DocumentImageAsset
        {
            Id = id,
            DocumentId = documentId,
            Source = DocumentImageSource.Asset,
            ContentType = "image/png",
            FileName = fileName,
            SizeBytes = bytes.LongLength,
            AltText = altText,
            Caption = caption,
            ImageSize = new DocumentImageSize { Width = 240, Height = 135 }
        };
    }

    private static DocumentVersion CreateCanonicalContractVersion(DocumentEditorDocument contract)
    {
        var json = DocumentEditorJson.Serialize(contract);
        var snapshot = new DocumentVersionSnapshot
        {
            DocumentId = contract.DocumentId,
            SchemaVersion = contract.SchemaVersion,
            Json = json
        };
        snapshot.Hash = DocumentVersionHashHelper.ComputeSnapshotHash(snapshot);

        return new DocumentVersion
        {
            Id = "contract-version-1-0",
            DocumentId = contract.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "1.0",
            Description = "Initial demo version",
            Author = DemoAuthor,
            CreatedAt = CanonicalDemoTimestamp,
            Snapshot = snapshot
        };
    }

    private static IEnumerable<InlineContent> GetInlineContent(DocumentBlock block)
        => block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => []
        };

    private static byte[] DecodeDataUri(string dataUri)
    {
        const string marker = "base64,";
        var index = dataUri.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? []
            : Convert.FromBase64String(dataUri[(index + marker.Length)..]);
    }

    /// <summary>Saves a demo image asset.</summary>
    public async Task<DocumentImageAsset> SaveImageAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var id = $"img-{Guid.NewGuid():N}";
        var bytes = memory.ToArray();
        _images[id] = new StoredDocumentImage(id, fileName, contentType, bytes);
        return new DocumentImageAsset
        {
            Id = id,
            ContentType = contentType,
            FileName = fileName,
            SizeBytes = bytes.LongLength,
            Source = DocumentImageSource.Asset
        };
    }

    /// <summary>Gets a stored demo image.</summary>
    public StoredDocumentImage? GetImage(string id)
    {
        return _images.TryGetValue(id, out var image) ? image : null;
    }

    /// <summary>Creates an immutable rendition from a saved document version.</summary>
    public Task<DocumentRenditionResult> CreateRenditionAsync(
        DocumentRenditionRequest request,
        CancellationToken cancellationToken = default)
    {
        return _renditionProvider.CreateRenditionAsync(request, cancellationToken);
    }

    /// <summary>Gets a finalized rendition.</summary>
    public Task<DocumentRendition?> GetRenditionAsync(string renditionId, CancellationToken cancellationToken = default)
    {
        return _renditionProvider.GetRenditionAsync(renditionId, cancellationToken);
    }

    /// <summary>Gets rendition pages.</summary>
    public Task<IReadOnlyList<DocumentRenditionPage>> GetRenditionPagesAsync(string renditionId, CancellationToken cancellationToken = default)
    {
        return _renditionProvider.GetPagesAsync(renditionId, cancellationToken);
    }

    /// <summary>Gets rendition anchors.</summary>
    public Task<IReadOnlyList<DocumentRenditionAnchor>> GetRenditionAnchorMapAsync(string renditionId, CancellationToken cancellationToken = default)
    {
        return _renditionProvider.GetAnchorMapAsync(renditionId, cancellationToken);
    }

    /// <summary>Stored demo image bytes.</summary>
    public sealed record StoredDocumentImage(string Id, string FileName, string ContentType, byte[] Content);

    private static DocumentEditorAuthor DemoAuthor => new()
    {
        Id = "demo-user",
        DisplayName = "Demo User",
        Email = "demo@example.local"
    };

    private static DocumentEditorDocument CreateExhibitsDocument(string documentId)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Evidence exhibit";
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = "Evidence exhibit" }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "This demo document keeps image drawing runs in the editor JSON model." }]
            }
        });
        document.Blocks.Add(CreateImage(
            "exhibits-url-image",
            30,
            DocumentImageSource.Asset,
            null,
            ExhibitAssetId,
            "Provider exhibit",
            "Image inserted from the demo provider",
            220,
            124,
            DocumentImageAlignment.Start,
            CreateLeftWrappedImageLayout(220, 124),
            sectionId: null));
        document.Blocks.Add(CreateImage(
            "exhibits-provider-image",
            40,
            DocumentImageSource.Asset,
            null,
            ExhibitAssetId,
            "Provider exhibit",
            "Image resolved through the demo image provider",
            240,
            135,
            DocumentImageAlignment.Center,
            CreateTopBottomImageLayout(240, 135),
            sectionId: null));
        DocumentImagePersistence.ConvertImageBlocksToDrawingRuns(document);
        DocumentImagePersistence.Sanitize(document);
        return document;
    }

    private static DocumentEditorDocument CreateTablePropertiesDocument(string documentId)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Table properties demo";
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = "Table properties demo" }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Select a table cell to open row, column, table, and cell property controls." }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Order = 30,
            Content = new TableBlockContent
            {
                Layout = new TableLayoutContent
                {
                    Width = 640,
                    Alignment = TableHorizontalAlignment.Center,
                    CellPadding = 8,
                    BackgroundColor = "#f8fafc",
                    Borders = new TableCellBorders
                    {
                        Top = "1px solid #94a3b8",
                        Right = "1px solid #94a3b8",
                        Bottom = "1px solid #94a3b8",
                        Left = "1px solid #94a3b8"
                    }
                },
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Feature", isHeader: true, backgroundColor: "#e2e8f0"),
                            CreateTableCell("Demo value", isHeader: true, backgroundColor: "#e2e8f0"),
                            CreateTableCell("UX check", isHeader: true, backgroundColor: "#e2e8f0")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Width"),
                            CreateTableCell("640 px"),
                            CreateTableCell("Resize from the properties panel")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Alignment"),
                            CreateTableCell("Centered"),
                            CreateTableCell("Switch left, center, or right")
                        ]
                    }
                ]
            }
        });
        return document;
    }

    private static TableCellContent CreateTableCell(string text, bool isHeader = false, string? backgroundColor = null, string? id = null)
    {
        return new TableCellContent
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            IsHeader = isHeader,
            BackgroundColor = backgroundColor,
            Padding = 8,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = id is null ? Guid.NewGuid().ToString("N") : $"{id}-block",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Id = id is null ? null : $"{id}-text", Text = text }]
                    }
                }
            ]
        };
    }
}
