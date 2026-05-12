using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo document editor store.</summary>
public class DemoDocumentEditorStore : InMemoryDocumentEditorProvider
{
    private const string DemoImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
    private const string ContractAssetId = "contract-evidence-asset";
    private const string ExhibitAssetId = "exhibit-provider-asset";
    private readonly Dictionary<string, StoredDocumentImage> _images = [];
    private readonly InMemoryDocumentRenditionProvider _renditionProvider;

    /// <summary>Creates a seeded server-side document store.</summary>
    public DemoDocumentEditorStore()
    {
        _renditionProvider = new InMemoryDocumentRenditionProvider(this, this);
        var contract = SeedContractDocument("contract-demo");
        var filing = SeedFilingDocument("filing-demo");
        var exhibits = CreateExhibitsDocument("exhibits-demo");

        contract.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = 30,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = DemoImageUrl,
                AltText = "Embedded evidence preview",
                Caption = "Evidence image loaded from the Demo API"
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
                Caption = "Image resolved through IDocumentImageUrlResolver"
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

        _ = CreateCommentAsync(contract.DocumentId, new DocumentComment
        {
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.Block,
                BlockId = contract.Blocks.FirstOrDefault()?.Id
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
                Caption = "Image inserted from a URL"
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
                Caption = "Image resolved through the demo image provider"
            }
        });
        return document;
    }
}
