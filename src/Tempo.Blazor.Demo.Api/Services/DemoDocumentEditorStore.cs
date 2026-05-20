using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo document editor store.</summary>
public class DemoDocumentEditorStore : InMemoryDocumentEditorProvider
{
    private const string DemoImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAUAAAAC0CAYAAADl5PURAAACsUlEQVR42u3ZzQ2CQBCAUYuwFLuxDjuwBDqgAK9UZfwB9ECC04JGdJd5hzeZ80C+y252+9MMkNHGEQABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEBAAB0CEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAgAIDOD6nGSAjAQQEEEAAAQQQQAABBBBAAAEEEKDiAA4xADISQEAAAQQQQAABBBBAAAEEEEAAAQQQQAB/pWk74MsEUABBAAVQAEEABVAAQQAFUABBAAUQYBUBfMQCkJAAAgIIIIAAAggggAACCCCAAAIIUHEA+xgAGQkgIIAAAliJ8zgDhRNAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBHIE8D5Oc438XFC+0jsigIAACiAggAIICKAAAgIogIAACiAggAIICKAAAgIogIAACiAggAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKICAAAogIIACCAjgGwG8xViT7fGwiLXdCag4gEIHpAig2AECKHSAAIodkDiAPh6Q9hEEQAABPg7gEAtAQgIICCCAAAIIIIAAAggggAACCCCAAAIIIIAAAgjgMpq2g2KIiQAKIAKIAAogAogACiACiAAKIAJIgQG8xgDISAABAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQIC/BrCPBSAhAQQEEEAAAQQQQAABBBBAAAEEEKDiAF5iAGQkgIAAAgggQBIvAt6vRwtbqO0AAAAASUVORK5CYII=";
    private const string ContractAssetId = "contract-evidence-asset";
    private const string ExhibitAssetId = "exhibit-provider-asset";
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

        contract.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = 30,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = DemoImageUrl,
                AltText = "Embedded evidence preview",
                Caption = "Evidence image loaded from the Demo API",
                Size = new DocumentImageSize { Width = 220, Height = 124 },
                Alignment = DocumentImageAlignment.Start,
                FloatingLayout = CreateLeftWrappedImageLayout()
            }
        });

        contract.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = 40,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = ContractAssetId,
                AltText = "Provider-managed exhibit",
                Caption = "Image resolved through IDocumentImageUrlResolver",
                Size = new DocumentImageSize { Width = 240, Height = 135 },
                Alignment = DocumentImageAlignment.Start,
                FloatingLayout = CreateLeftWrappedImageLayout()
            }
        });

        _ = SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = contract.DocumentId,
            Document = contract,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

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

        _ = CreateCommentAsync(contract.DocumentId, new DocumentComment
        {
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "contract-intro",
                StartInlineIndex = 1,
                EndInlineIndex = 1,
                StartOffset = "This agreement is made with ".Length,
                EndOffset = "This agreement is made with Client name".Length
            },
            Visibility = DocumentCommentVisibility.Internal,
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = DemoAuthor,
                    Text = "Check whether the client token is resolved before export."
                }
            ]
        }).GetAwaiter().GetResult();

        _ = CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = contract.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "1.0",
            Description = "Initial demo version",
            Author = DemoAuthor
        }).GetAwaiter().GetResult();
    }

    private static DocumentFloatingLayout CreateLeftWrappedImageLayout() =>
        new()
        {
            Inline = false,
            WrapMode = DocumentWrapMode.Square,
            HorizontalPosition = DocumentImageHorizontalPosition.Left,
            HorizontalRelativeTo = DocumentRelativePosition.Page,
            VerticalRelativeTo = DocumentRelativePosition.Paragraph,
            DistanceRight = 16,
            DistanceBottom = 12
        };

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
                Inlines = [new TextRun { Text = "This demo document keeps image blocks in the editor JSON model." }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = 30,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = DemoImageUrl,
                AltText = "URL exhibit",
                Caption = "Image inserted from a URL",
                Size = new DocumentImageSize { Width = 220, Height = 124 },
                Alignment = DocumentImageAlignment.Center
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = 40,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = ExhibitAssetId,
                AltText = "Provider exhibit",
                Caption = "Image resolved through the demo image provider",
                Size = new DocumentImageSize { Width = 240, Height = 135 },
                Alignment = DocumentImageAlignment.Center
            }
        });
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

    private static TableCellContent CreateTableCell(string text, bool isHeader = false, string? backgroundColor = null)
    {
        return new TableCellContent
        {
            IsHeader = isHeader,
            BackgroundColor = backgroundColor,
            Padding = 8,
            Blocks =
            [
                new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Text = text }]
                    }
                }
            ]
        };
    }
}
