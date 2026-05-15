using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Services;

/// <summary>Demo provider for <c>TmDocumentEditor</c>.</summary>
public class DemoDocumentEditorProvider : InMemoryDocumentEditorProvider
{
    private const string DemoImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAUAAAAC0CAYAAADl5PURAAACsUlEQVR42u3ZzQ2CQBCAUYuwFLuxDjuwBDqgAK9UZfwB9ECC04JGdJd5hzeZ80C+y252+9MMkNHGEQABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEBAAB0CEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAgAIDOD6nGSAjAQQEEEAAAQQQQAABBBBAAAEEEKDiAA4xADISQEAAAQQQQAABBBBAAAEEEEAAAQQQQAB/pWk74MsEUABBAAVQAEEABVAAQQAFUABBAAUQYBUBfMQCkJAAAgIIIIAAAggggAACCCCAAAIIUHEA+xgAGQkgIIAAAliJ8zgDhRNAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBHIE8D5Oc438XFC+0jsigIAACiAggAIICKAAAgIogIAACiAggAIICKAAAgIogIAACiAggAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKICAAAogIIACCAjgGwG8xViT7fGwiLXdCag4gEIHpAig2AECKHSAAIodkDiAPh6Q9hEEQAABPg7gEAtAQgIICCCAAAIIIIAAAggggAACCCCAAAIIIIAAAgjgMpq2g2KIiQAKIAKIAAogAogACiACiAAKIAJIgQG8xgDISAABAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQIC/BrCPBSAhAQQEEEAAAQQQQAABBBBAAAEEEKDiAF5iAGQkgIAAAgggQBIvAt6vRwtbqO0AAAAASUVORK5CYII=";
    private readonly HttpClient? _http;

    /// <summary>Creates the demo provider with sample legal documents.</summary>
    public DemoDocumentEditorProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
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
                Caption = "Evidence image loaded from a URL",
                Size = new DocumentImageSize { Width = 220, Height = 124 },
                Alignment = DocumentImageAlignment.Center
            }
        });

        contract.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = 40,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = DemoDocumentImageUrlResolver.ContractAssetId,
                AltText = "Provider-managed exhibit",
                Caption = "Image resolved through IDocumentImageUrlResolver",
                Size = new DocumentImageSize { Width = 240, Height = 135 },
                Alignment = DocumentImageAlignment.Center
            }
        });

        _ = base.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = contract.DocumentId,
            Document = contract,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        _ = base.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = filing.DocumentId,
            Document = filing,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        _ = base.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = exhibits.DocumentId,
            Document = exhibits,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        _ = base.CreateCommentAsync(contract.DocumentId, new DocumentComment
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

        _ = base.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = contract.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "1.0",
            Description = "Initial demo version",
            Author = DemoAuthor
        }).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override async Task<DocumentEditorLoadResult> LoadAsync(
        string documentId,
        DocumentEditorLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<DocumentEditorLoadResult>(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}",
                    cancellationToken);

                if (result?.Document is not null || result?.Found == false)
                {
                    return result;
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.LoadAsync(documentId, options, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentEditorSaveResult> SaveAsync(
        DocumentEditorSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PutAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(request.DocumentId)}",
                    request,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DocumentEditorSaveResult>(
                        cancellationToken);
                    if (result is not null)
                    {
                        return result;
                    }
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.SaveAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentVersion> CreateVersionAsync(
        DocumentVersionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(request.DocumentId)}/versions",
                    request,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var version = await response.Content.ReadFromJsonAsync<DocumentVersion>(cancellationToken);
                    if (version is not null)
                    {
                        return version;
                    }
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.CreateVersionAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var versions = await _http.GetFromJsonAsync<List<DocumentVersion>>(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/versions",
                    cancellationToken);
                if (versions is not null)
                {
                    return versions;
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.GetVersionsAsync(documentId, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var comments = await _http.GetFromJsonAsync<List<DocumentComment>>(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments",
                    cancellationToken);
                if (comments is not null)
                {
                    return comments;
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.GetCommentsAsync(documentId, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentComment> CreateCommentAsync(
        string documentId,
        DocumentComment comment,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments",
                    comment,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var created = await response.Content.ReadFromJsonAsync<DocumentComment>(cancellationToken);
                    if (created is not null)
                    {
                        return created;
                    }
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.CreateCommentAsync(documentId, comment, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentComment> AddCommentReplyAsync(
        string documentId,
        string commentId,
        DocumentCommentEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments/{Uri.EscapeDataString(commentId)}/replies",
                    entry,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var updated = await response.Content.ReadFromJsonAsync<DocumentComment>(cancellationToken);
                    if (updated is not null)
                    {
                        return updated;
                    }
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.AddCommentReplyAsync(documentId, commentId, entry, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentComment> ResolveCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor resolvedBy,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments/{Uri.EscapeDataString(commentId)}/resolve",
                    resolvedBy,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var updated = await response.Content.ReadFromJsonAsync<DocumentComment>(cancellationToken);
                    if (updated is not null)
                    {
                        return updated;
                    }
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.ResolveCommentAsync(documentId, commentId, resolvedBy, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentComment> ReopenCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor reopenedBy,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments/{Uri.EscapeDataString(commentId)}/reopen",
                    reopenedBy,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var updated = await response.Content.ReadFromJsonAsync<DocumentComment>(cancellationToken);
                    if (updated is not null)
                    {
                        return updated;
                    }
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        return await base.ReopenCommentAsync(documentId, commentId, reopenedBy, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task DeleteCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor deletedBy,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments/{Uri.EscapeDataString(commentId)}/delete",
                    deletedBy,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Demo applications remain usable when the optional Demo API is not running.
            }
        }

        await base.DeleteCommentAsync(documentId, commentId, deletedBy, cancellationToken);
    }

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
                AssetId = DemoDocumentImageUrlResolver.ExhibitAssetId,
                AltText = "Provider exhibit",
                Caption = "Image resolved through the demo image provider",
                Size = new DocumentImageSize { Width = 240, Height = 135 },
                Alignment = DocumentImageAlignment.Center
            }
        });
        return document;
    }
}
