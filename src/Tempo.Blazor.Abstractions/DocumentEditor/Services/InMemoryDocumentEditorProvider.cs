using System.Security.Cryptography;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>In-memory document editor provider intended for tests and demos.</summary>
public class InMemoryDocumentEditorProvider : IDocumentEditorProvider, IDocumentAuditSink
{
    private readonly Dictionary<string, StoredDocument> _documents = [];
    private readonly Dictionary<string, List<DocumentVersion>> _versions = [];
    private readonly List<DocumentEditorAuditEvent> _auditEvents = [];

    /// <summary>Recorded audit events.</summary>
    public IReadOnlyList<DocumentEditorAuditEvent> AuditEvents => _auditEvents;

    /// <summary>Clears all in-memory documents, versions, and audit events.</summary>
    protected void ClearStore()
    {
        _documents.Clear();
        _versions.Clear();
        _auditEvents.Clear();
    }

    /// <summary>Seeds a new empty document.</summary>
    public DocumentEditorDocument SeedEmptyDocument(string documentId = "empty-document")
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Empty document";
        StoreDocument(document);
        return Clone(document);
    }

    /// <summary>Seeds a representative contract document.</summary>
    public DocumentEditorDocument SeedContractDocument(string documentId = "contract-demo")
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Service agreement";
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos, Arial, sans-serif",
            BodyFontSize = 11.5,
            BodyLineHeight = 1.25,
            ParagraphSpacingAfter = 9
        };
        document.Sections[0].Id = "contract-section-main";
        document.Sections[0].Title = "Agreement";
        document.Sections[0].Properties.HeaderFooterReferences =
        [
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "contract-header-primary",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary
            },
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "contract-footer-primary",
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary
            }
        ];
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-heading",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-heading-text",
                        Text = "Service agreement",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.FontFamily, Value = "Aptos Display, Aptos, Arial, sans-serif" },
                            new InlineMark { Type = InlineMarkType.FontSize, Value = "24pt" }
                        ]
                    }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-intro",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Justify,
                LineSpacing = 1.25,
                SpacingAfter = 10
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-intro-prefix",
                        Text = "This agreement is made with ",
                        Marks = [new InlineMark { Type = InlineMarkType.Bold }]
                    },
                    new TokenRun
                    {
                        Id = "contract-client-token",
                        Key = "client.name",
                        DisplayName = "Client name",
                        TokenType = "text",
                        TypeLabel = "CRM",
                        FallbackText = "Acme s.r.o."
                    },
                    new TextRun { Id = "contract-intro-suffix", Text = "." }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-scope",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Paragraph,
            Order = 25,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Left,
                LineSpacing = 1.25,
                SpacingAfter = 12
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-scope-approved",
                        Text = "The provider will deliver implementation, training, and documentation services.",
                        Marks = [new InlineMark { Type = InlineMarkType.TextColor, Value = "#1f2937" }]
                    },
                    new TextRun
                    {
                        Id = "contract-scope-revision",
                        Text = " Priority support is included during the first thirty days.",
                        Marks =
                        [
                            new InlineMark
                            {
                                Type = InlineMarkType.Revision,
                                RevisionId = "contract-revision-scope",
                                Value = "Insertion"
                            }
                        ]
                    }
                ]
            }
        });
        document.HeadersFooters.Add(CreateSeedHeaderFooter(
            "contract-header-primary",
            DocumentHeaderFooterType.Header,
            "Tempo Legal - Service agreement"));
        document.HeadersFooters.Add(CreateSeedHeaderFooter(
            "contract-footer-primary",
            DocumentHeaderFooterType.Footer,
            "Confidential - Page 1"));
        document.Revisions.Add(new DocumentRevision
        {
            Id = "contract-revision-scope",
            Type = DocumentRevisionType.Insertion,
            Range = new DocumentRevisionRange
            {
                BlockId = "contract-scope",
                StartInlineIndex = 1,
                EndInlineIndex = 1,
                StartOffset = 0,
                EndOffset = 60
            },
            Author = new DocumentRevisionAuthor
            {
                Id = "demo-reviewer",
                DisplayName = "Demo Reviewer",
                Email = "reviewer@example.local"
            },
            CreatedAt = new DateTimeOffset(2026, 5, 14, 8, 30, 0, TimeSpan.Zero),
            Action = DocumentRevisionAction.Pending,
            PayloadJson = "Priority support is included during the first thirty days."
        });

        StoreDocument(document);
        return Clone(document);
    }

    /// <summary>Seeds a simple court filing document.</summary>
    public DocumentEditorDocument SeedFilingDocument(string documentId = "filing-demo")
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Court filing";
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = "Court filing" }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "The claimant submits the following petition." }]
            }
        });

        StoreDocument(document);
        return Clone(document);
    }

    /// <inheritdoc />
    public virtual Task<DocumentEditorLoadResult> LoadAsync(
        string documentId,
        DocumentEditorLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DocumentEditorLoadOptions();
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            return Task.FromResult(DocumentEditorLoadResult.NotFound());
        }

        return Task.FromResult(new DocumentEditorLoadResult
        {
            Found = true,
            Document = options.IncludeDocument ? Clone(stored.Document) : null,
            JsonSnapshot = options.IncludeJson ? stored.Json : null,
            ConcurrencyToken = stored.ConcurrencyToken
        });
    }

    /// <inheritdoc />
    public Task<string?> LoadJsonAsync(string documentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_documents.TryGetValue(documentId, out var stored) ? stored.Json : null);
    }

    /// <inheritdoc />
    public virtual Task<DocumentEditorSaveResult> SaveAsync(
        DocumentEditorSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_documents.TryGetValue(request.DocumentId, out var stored)
            && request.ConcurrencyMode == DocumentEditorConcurrencyMode.Required
            && stored.ConcurrencyToken != request.BaseConcurrencyToken)
        {
            return Task.FromResult(DocumentEditorSaveResult.ConcurrencyConflict(stored.ConcurrencyToken));
        }

        if (_documents.TryGetValue(request.DocumentId, out stored)
            && request.ConcurrencyMode == DocumentEditorConcurrencyMode.Optional
            && request.BaseConcurrencyToken is not null
            && stored.ConcurrencyToken != request.BaseConcurrencyToken)
        {
            return Task.FromResult(DocumentEditorSaveResult.ConcurrencyConflict(stored.ConcurrencyToken));
        }

        var document = request.Document ?? DocumentEditorJson.Deserialize(request.JsonSnapshot ?? string.Empty);
        document.DocumentId = request.DocumentId;
        document.Metadata.ModifiedAt = DateTimeOffset.UtcNow;

        var json = request.Document is not null
            ? DocumentEditorJson.Serialize(document)
            : request.NormalizeJson ? DocumentEditorJson.Normalize(request.JsonSnapshot!) : request.JsonSnapshot!;

        var savedDocument = request.Document is not null ? Clone(document) : DocumentEditorJson.Deserialize(json);
        var concurrencyToken = CreateConcurrencyToken();
        _documents[request.DocumentId] = new StoredDocument(savedDocument, json, concurrencyToken);

        return Task.FromResult(DocumentEditorSaveResult.Saved(Clone(savedDocument), json, concurrencyToken));
    }

    /// <inheritdoc />
    public virtual Task<DocumentVersion> CreateVersionAsync(
        DocumentVersionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(request.DocumentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{request.DocumentId}' was not found.");
        }

        var snapshot = new DocumentVersionSnapshot
        {
            DocumentId = request.DocumentId,
            SchemaVersion = stored.Document.SchemaVersion,
            Json = stored.Json
        };
        snapshot.Hash = DocumentVersionHashHelper.ComputeSnapshotHash(snapshot);

        var version = new DocumentVersion
        {
            DocumentId = request.DocumentId,
            Kind = request.Kind,
            Label = request.Label,
            Description = request.Description,
            Author = request.Author,
            Snapshot = snapshot
        };

        if (!_versions.TryGetValue(request.DocumentId, out var versions))
        {
            versions = [];
            _versions[request.DocumentId] = versions;
        }

        versions.Add(Clone(version));
        return Task.FromResult(Clone(version));
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var versions = _versions.TryGetValue(documentId, out var stored)
            ? stored.Select(Clone).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<DocumentVersion>>(versions);
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var comments = _documents.TryGetValue(documentId, out var stored)
            ? stored.Document.Comments.Select(Clone).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<DocumentComment>>(comments);
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> CreateCommentAsync(
        string documentId,
        DocumentComment comment,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var storedComment = NormalizeComment(Clone(comment));
        stored.Document.Comments.Add(storedComment);
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(storedComment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> AddCommentReplyAsync(
        string documentId,
        string commentId,
        DocumentCommentEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        var storedEntry = NormalizeCommentEntry(Clone(entry));
        comment.Entries.Add(storedEntry);
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> UpdateCommentEntryAsync(
        string documentId,
        string commentId,
        string entryId,
        string text,
        DocumentEditorAuthor updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        var entry = comment.Entries.First(item => item.Id == entryId);
        entry.Text = text.Trim();
        entry.ModifiedAt = DateTimeOffset.UtcNow;
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> ResolveCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor resolvedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        comment.Status = DocumentCommentStatus.Resolved;
        comment.ResolvedAt = DateTimeOffset.UtcNow;
        comment.ResolvedBy = resolvedBy;
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> ReopenCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor reopenedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        comment.Status = DocumentCommentStatus.Open;
        comment.ResolvedAt = null;
        comment.ResolvedBy = null;
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task DeleteCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor deletedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        stored.Document.Comments.RemoveAll(item => item.Id == commentId);
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordAsync(DocumentEditorAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _auditEvents.Add(Clone(auditEvent));
        return Task.CompletedTask;
    }

    private void StoreDocument(DocumentEditorDocument document, string? concurrencyToken = null)
    {
        var clone = Clone(document);
        _documents[clone.DocumentId] = new StoredDocument(
            clone,
            DocumentEditorJson.Serialize(clone),
            concurrencyToken ?? CreateConcurrencyToken());
    }

    private static string CreateConcurrencyToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    private static DocumentComment NormalizeComment(DocumentComment comment)
    {
        if (string.IsNullOrWhiteSpace(comment.Id))
        {
            comment.Id = Guid.NewGuid().ToString("N");
        }

        foreach (var entry in comment.Entries)
        {
            NormalizeCommentEntry(entry);
        }

        return comment;
    }

    private static DocumentCommentEntry NormalizeCommentEntry(DocumentCommentEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            entry.Id = Guid.NewGuid().ToString("N");
        }

        if (entry.CreatedAt == default)
        {
            entry.CreatedAt = DateTimeOffset.UtcNow;
        }

        return entry;
    }

    private static DocumentHeaderFooter CreateSeedHeaderFooter(string id, DocumentHeaderFooterType type, string text)
    {
        return new DocumentHeaderFooter
        {
            Id = id,
            Type = type,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = "contract-section-main",
            Blocks =
            [
                new DocumentBlock
                {
                    Id = $"{id}-block",
                    SectionId = "contract-section-main",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun
                            {
                                Id = $"{id}-text",
                                Text = text,
                                Marks = [new InlineMark { Type = InlineMarkType.FontSize, Value = "9pt" }]
                            }
                        ]
                    }
                }
            ]
        };
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document editor value.");
    }

    private sealed record StoredDocument(DocumentEditorDocument Document, string Json, string ConcurrencyToken);
}
