using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Services;

/// <summary>Demo provider for <c>TmDocumentEditor</c>.</summary>
public class DemoDocumentEditorProvider : InMemoryDocumentEditorProvider
{
    private const string DemoImageUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAUAAAAC0CAYAAADl5PURAAACsUlEQVR42u3ZzQ2CQBCAUYuwFLuxDjuwBDqgAK9UZfwB9ECC04JGdJd5hzeZ80C+y252+9MMkNHGEQABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEBAAB0CEEAAAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAgAIDOD6nGSAjAQQEEEAAAQQQQAABBBBAAAEEEKDiAA4xADISQEAAAQQQQAABBBBAAAEEEEAAAQQQQAB/pWk74MsEUABBAAVQAEEABVAAQQAFUABBAAUQYBUBfMQCkJAAAgIIIIAAAggggAACCCCAAAIIUHEA+xgAGQkgIIAAAliJ8zgDhRNAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEABBAEUQAEEARRAAQQBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBARQAAEBFEBAAAUQEEABBHIE8D5Oc438XFC+0jsigIAACiAggAIICKAAAgIogIAACiAggAIICKAAAgIogIAACiAggAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKIACCAIogAIIAiiAAggCKICAAAogIIACCAjgGwG8xViT7fGwiLXdCag4gEIHpAig2AECKHSAAIodkDiAPh6Q9hEEQAABPg7gEAtAQgIICCCAAAIIIIAAAggggAACCCCAAAIIIIAAAgjgMpq2g2KIiQAKIAKIAAogAogACiACiAAKIAJIgQG8xgDISAABAQQQQAABBBBAAAEEEEAAAQQQQAABBBBAAAEEEEAAAQQQQIC/BrCPBSAhAQQEEEAAAQQQQAABBBBAAAEEEKDiAF5iAGQkgIAAAgggQBIvAt6vRwtbqO0AAAAASUVORK5CYII=";
    private const string ContractUrlImageUrl = "/document-editor-evidence.svg";
    private readonly HttpClient? _http;

    /// <summary>Creates the demo provider with sample legal documents.</summary>
    public DemoDocumentEditorProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
        var contract = SeedContractDocument("contract-demo");
        var filing = SeedFilingDocument("filing-demo");
        var exhibits = CreateExhibitsDocument("exhibits-demo");
        var table = CreateTablePropertiesDocument("table-demo");

        contract.Blocks.Add(new DocumentBlock
        {
            Id = "contract-evidence-url-image",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Image,
            Order = 31,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = ContractUrlImageUrl,
                AltText = "URL evidence preview",
                Caption = "Evidence preview loaded from a URL",
                Size = new DocumentImageSize { Width = 160, Height = 90 },
                NaturalSize = new DocumentImageSize { Width = 160, Height = 90 },
                Alignment = DocumentImageAlignment.Start,
                Layout = CreateLeftWrappedImageLayout(160, 90, "contract-image-wrap-demo-text")
            }
        });

        contract.Blocks.Add(new DocumentBlock
        {
            Id = "contract-image-wrap-demo-text",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Paragraph,
            Order = 32,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Left,
                LineSpacing = 1.25,
                SpacingAfter = 24
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-image-wrap-demo-run",
                        Text = "This longer clause demonstrates live text wrapping around the evidence preview. Click any visual line beside the image, continue typing, resize or move the object, and the paragraph should reflow as one normal editable paragraph. The sample intentionally keeps only one wrapped object in this paragraph so the demo opens in a readable state."
                    }
                ]
            }
        });

        contract.Blocks.Add(new DocumentBlock
        {
            Id = "contract-missing-alt-image",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Image,
            Order = 70,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = ContractUrlImageUrl,
                AltText = null,
                Caption = "Accessibility sample: missing alt text",
                Size = new DocumentImageSize { Width = 180, Height = 102 },
                NaturalSize = new DocumentImageSize { Width = 180, Height = 102 },
                Alignment = DocumentImageAlignment.Center,
                Layout = DocumentObjectLayout.Inline()
            }
        });

        contract.Blocks.Add(new DocumentBlock
        {
            Id = "contract-provider-asset-image",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Image,
            Order = 50,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = DemoDocumentImageUrlResolver.ContractAssetId,
                AltText = "Provider-managed exhibit",
                Caption = "Image resolved through IDocumentImageUrlResolver",
                Size = new DocumentImageSize { Width = 240, Height = 135 },
                NaturalSize = new DocumentImageSize { Width = 240, Height = 135 },
                Alignment = DocumentImageAlignment.Center,
                Layout = DocumentObjectLayout.Inline()
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

        _ = base.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = table.DocumentId,
            Document = table,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force
        }).GetAwaiter().GetResult();

        _ = base.CreateCommentAsync(contract.DocumentId, new DocumentComment
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

        _ = base.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = contract.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "1.0",
            Description = "Initial demo version",
            Author = DemoAuthor
        }).GetAwaiter().GetResult();
    }

    /// <summary>Forces demo saves to return a recoverable provider error.</summary>
    public bool FailDemoSaves { get; set; }

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
        if (FailDemoSaves)
        {
            return new DocumentEditorSaveResult
            {
                Success = false,
                ErrorKind = DocumentEditorSaveErrorKind.Recoverable,
                ErrorMessage = "Demo autosave provider failed."
            };
        }

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
    public override async Task<DocumentComment> UpdateCommentEntryAsync(
        string documentId,
        string commentId,
        string entryId,
        string text,
        DocumentEditorAuthor updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PutAsJsonAsync(
                    $"api/document-editor/documents/{Uri.EscapeDataString(documentId)}/comments/{Uri.EscapeDataString(commentId)}/entries/{Uri.EscapeDataString(entryId)}",
                    new DocumentCommentEntryUpdateRequest { Text = text, UpdatedBy = updatedBy },
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

        return await base.UpdateCommentEntryAsync(documentId, commentId, entryId, text, updatedBy, cancellationToken);
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
                Url = ContractUrlImageUrl,
                AltText = "URL exhibit",
                Caption = "Image inserted from a URL",
                Size = new DocumentImageSize { Width = 220, Height = 124 },
                NaturalSize = new DocumentImageSize { Width = 220, Height = 124 },
                Alignment = DocumentImageAlignment.Start,
                Layout = CreateLeftWrappedImageLayout(220, 124)
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
                NaturalSize = new DocumentImageSize { Width = 240, Height = 135 },
                Alignment = DocumentImageAlignment.Center,
                Layout = CreateTopBottomImageLayout(240, 135)
            }
        });
        return document;
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

    private static DocumentObjectLayout CreateRightWrappedImageLayout(double width, double height) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
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

    private static DocumentObjectLayout CreateTopBottomImageLayout(double width, double height) =>
        new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
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
