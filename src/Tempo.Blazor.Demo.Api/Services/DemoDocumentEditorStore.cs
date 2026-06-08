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
        var canvasAdvancedCharacter = CreateCanvasAdvancedCharacterDocument("phase-e6-canvas-advanced-char");
        var canvasCommentsRevisions = CreateCanvasCommentsRevisionsDocument("phase-17-canvas-comments-revisions");
        var canvasSearchOutlineToc = SeedCanvasSearchOutlineTocDocument();

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
        StoreDocument(canvasAdvancedCharacter, "phase-e6-canvas-advanced-char-canonical-v1");
        StoreDocument(canvasCommentsRevisions, "phase-17-canvas-comments-revisions-canonical-v1");
        StoreDocument(canvasSearchOutlineToc, "phase-18-canvas-search-outline-toc-canonical-v1");
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

        contract.Blocks.Add(CreateImageDrawingParagraph(
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

        contract.Blocks.Add(CreateImageDrawingParagraph(
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

        contract.Blocks.Add(CreateImageDrawingParagraph(
            "contract-center-wrap-image",
            45,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Centered square evidence preview",
            "Centered square wrapped exhibit preview",
            132,
            74,
            DocumentImageAlignment.Center,
            CreateCenterWrappedImageLayout(132, 74, "contract-center-wrap-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-center-wrap-text",
            46,
            "This center square scenario deliberately contains enough words to fill both sides of the centered preview. The first lines should split into a left interval and a right interval, continue around the object, and then return to a normal full-width line below the image without becoming a top-and-bottom band.",
            spacingAfter: 16));

        contract.Blocks.Add(CreateImageDrawingParagraph(
            "contract-offset-wrap-image",
            48,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Offset square evidence preview",
            "Square image with arbitrary drag-like offset",
            118,
            66,
            DocumentImageAlignment.Start,
            CreateOffsetWrappedImageLayout(118, 66, "contract-offset-wrap-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-offset-wrap-text",
            49,
            "This paragraph is anchored to an arbitrary drag-like offset rather than a preset left, center, or right alignment. Text should adapt to the actual rectangle position, preserve ordinary word spacing, and remain easy to select before, beside, and after the image.",
            spacingAfter: 16));

        contract.Blocks.Add(CreateImageDrawingParagraph(
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
            "Top and bottom wrapping should reserve the full object band. No text line is allowed to slide horizontally through this image because that would make the page feel unpredictable, especially after saving, resetting, and reloading the demo document.",
            spacingAfter: 16));

        contract.Blocks.Add(CreateImageDrawingParagraph(
            "contract-tight-wrap-image",
            52,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Tight contour evidence preview",
            "Tight wrapped image with a custom diamond contour",
            128,
            72,
            DocumentImageAlignment.Center,
            CreateTightWrappedImageLayout(128, 72, "contract-tight-wrap-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-tight-wrap-text",
            53,
            "This tight wrapping paragraph uses a custom diamond contour so the available line intervals differ from a plain rectangle. The text is intentionally stable and descriptive, giving E2E checks predictable words while proving that polygon metadata can survive save, reload, and DOCX export workflows.",
            spacingAfter: 16));

        contract.Blocks.Add(CreateImageDrawingParagraph(
            "contract-in-front-image",
            54,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "In front floating evidence badge",
            "In front of text object positioned in the page margin",
            96,
            54,
            DocumentImageAlignment.End,
            CreateInFrontImageLayout(96, 54, "contract-layering-text")));

        contract.Blocks.Add(CreateImageDrawingParagraph(
            "contract-behind-text-image",
            54.25,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Behind text evidence watermark",
            "Behind text object positioned in the page margin",
            96,
            54,
            DocumentImageAlignment.End,
            CreateBehindTextImageLayout(96, 54, "contract-layering-text")));

        contract.Blocks.Add(CreateParagraph(
            "contract-layering-text",
            54.5,
            "This layering scenario keeps one image in front of text and another behind text without hiding the paragraph itself. Both badges are deliberately positioned in the page margin so the demo exercises front and behind layer serialization while keeping the contract copy readable and overlap-free.",
            spacingAfter: 16));

        contract.Blocks.Add(CreatePageBreak("contract-engine-scenarios-page-break", 55));

        contract.Blocks.Add(CreateImageDrawingParagraph(
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

        contract.Blocks.Add(CreateImageDrawingParagraph(
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
        AddContractHeaderFooterDrawingRuns(contract);
        contract.Comments.Add(CreateCanonicalComment());
        AddCanonicalDeletionRevision(contract);
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

    private static DocumentObjectLayout CreateCenterWrappedImageLayout(double width, double height, string? anchorBlockId = null) =>
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
                HorizontalAlignment = DocumentImageHorizontalPosition.Center,
                Y = 0
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceLeft = 10,
                DistanceRight = 10,
                DistanceBottom = 10
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

    private static DocumentObjectLayout CreateOffsetWrappedImageLayout(double width, double height, string? anchorBlockId = null) =>
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
                X = 214,
                Y = 4
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceLeft = 10,
                DistanceRight = 12,
                DistanceBottom = 10
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

    private static DocumentObjectLayout CreateTightWrappedImageLayout(double width, double height, string? anchorBlockId = null) =>
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
                Mode = DocumentWrapMode.Tight,
                Side = DocumentObjectWrapSide.Largest,
                DistanceLeft = 8,
                DistanceRight = 8,
                DistanceTop = 4,
                DistanceBottom = 8,
                WrapContourPoints =
                [
                    new() { X = 0.5, Y = 0 },
                    new() { X = 1, Y = 0.45 },
                    new() { X = 0.62, Y = 1 },
                    new() { X = 0, Y = 0.55 }
                ]
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

    private static DocumentObjectLayout CreateInFrontImageLayout(double width, double height, string? anchorBlockId = null) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Fixed,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = false,
                FixedOnPage = true
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Page,
                X = -124,
                Y = 250
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.InFrontOfText
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            },
            Stacking = new DocumentObjectStacking
            {
                ZIndex = 20,
                AllowOverlap = true
            }
        };

    private static DocumentObjectLayout CreateBehindTextImageLayout(double width, double height, string? anchorBlockId = null) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Fixed,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = false,
                FixedOnPage = true
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Page,
                X = -124,
                Y = 318
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.BehindText
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            },
            Stacking = new DocumentObjectStacking
            {
                ZIndex = 0,
                AllowOverlap = true
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

    private static DocumentEditorDocument CreateCanvasAdvancedCharacterDocument(string documentId)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        var sectionId = document.Sections[0].Id;
        document.Metadata.Title = "Canvas advanced character formatting";
        document.Metadata.CreatedAt = CanonicalDemoTimestamp;
        document.Metadata.ModifiedAt = CanonicalDemoTimestamp;
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos, Arial, sans-serif",
            BodyFontSize = 12,
            BodyLineHeight = 1.16,
            ParagraphSpacingAfter = 8
        };
        document.PageSettings = new DocumentPageSettings
        {
            Size = DocumentPageSize.A4,
            Margins = new DocumentPageMargins { Top = 72, Right = 78, Bottom = 72, Left = 78 },
            HeaderDistanceFromTop = 36,
            FooterDistanceFromBottom = 36
        };
        document.Sections[0].Properties.PageSettings = document.PageSettings;

        document.Blocks.Add(new DocumentBlock
        {
            Id = "canvas-e6-heading",
            SectionId = sectionId,
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Id = "canvas-e6-heading-run", Text = "Advanced character controls" }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "canvas-e6-formula",
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            ParagraphProperties = new DocumentParagraphProperties { SpacingAfter = 8, LineSpacing = 1.16 },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "canvas-e6-h", Text = "H" },
                    new TextRun { Id = "canvas-e6-subscript", Text = "2", Marks = [new InlineMark { Type = InlineMarkType.Subscript }] },
                    new TextRun { Id = "canvas-e6-o", Text = "O  " },
                    new TextRun { Id = "canvas-e6-x", Text = "x" },
                    new TextRun { Id = "canvas-e6-superscript", Text = "2", Marks = [new InlineMark { Type = InlineMarkType.Superscript }] },
                    new TextRun { Id = "canvas-e6-formula-tail", Text = " baseline proof" }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "canvas-e6-preformatted",
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Order = 30,
            ParagraphProperties = new DocumentParagraphProperties { SpacingAfter = 8, LineSpacing = 1.16 },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "canvas-e6-smallcaps", Text = "small caps sample", Marks = [new InlineMark { Type = InlineMarkType.SmallCaps }] },
                    new TextRun { Id = "canvas-e6-gap-a", Text = "  " },
                    new TextRun { Id = "canvas-e6-expanded", Text = "expanded", Marks = [new InlineMark { Type = InlineMarkType.CharacterSpacing, Value = "2" }] },
                    new TextRun { Id = "canvas-e6-gap-b", Text = "  " },
                    new TextRun { Id = "canvas-e6-scaled", Text = "scaled", Marks = [new InlineMark { Type = InlineMarkType.CharacterScale, Value = "125" }] },
                    new TextRun { Id = "canvas-e6-gap-c", Text = "  " },
                    new TextRun { Id = "canvas-e6-double", Text = "double strike", Marks = [new InlineMark { Type = InlineMarkType.DoubleStrikethrough }] }
                ]
            }
        });
        document.Blocks.Add(CreateCanvasParagraph(
            sectionId,
            "canvas-e6-command-target",
            40,
            "phase e6 command target for toolbar case and undo"));

        return document;
    }

    private static DocumentBlock CreateCanvasParagraph(
        string sectionId,
        string id,
        double order,
        string text)
        => new()
        {
            Id = id,
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Order = order,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Left,
                LineSpacing = 1.16,
                SpacingAfter = 8
            },
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = $"{id}-run", Text = text }]
            }
        };

    private static DocumentEditorDocument CreateCanvasCommentsRevisionsDocument(string documentId)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        var sectionId = "canvas-comments-revisions-section-main";
        document.Metadata.Title = "Canvas comments and revisions";
        document.Metadata.CreatedAt = CanonicalDemoTimestamp;
        document.Metadata.ModifiedAt = CanonicalDemoTimestamp;
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos, Arial, sans-serif",
            BodyFontSize = 11.5,
            BodyLineHeight = 1.16,
            ParagraphSpacingAfter = 9
        };
        document.PageSettings = new DocumentPageSettings
        {
            Size = DocumentPageSize.A4,
            Margins = new DocumentPageMargins { Top = 72, Right = 78, Bottom = 72, Left = 78 }
        };
        document.Sections[0].Id = sectionId;
        document.Sections[0].Properties.PageSettings = document.PageSettings;

        document.Blocks.Add(new DocumentBlock
        {
            Id = "canvas-phase17-review",
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Order = 10,
            ParagraphProperties = new DocumentParagraphProperties { LineSpacing = 1.16, SpacingAfter = 12 },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "canvas-phase17-comment-run",
                        Text = "Commented clause ",
                        Marks =
                        [
                            new InlineMark
                            {
                                Type = InlineMarkType.CommentAnchor,
                                CommentAnchor = new CommentAnchorMarkData { CommentId = "canvas-phase17-comment" }
                            }
                        ]
                    },
                    new TextRun
                    {
                        Id = "canvas-phase17-insertion-run",
                        Text = "inserted text ",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.Revision, RevisionId = "canvas-phase17-revision-insert", Value = "Insertion" }
                        ]
                    },
                    new TextRun
                    {
                        Id = "canvas-phase17-deletion-run",
                        Text = "deleted text ",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.Revision, RevisionId = "canvas-phase17-revision-delete", Value = "Deletion" }
                        ]
                    },
                    new TextRun
                    {
                        Id = "canvas-phase17-format-run",
                        Text = "formatted text.",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.Bold },
                            new InlineMark { Type = InlineMarkType.Revision, RevisionId = "canvas-phase17-revision-format", Value = "Formatting" }
                        ]
                    }
                ]
            }
        });

        document.Blocks.Add(CreateCanvasParagraph(
            sectionId,
            "canvas-phase17-protected",
            20,
            "Locked prefix editable island locked suffix."));

        document.Comments.Add(new DocumentComment
        {
            Id = "canvas-phase17-comment",
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "canvas-phase17-review",
                StartInlineIndex = 0,
                EndInlineIndex = 0,
                StartOffset = 0,
                EndOffset = 16
            },
            Visibility = DocumentCommentVisibility.Internal,
            Entries =
            [
                new DocumentCommentEntry
                {
                    Id = "canvas-phase17-comment-entry",
                    Author = DemoAuthor,
                    Text = "Canvas phase 17 comment",
                    CreatedAt = CanonicalDemoTimestamp.AddMinutes(17)
                }
            ]
        });

        document.Revisions.Add(CreateCanvasPhase17Revision("canvas-phase17-revision-insert", DocumentRevisionType.Insertion, "canvas-phase17-review", 17, 31, "Inserted text pending review"));
        document.Revisions.Add(CreateCanvasPhase17Revision("canvas-phase17-revision-delete", DocumentRevisionType.Deletion, "canvas-phase17-review", 31, 44, "Deleted text pending review"));
        document.Revisions.Add(CreateCanvasPhase17Revision("canvas-phase17-revision-format", DocumentRevisionType.Formatting, "canvas-phase17-review", 44, 59, """{"markType":"Bold","newActive":true}"""));
        document.IsProtected = true;
        document.RestrictedMarkers.Add(new DocumentRestrictedMarker
        {
            Id = "canvas-phase17-editable-region",
            StartBlockId = "canvas-phase17-protected",
            StartOffset = 14,
            EndBlockId = "canvas-phase17-protected",
            EndOffset = 29,
            Label = "Editable island"
        });

        return document;
    }

    private static DocumentRevision CreateCanvasPhase17Revision(string id, DocumentRevisionType type, string blockId, int startOffset, int endOffset, string payload)
        => new()
        {
            Id = id,
            Type = type,
            Range = new DocumentRevisionRange
            {
                BlockId = blockId,
                StartInlineIndex = 0,
                EndInlineIndex = 3,
                StartOffset = startOffset,
                EndOffset = endOffset
            },
            Author = new DocumentRevisionAuthor
            {
                Id = "demo-reviewer",
                DisplayName = "Demo Reviewer",
                Email = "reviewer@example.local"
            },
            CreatedAt = CanonicalDemoTimestamp.AddMinutes(18),
            Action = DocumentRevisionAction.Pending,
            PayloadJson = payload
        };

    private static DocumentBlock CreateImageDrawingParagraph(
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
                    Width = 560,
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
                            CreateTableCell("Status", isHeader: true, backgroundColor: "#eef2ff", id: "contract-pricing-table-h-status"),
                            CreateTableCell("Evidence", isHeader: true, backgroundColor: "#eef2ff", id: "contract-pricing-table-h-evidence")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Implementation", id: "contract-pricing-table-r1-item"),
                            CreateTableCell("Provider", id: "contract-pricing-table-r1-responsibility"),
                            CreateTableCell("Ready for review", id: "contract-pricing-table-r1-status"),
                            CreateTableCellWithImage("contract-pricing-table", "contract-pricing-table-r1-evidence")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateTableCell("Client data", id: "contract-pricing-table-r2-item"),
                            CreateTableCell("Client", id: "contract-pricing-table-r2-responsibility"),
                            CreateTableCell("Pending confirmation", id: "contract-pricing-table-r2-status"),
                            CreateTableCell("Awaiting upload", id: "contract-pricing-table-r2-evidence")
                        ]
                    }
                ]
            }
        };

    private static TableCellContent CreateTableCellWithImage(string tableId, string cellId)
    {
        const string blockId = "contract-pricing-table-r1-evidence-block";
        var drawing = CreateImageDrawingRun(
            "contract-table-cell-image",
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            "Table cell evidence image",
            "Image wrapped inside a pricing table cell",
            76,
            43,
            CreateTableCellImageLayout(tableId, cellId, blockId, 76, 43));
        drawing.Layout.Anchor.Region = DocumentRenditionAnchorScope.TableCell;
        drawing.Layout.Anchor.TableId = tableId;
        drawing.Layout.Anchor.CellId = cellId;
        drawing.Layout.Anchor.BlockId = blockId;
        drawing.Docx = new DocumentDocxDrawingMetadata { LayoutInCell = true };

        return new TableCellContent
        {
            Id = cellId,
            Padding = 8,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = blockId,
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            drawing,
                            new TextRun
                            {
                                Id = "contract-pricing-table-r1-evidence-text",
                                Text = " Stable table-cell image text proves local wrapping without affecting neighboring cells."
                            }
                        ]
                    }
                }
            ]
        };
    }

    private static DocumentObjectLayout CreateTableCellImageLayout(string tableId, string cellId, string blockId, double width, double height)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = blockId,
                Region = DocumentRenditionAnchorScope.TableCell,
                TableId = tableId,
                CellId = cellId,
                MoveWithText = true
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Column,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Left
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceRight = 6,
                DistanceBottom = 4
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

    private static void AddContractHeaderFooterDrawingRuns(DocumentEditorDocument document)
    {
        AddDrawingRunToHeaderFooter(
            document,
            "contract-header-primary",
            "contract-header-primary-block",
            CreateHeaderFooterDrawingRun(
                "contract-header-logo-image",
                "Header logo evidence",
                DocumentRenditionAnchorScope.Header,
                "contract-header-primary",
                "contract-header-primary-block",
                52,
                29));
        AddDrawingRunToHeaderFooter(
            document,
            "contract-footer-primary",
            "contract-footer-primary-block",
            CreateHeaderFooterDrawingRun(
                "contract-footer-logo-image",
                "Footer logo evidence",
                DocumentRenditionAnchorScope.Footer,
                "contract-footer-primary",
                "contract-footer-primary-block",
                44,
                25));
    }

    private static DocumentDrawingRun CreateHeaderFooterDrawingRun(
        string objectId,
        string altText,
        DocumentRenditionAnchorScope region,
        string headerFooterId,
        string blockId,
        double width,
        double height)
    {
        var drawing = CreateImageDrawingRun(
            objectId,
            DocumentImageSource.Asset,
            null,
            ContractAssetId,
            altText,
            altText,
            width,
            height,
            DocumentObjectLayout.Inline());
        drawing.Layout.Anchor.Region = region;
        drawing.Layout.Anchor.HeaderFooterId = headerFooterId;
        drawing.Layout.Anchor.BlockId = blockId;
        return drawing;
    }

    private static void AddDrawingRunToHeaderFooter(
        DocumentEditorDocument document,
        string headerFooterId,
        string blockId,
        DocumentDrawingRun drawing)
    {
        var paragraph = document.HeadersFooters
            .FirstOrDefault(headerFooter => string.Equals(headerFooter.Id, headerFooterId, StringComparison.Ordinal))
            ?.Blocks.FirstOrDefault(block => string.Equals(block.Id, blockId, StringComparison.Ordinal))
            ?.Content as ParagraphBlockContent;
        if (paragraph is null)
        {
            return;
        }

        drawing.Layout.Anchor.InlineIndex = paragraph.Inlines.Count;
        paragraph.Inlines.Add(drawing);
    }

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
        document.Blocks.Add(CreateImageDrawingParagraph(
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
        document.Blocks.Add(CreateImageDrawingParagraph(
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
